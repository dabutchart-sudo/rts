using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SquadStrengthState
{
    Healthy,
    Depleted,
    Critical
}

public enum SquadRecoveryState
{
    Normal,
    Recovering
}

public sealed class SquadAIController : MonoBehaviour
{
    public static SquadAIController Instance { get; private set; }

    [Header("Objective Persistence")]
    [SerializeField] private bool persistSquadObjectives = true;
    [SerializeField] private bool logObjectiveChanges = true;

    [Header("AI Think Timing")]
    [SerializeField] private float aiThinkInterval = 4f;
    [SerializeField] private float aiThinkJitter = 0.75f;
    [SerializeField] private float objectiveSwitchThreshold = 8f;

    [Header("Objective Scoring")]
    [SerializeField] private float distanceWeight = 0.45f;
    [SerializeField] private float captureNeedWeight = 0.35f;
    [SerializeField] private float friendlyPresenceWeight = 3f;
    [SerializeField] private float enemyPressureWeight = 5f;
    [SerializeField] private float roleBiasWeight = 18f;
    [SerializeField] private float currentObjectiveBonus = 10f;
    [SerializeField] private float objectiveCrowdingPenalty = 7f;

    [Header("Captured Point Garrisons")]
    [Tooltip("When enabled, one attacker squad remains on each captured point while other squads continue the attack.")]
    [SerializeField] private bool enableCapturedPointGarrisons = true;

    [Tooltip("A garrison is released once attacker control falls to or below this capture progress.")]
    [Range(-100f, 100f)]
    [SerializeField] private float garrisonReleaseProgress = 0f;

    [Tooltip("Write garrison assignment and release events to the Console.")]
    [SerializeField] private bool logGarrisonChanges = true;

    [Header("Squad Strength")]
    [Tooltip("Squads at or above this member count are treated as healthy.")]
    [SerializeField] private int healthyMemberThreshold = 3;

    [Tooltip("Squads at or below this member count are treated as critical.")]
    [SerializeField] private int criticalMemberThreshold = 1;

    [Tooltip("Extra penalty per enemy on an objective when a squad is depleted.")]
    [SerializeField] private float depletedEnemyPenalty = 6f;

    [Tooltip("Extra penalty per enemy on an objective when a squad is critical.")]
    [SerializeField] private float criticalEnemyPenalty = 14f;

    [Tooltip("Bonus per friendly already at an objective when a squad is depleted.")]
    [SerializeField] private float depletedFriendlySafetyBonus = 2.5f;

    [Tooltip("Bonus per friendly already at an objective when a squad is critical.")]
    [SerializeField] private float criticalFriendlySafetyBonus = 5f;

    [Tooltip("Bonus for a friendly-controlled objective when a squad is depleted.")]
    [SerializeField] private float depletedSafeObjectiveBonus = 18f;

    [Tooltip("Bonus for a friendly-controlled objective when a squad is critical.")]
    [SerializeField] private float criticalSafeObjectiveBonus = 42f;

    [Tooltip("How strongly reduced squads prefer objectives that are already closer to friendly control.")]
    [SerializeField] private float reducedSquadControlBias = 0.18f;

    [Header("Reinforcement Recovery")]
    [Tooltip("When enabled, reduced squads deliberately favour safe friendly positions while waiting for reinforcements.")]
    [SerializeField] private bool enableReinforcementRecovery = true;

    [Tooltip("Extra bonus applied to a friendly-controlled objective while a depleted squad is recovering.")]
    [SerializeField] private float depletedRecoverySafeBonus = 28f;

    [Tooltip("Extra bonus applied to a friendly-controlled objective while a critical squad is recovering.")]
    [SerializeField] private float criticalRecoverySafeBonus = 65f;

    [Tooltip("Penalty per metre from the faction reinforcement spawn while recovering. This encourages reduced squads to fall back rather than push deep.")]
    [SerializeField] private float recoveryDepthPenalty = 0.35f;

    [Tooltip("Extra penalty for entering a contested objective while recovering.")]
    [SerializeField] private float recoveryContestedPenalty = 24f;

    [Tooltip("Bonus for recovering near friendly soldiers, per friendly occupant at the objective.")]
    [SerializeField] private float recoveryFriendlyPresenceBonus = 4f;

