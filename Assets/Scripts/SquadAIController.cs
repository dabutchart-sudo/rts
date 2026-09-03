using System.Collections.Generic;
using System.Text;
using UnityEngine;

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

    private readonly Dictionary<string, float> nextThinkTimes = new Dictionary<string, float>();

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

        bool existingObjectiveValid = IsExistingObjectiveStillValid(squad, activePoints, sectorIndex, isAttacker);

        if (persistSquadObjectives && existingObjectiveValid && !IsThinkDue(squad))
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
                if (bestDecision.Score < currentScore + objectiveSwitchThreshold)
                {
                    return previousObjective;
                }
            }
        }

        squad.AssignStrategicObjective(bestDecision.Target, sectorIndex);

        if (logObjectiveChanges && bestDecision.Target != previousObjective)
        {
            string objectiveName = bestDecision.Target != null ? bestDecision.Target.name : "None";
            Debug.Log($"Squad AI: {squad.Faction} {squad.DisplayName} [{squad.Role}] -> {objectiveName} | score {bestDecision.Score:F1}");
        }

        return bestDecision.Target;
    }

    public string GetDiagnosticReport(Squad squad)
    {
        if (squad == null) return "No squad";

        StringBuilder report = new StringBuilder();
        string objectiveName = squad.StrategicObjective != null ? squad.StrategicObjective.name : "None";
        float timeUntilThink = GetTimeUntilNextThink(squad);

        report.Append($"{squad.DisplayName} [{squad.Role}]  Members {squad.MemberCount}  Order {squad.CurrentOrder}/{squad.CurrentCommandSource}");
        report.Append($"\nObjective: {objectiveName}  Next think: {timeUntilThink:0.0}s");

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

        foreach (CapturePoint point in activePoints)
        {
            float score = ScoreObjective(squad, point, activePoints, isAttacker, point.transform == squad.StrategicObjective);
            report.Append($"\n  {point.capturePointName}: {score:0.0}  cap {point.captureProgress:0}%  A{point.attackerCount}/D{point.defenderCount}");
        }

        return report.ToString();
    }

    public float GetTimeUntilNextThink(Squad squad)
    {
        if (squad == null || string.IsNullOrEmpty(squad.SquadId)) return 0f;
        if (!nextThinkTimes.TryGetValue(squad.SquadId, out float nextThink)) return 0f;
        return Mathf.Max(0f, nextThink - Time.time);
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
                    return point.captureProgress < 100f;

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
                return threatened || !AnyThreatenedPoint(activePoints);

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

        int squadsAlreadyAssigned = CountOtherSquadsAssignedTo(point.transform, squad);
        score -= squadsAlreadyAssigned * objectiveCrowdingPenalty;

        if (isCurrentObjective)
        {
            score += currentObjectiveBonus;
        }

        score += GetStableTieBreaker(squad, point);
        return score;
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