    private readonly Dictionary<string, float> nextThinkTimes = new Dictionary<string, float>();
    private readonly Dictionary<string, SquadStrengthState> lastStrengthStates = new Dictionary<string, SquadStrengthState>();
    private readonly Dictionary<string, string> attackerGarrisonByPoint = new Dictionary<string, string>();
    private int garrisonSectorIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeController()
    {
        EnsureInstance();
    }

    public static SquadAIController EnsureInstance()
    {
        if (Instance != null) return Instance;

        SquadAIController existing = FindAnyObjectByType<SquadAIController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("SquadAIController");
        return host.AddComponent<SquadAIController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Transform GetStrategicTarget(GameObject unit, bool isAttacker)
    {
        if (GameManager.Instance == null)
        {
            return null;
        }

        Squad squad = GetSquad(unit);
        if (squad == null)
        {
            return GameManager.Instance.GetCurrentTarget(unit, isAttacker);
        }

        int sectorIndex = GameManager.Instance.currentSectorIndex;
        Sector[] sectors = GameManager.Instance.sectors;

        if (GameManager.Instance.isTransitioningSector || sectors == null || sectorIndex < 0 || sectorIndex >= sectors.Length)
        {
            return null;
        }

        List<CapturePoint> activePoints = GetActivePoints(sectors[sectorIndex]);
        if (activePoints.Count == 0)
        {
            squad.ClearStrategicObjective();
            return null;
        }

        if (isAttacker && enableCapturedPointGarrisons)
        {
            RefreshAttackerGarrisons(activePoints, sectorIndex);
            CapturePoint garrisonPoint = GetGarrisonPointForSquad(squad, activePoints, sectorIndex);

            if (garrisonPoint != null)
            {
                Transform previousObjective = squad.StrategicObjective;
                squad.AssignStrategicObjective(garrisonPoint.transform, sectorIndex);

                if (logObjectiveChanges && previousObjective != garrisonPoint.transform)
                {
                    Debug.Log($"Squad AI: Attacker {squad.DisplayName} [GARRISON/{GetStrengthState(squad)}] -> {garrisonPoint.capturePointName}");
                }

                return garrisonPoint.transform;
            }
        }

        bool strengthChanged = RecordAndCheckStrengthChange(squad);
        bool existingObjectiveValid = IsExistingObjectiveStillValid(squad, activePoints, sectorIndex, isAttacker);

        if (persistSquadObjectives && existingObjectiveValid && !strengthChanged && !IsThinkDue(squad))
        {
            return squad.StrategicObjective;
        }

        ScheduleNextThink(squad);

        ObjectiveDecision bestDecision = ChooseBestScoredObjective(squad, activePoints, isAttacker);
        Transform previousObjective = squad.StrategicObjective;

        if (persistSquadObjectives && existingObjectiveValid && previousObjective != null && bestDecision.Target != previousObjective)
        {
            CapturePoint currentPoint = FindCapturePoint(activePoints, previousObjective);
            if (currentPoint != null)
            {
                float currentScore = ScoreObjective(squad, currentPoint, activePoints, isAttacker, true);
                float switchThreshold = GetSwitchThresholdForStrength(squad);

                if (bestDecision.Score < currentScore + switchThreshold)
                {
                    return previousObjective;
                }
            }
        }

        squad.AssignStrategicObjective(bestDecision.Target, sectorIndex);

        if (logObjectiveChanges && bestDecision.Target != previousObjective)
        {
            string objectiveName = bestDecision.Target != null ? bestDecision.Target.name : "None";
            Debug.Log($"Squad AI: {squad.Faction} {squad.DisplayName} [{squad.Role}/{GetStrengthState(squad)}/{GetRecoveryState(squad)}] -> {objectiveName} | score {bestDecision.Score:F1}");
        }

        return bestDecision.Target;
    }

    public string GetDiagnosticReport(Squad squad)
    {
        if (squad == null) return "No squad";

        StringBuilder report = new StringBuilder();
        string objectiveName = squad.StrategicObjective != null ? squad.StrategicObjective.name : "None";
        float timeUntilThink = GetTimeUntilNextThink(squad);
        SquadStrengthState strength = GetStrengthState(squad);
        SquadRecoveryState recovery = GetRecoveryState(squad);

        report.Append($"{squad.DisplayName} [{squad.Role}]  Strength {strength} ({squad.MemberCount})  Recovery {recovery}");
        report.Append($"\nOrder {squad.CurrentOrder}/{squad.CurrentCommandSource}  Objective: {objectiveName}  Next think: {timeUntilThink:0.0}s");

        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            return report.ToString();
        }

        int sectorIndex = GameManager.Instance.currentSectorIndex;
        if (sectorIndex < 0 || sectorIndex >= GameManager.Instance.sectors.Length)
        {
            return report.ToString();
        }

        List<CapturePoint> activePoints = GetActivePoints(GameManager.Instance.sectors[sectorIndex]);
        bool isAttacker = squad.Faction == Faction.Attacker;

        if (isAttacker && enableCapturedPointGarrisons)
        {
            CapturePoint garrisonPoint = GetGarrisonPointForSquad(squad, activePoints, sectorIndex);
            if (garrisonPoint != null)
            {
                report.Append($"\nDuty GARRISON: {garrisonPoint.capturePointName}");
            }
        }

        foreach (CapturePoint point in activePoints)
        {
            float score = ScoreObjective(squad, point, activePoints, isAttacker, point.transform == squad.StrategicObjective);
            report.Append($"\n  {point.capturePointName}: {score:0.0}  cap {point.captureProgress:0}%  A{point.attackerCount}/D{point.defenderCount}");
        }

        return report.ToString();
    }

    public SquadStrengthState GetStrengthState(Squad squad)
    {
        int memberCount = squad != null ? squad.MemberCount : 0;

        if (memberCount <= Mathf.Max(0, criticalMemberThreshold))
        {
            return SquadStrengthState.Critical;
        }

        if (memberCount < Mathf.Max(1, healthyMemberThreshold))
        {
            return SquadStrengthState.Depleted;
        }

        return SquadStrengthState.Healthy;
    }

    public SquadRecoveryState GetRecoveryState(Squad squad)
    {
        if (!enableReinforcementRecovery || squad == null)
        {
            return SquadRecoveryState.Normal;
        }

        return GetStrengthState(squad) == SquadStrengthState.Healthy
            ? SquadRecoveryState.Normal
            : SquadRecoveryState.Recovering;
    }

    public float GetTimeUntilNextThink(Squad squad)
    {
        if (squad == null || string.IsNullOrEmpty(squad.SquadId)) return 0f;
        if (!nextThinkTimes.TryGetValue(squad.SquadId, out float nextThink)) return 0f;
        return Mathf.Max(0f, nextThink - Time.time);
    }

    private void RefreshAttackerGarrisons(List<CapturePoint> activePoints, int sectorIndex)
    {
        if (!enableCapturedPointGarrisons || SquadManager.Instance == null)
        {
            attackerGarrisonByPoint.Clear();
            garrisonSectorIndex = sectorIndex;
            return;
        }

        if (garrisonSectorIndex != sectorIndex)
        {
            attackerGarrisonByPoint.Clear();
            garrisonSectorIndex = sectorIndex;
        }

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, string> assignment in attackerGarrisonByPoint)
        {
            CapturePoint point = FindPointByGarrisonKey(activePoints, sectorIndex, assignment.Key);
            Squad assignedSquad = FindAttackerSquadById(assignment.Value);

            bool pointLost = point == null || point.captureProgress <= garrisonReleaseProgress;
            bool squadUnavailable = assignedSquad == null || assignedSquad.MemberCount <= 0;

            if (pointLost || squadUnavailable)
            {
                keysToRemove.Add(assignment.Key);

                if (logGarrisonChanges)
                {
                    string pointName = point != null ? point.capturePointName : assignment.Key;
                    string squadName = assignedSquad != null ? assignedSquad.DisplayName : assignment.Value;
                    Debug.Log($"Squad AI: Released attacker garrison {squadName} from {pointName}.");
                }
            }
        }

        foreach (string key in keysToRemove)
        {
            attackerGarrisonByPoint.Remove(key);
        }

        HashSet<string> assignedSquadIds = new HashSet<string>(attackerGarrisonByPoint.Values);

        foreach (CapturePoint point in activePoints)
        {
            if (point == null || point.captureProgress < 99.99f) continue;

            string pointKey = BuildGarrisonKey(sectorIndex, point);
            if (attackerGarrisonByPoint.ContainsKey(pointKey)) continue;

            Squad candidate = ChooseGarrisonSquad(point, assignedSquadIds);
            if (candidate == null) continue;

            attackerGarrisonByPoint[pointKey] = candidate.SquadId;
            assignedSquadIds.Add(candidate.SquadId);

            if (logGarrisonChanges)
            {
                Debug.Log($"Squad AI: Attacker {candidate.DisplayName} assigned as GARRISON for {point.capturePointName} ({candidate.MemberCount} members).");
            }
        }
    }

    private Squad ChooseGarrisonSquad(CapturePoint point, HashSet<string> alreadyAssignedSquadIds)
    {
        if (point == null || SquadManager.Instance == null) return null;

        Squad best = null;
        float bestScore = float.NegativeInfinity;

        foreach (Squad squad in SquadManager.Instance.AttackerSquads)
        {
            if (squad == null || squad.MemberCount <= 0) continue;
            if (alreadyAssignedSquadIds.Contains(squad.SquadId)) continue;

            float score = 0f;

            if (squad.StrategicObjective == point.transform)
            {
                score += 1000f;
            }

            if (GetStrengthState(squad) == SquadStrengthState.Healthy)
            {
                score += 200f;
            }

            score += CountAssaultMembers(squad) * 25f;
            score -= Vector3.Distance(GetSquadCentre(squad), point.transform.position);

            if (score > bestScore)
            {
                best = squad;
                bestScore = score;
            }
        }

        return best;
    }

    private int CountAssaultMembers(Squad squad)
    {
        if (squad == null) return 0;

        int count = 0;
        foreach (GameObject member in squad.Members)
        {
            if (member != null && UnitClassIdentity.GetClass(member) == UnitClass.Assault)
            {
                count++;
            }
        }

        return count;
    }

    private CapturePoint GetGarrisonPointForSquad(Squad squad, List<CapturePoint> activePoints, int sectorIndex)
    {
        if (squad == null || squad.Faction != Faction.Attacker) return null;
        if (garrisonSectorIndex != sectorIndex) return null;

        foreach (CapturePoint point in activePoints)
        {
            if (point == null) continue;

            string key = BuildGarrisonKey(sectorIndex, point);
            if (attackerGarrisonByPoint.TryGetValue(key, out string squadId) && squadId == squad.SquadId)
            {
                return point;
            }
        }

        return null;
    }

    private CapturePoint FindPointByGarrisonKey(List<CapturePoint> activePoints, int sectorIndex, string key)
    {
        foreach (CapturePoint point in activePoints)
        {
            if (point != null && BuildGarrisonKey(sectorIndex, point) == key)
            {
                return point;
            }
        }

        return null;
    }

    private Squad FindAttackerSquadById(string squadId)
    {
        if (string.IsNullOrEmpty(squadId) || SquadManager.Instance == null) return null;

        foreach (Squad squad in SquadManager.Instance.AttackerSquads)
        {
            if (squad != null && squad.SquadId == squadId)
            {
                return squad;
            }
        }

        return null;
    }

    private string BuildGarrisonKey(int sectorIndex, CapturePoint point)
    {
        string pointName = point != null && !string.IsNullOrEmpty(point.capturePointName)
            ? point.capturePointName
            : (point != null ? point.name : "Unknown");

        return $"{sectorIndex}:{pointName}";
    }

    private bool RecordAndCheckStrengthChange(Squad squad)
    {
        if (squad == null || string.IsNullOrEmpty(squad.SquadId)) return false;

        SquadStrengthState current = GetStrengthState(squad);
        if (!lastStrengthStates.TryGetValue(squad.SquadId, out SquadStrengthState previous))
        {
            lastStrengthStates[squad.SquadId] = current;
            return false;
        }

        if (previous == current) return false;

        lastStrengthStates[squad.SquadId] = current;
        nextThinkTimes[squad.SquadId] = 0f;
        return true;
    }

    private float GetSwitchThresholdForStrength(Squad squad)
    {
        switch (GetStrengthState(squad))
        {
            case SquadStrengthState.Critical:
                return objectiveSwitchThreshold * 0.25f;
            case SquadStrengthState.Depleted:
                return objectiveSwitchThreshold * 0.6f;
            default:
                return objectiveSwitchThreshold;
        }
    }

    private bool IsThinkDue(Squad squad)
    {
        if (squad == null || string.IsNullOrEmpty(squad.SquadId)) return true;

        if (!nextThinkTimes.TryGetValue(squad.SquadId, out float nextThink))
        {
            return true;
        }

        return Time.time >= nextThink;
    }

    private void ScheduleNextThink(Squad squad)
    {
        if (squad == null || string.IsNullOrEmpty(squad.SquadId)) return;

        float jitter = aiThinkJitter > 0f ? Random.Range(-aiThinkJitter, aiThinkJitter) : 0f;
        float delay = Mathf.Max(0.25f, aiThinkInterval + jitter);
        nextThinkTimes[squad.SquadId] = Time.time + delay;
    }

    private Squad GetSquad(GameObject unit)
    {
        if (unit == null) return null;

        SquadMember member = unit.GetComponent<SquadMember>();
        return member != null ? member.Squad : null;
    }

    private List<CapturePoint> GetActivePoints(Sector sector)
    {
        List<CapturePoint> result = new List<CapturePoint>();

        if (sector == null || sector.capturePoints == null)
        {
            return result;
        }

        foreach (CapturePoint point in sector.capturePoints)
        {
            if (point != null)
            {
                result.Add(point);
            }
        }

        return result;
    }

    private bool IsExistingObjectiveStillValid(Squad squad, List<CapturePoint> activePoints, int sectorIndex, bool isAttacker)
    {
        if (squad.StrategicObjective == null || squad.StrategicObjectiveSectorIndex != sectorIndex)
        {
            return false;
        }

        CapturePoint point = FindCapturePoint(activePoints, squad.StrategicObjective);
        if (point == null)
        {
            return false;
        }

        if (isAttacker)
        {
            switch (squad.Role)
            {
                case SquadRole.Attack:
                case SquadRole.Support:
                    return point.captureProgress < 100f || GetRecoveryState(squad) == SquadRecoveryState.Recovering;

                case SquadRole.Reserve:
                    return point.captureProgress >= 100f || !AnyCapturedPoint(activePoints);

                default:
                    return point.captureProgress < 100f;
            }
        }

        bool threatened = IsThreatened(point);

        switch (squad.Role)
        {
            case SquadRole.Defend:
            case SquadRole.Support:
                return threatened || !AnyThreatenedPoint(activePoints) || GetRecoveryState(squad) == SquadRecoveryState.Recovering;

            case SquadRole.Reserve:
                return !threatened || !AnySecurePoint(activePoints);

            default:
                return true;
        }
    }

    private ObjectiveDecision ChooseBestScoredObjective(Squad squad, List<CapturePoint> activePoints, bool isAttacker)
    {
        ObjectiveDecision best = new ObjectiveDecision(null, float.NegativeInfinity);

        foreach (CapturePoint point in activePoints)
        {
            if (point == null) continue;

            float score = ScoreObjective(squad, point, activePoints, isAttacker, point.transform == squad.StrategicObjective);
            if (score > best.Score)
            {
                best = new ObjectiveDecision(point.transform, score);
            }
        }

        if (best.Target == null && activePoints.Count > 0)
        {
            CapturePoint fallback = PickStable(squad, activePoints);
            return new ObjectiveDecision(fallback != null ? fallback.transform : null, 0f);
        }

        return best;
    }

    private float ScoreObjective(Squad squad, CapturePoint point, List<CapturePoint> activePoints, bool isAttacker, bool isCurrentObjective)
    {
        float score = 0f;
        Vector3 squadCentre = GetSquadCentre(squad);
        float distance = Vector3.Distance(squadCentre, point.transform.position);

        score -= distance * distanceWeight;

        if (isAttacker)
        {
            float captureNeed = Mathf.Clamp(100f - point.captureProgress, 0f, 200f);
            score += captureNeed * captureNeedWeight;
            score += point.attackerCount * friendlyPresenceWeight;
            score += point.defenderCount * enemyPressureWeight;

            switch (squad.Role)
            {
                case SquadRole.Attack:
                    if (point.captureProgress < 100f) score += roleBiasWeight;
                    else score -= roleBiasWeight * 2f;
                    break;

                case SquadRole.Support:
                    if (point.captureProgress < 100f) score += roleBiasWeight * 0.5f;
                    score += point.attackerCount * friendlyPresenceWeight;
                    score += point.defenderCount * enemyPressureWeight;
                    break;

                case SquadRole.Reserve:
                    if (point.captureProgress >= 100f) score += roleBiasWeight;
                    else if (AnyCapturedPoint(activePoints)) score -= roleBiasWeight;
                    break;
            }
        }
        else
        {
            float defenceNeed = Mathf.Clamp(point.captureProgress + 100f, 0f, 200f);
            score += defenceNeed * captureNeedWeight;
            score += point.defenderCount * friendlyPresenceWeight;
            score += point.attackerCount * enemyPressureWeight;

            switch (squad.Role)
            {
                case SquadRole.Defend:
                    if (IsThreatened(point)) score += roleBiasWeight;
                    else score += roleBiasWeight * 0.25f;
                    break;

                case SquadRole.Support:
                    if (IsThreatened(point)) score += roleBiasWeight * 0.75f;
                    score += point.attackerCount * enemyPressureWeight;
                    break;

                case SquadRole.Reserve:
                    if (!IsThreatened(point)) score += roleBiasWeight;
                    else if (AnySecurePoint(activePoints)) score -= roleBiasWeight;
                    break;
            }
        }

        ApplySquadStrengthScoring(ref score, squad, point, isAttacker);
        ApplyRecoveryScoring(ref score, squad, point, isAttacker);

        int squadsAlreadyAssigned = CountOtherSquadsAssignedTo(point.transform, squad);
        score -= squadsAlreadyAssigned * objectiveCrowdingPenalty;

        if (isCurrentObjective)
        {
            score += currentObjectiveBonus;
        }

        score += GetStableTieBreaker(squad, point);
        return score;
    }

    private void ApplySquadStrengthScoring(ref float score, Squad squad, CapturePoint point, bool isAttacker)
    {
        SquadStrengthState strength = GetStrengthState(squad);
        if (strength == SquadStrengthState.Healthy) return;

        int friendlyCount = isAttacker ? point.attackerCount : point.defenderCount;
        int enemyCount = isAttacker ? point.defenderCount : point.attackerCount;

        float enemyPenalty = strength == SquadStrengthState.Critical ? criticalEnemyPenalty : depletedEnemyPenalty;
        float friendlyBonus = strength == SquadStrengthState.Critical ? criticalFriendlySafetyBonus : depletedFriendlySafetyBonus;
        float safeBonus = strength == SquadStrengthState.Critical ? criticalSafeObjectiveBonus : depletedSafeObjectiveBonus;

        score -= enemyCount * enemyPenalty;
        score += friendlyCount * friendlyBonus;

        if (isAttacker)
        {
            if (point.captureProgress >= 100f)
            {
                score += safeBonus;
            }

            float friendlyControl = Mathf.InverseLerp(-100f, 100f, point.captureProgress);
            score += friendlyControl * 100f * reducedSquadControlBias;
        }
        else
        {
            if (!IsThreatened(point))
            {
                score += safeBonus;
            }

            float friendlyControl = Mathf.InverseLerp(100f, -100f, point.captureProgress);
            score += friendlyControl * 100f * reducedSquadControlBias;
        }
    }

    private void ApplyRecoveryScoring(ref float score, Squad squad, CapturePoint point, bool isAttacker)
    {
        if (GetRecoveryState(squad) != SquadRecoveryState.Recovering) return;

        SquadStrengthState strength = GetStrengthState(squad);
        int friendlyCount = isAttacker ? point.attackerCount : point.defenderCount;
        int enemyCount = isAttacker ? point.defenderCount : point.attackerCount;

        bool friendlyControlled = isAttacker
            ? point.captureProgress >= 100f
            : point.captureProgress <= -99.9f && point.attackerCount == 0;

        bool contested = enemyCount > 0 || (isAttacker && point.captureProgress < 100f) || (!isAttacker && IsThreatened(point));

        if (friendlyControlled)
        {
            score += strength == SquadStrengthState.Critical
                ? criticalRecoverySafeBonus
                : depletedRecoverySafeBonus;
        }

        if (contested)
        {
            score -= recoveryContestedPenalty;
        }

        score += friendlyCount * recoveryFriendlyPresenceBonus;

        Transform reinforcementOrigin = GetReinforcementOrigin(isAttacker);
        if (reinforcementOrigin != null)
        {
            float depth = Vector3.Distance(reinforcementOrigin.position, point.transform.position);
            score -= depth * recoveryDepthPenalty;
        }
    }

    private Transform GetReinforcementOrigin(bool isAttacker)
    {
        if (GameManager.Instance == null) return null;

        UnitSpawner spawner = isAttacker ? GameManager.Instance.attackerSpawner : GameManager.Instance.defenderSpawner;
        return spawner != null ? spawner.transform : null;
    }

    private int CountOtherSquadsAssignedTo(Transform target, Squad requestingSquad)
    {
        if (target == null || SquadManager.Instance == null) return 0;

        int count = 0;
        count += CountAssignedInList(SquadManager.Instance.AttackerSquads, target, requestingSquad);
        count += CountAssignedInList(SquadManager.Instance.DefenderSquads, target, requestingSquad);
        return count;
    }

    private int CountAssignedInList(IReadOnlyList<Squad> squads, Transform target, Squad requestingSquad)
    {
        int count = 0;
        if (squads == null) return count;

        foreach (Squad squad in squads)
        {
            if (squad == null || squad == requestingSquad) continue;
            if (squad.StrategicObjective == target) count++;
        }

        return count;
    }

    private Vector3 GetSquadCentre(Squad squad)
    {
        if (squad == null || squad.MemberCount == 0) return Vector3.zero;

        Vector3 total = Vector3.zero;
        int count = 0;

        foreach (GameObject member in squad.Members)
        {
            if (member == null) continue;
            total += member.transform.position;
            count++;
        }

        return count > 0 ? total / count : Vector3.zero;
    }

    private float GetStableTieBreaker(Squad squad, CapturePoint point)
    {
        string key = $"{squad.SquadId}:{point.capturePointName}";
        int hash = GetStablePositiveHash(key);
        return (hash % 100) * 0.001f;
    }

    private CapturePoint PickStable(Squad squad, List<CapturePoint> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;

        int hash = GetStablePositiveHash(squad.SquadId);
        return candidates[hash % candidates.Count];
    }

    private CapturePoint FindCapturePoint(List<CapturePoint> points, Transform target)
    {
        foreach (CapturePoint point in points)
        {
            if (point != null && point.transform == target)
            {
                return point;
            }
        }

        return null;
    }

    private bool IsThreatened(CapturePoint point)
    {
        return point != null && (point.captureProgress > -100f || point.attackerCount > 0);
    }

    private bool AnyThreatenedPoint(List<CapturePoint> points)
    {
        foreach (CapturePoint point in points)
        {
            if (IsThreatened(point)) return true;
        }

        return false;
    }

    private bool AnySecurePoint(List<CapturePoint> points)
    {
        foreach (CapturePoint point in points)
        {
            if (!IsThreatened(point)) return true;
        }

        return false;
    }

    private bool AnyCapturedPoint(List<CapturePoint> points)
    {
        foreach (CapturePoint point in points)
        {
            if (point != null && point.captureProgress >= 100f) return true;
        }

        return false;
    }

    private int GetStablePositiveHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            if (hash == int.MinValue) return int.MaxValue;
            return Mathf.Abs(hash);
        }
    }

    private readonly struct ObjectiveDecision
    {
        public Transform Target { get; }
        public float Score { get; }

        public ObjectiveDecision(Transform target, float score)
        {
            Target = target;
            Score = score;
        }
    }
}
