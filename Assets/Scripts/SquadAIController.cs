using System.Collections.Generic;
using UnityEngine;

public sealed class SquadAIController : MonoBehaviour
{
    public static SquadAIController Instance { get; private set; }

    [Header("Objective Persistence")]
    [SerializeField] private bool persistSquadObjectives = true;
    [SerializeField] private bool logObjectiveChanges = true;

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

        if (persistSquadObjectives && IsExistingObjectiveStillValid(squad, activePoints, sectorIndex, isAttacker))
        {
            return squad.StrategicObjective;
        }

        Transform newObjective = ChooseObjective(squad, activePoints, isAttacker);
        Transform previousObjective = squad.StrategicObjective;
        squad.AssignStrategicObjective(newObjective, sectorIndex);

        if (logObjectiveChanges && newObjective != previousObjective)
        {
            string objectiveName = newObjective != null ? newObjective.name : "None";
            Debug.Log($"Squad AI: {squad.Faction} {squad.DisplayName} [{squad.Role}] -> {objectiveName}");
        }

        return newObjective;
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

    private Transform ChooseObjective(Squad squad, List<CapturePoint> activePoints, bool isAttacker)
    {
        if (activePoints.Count == 1)
        {
            return activePoints[0].transform;
        }

        return isAttacker
            ? ChooseAttackerObjective(squad, activePoints)
            : ChooseDefenderObjective(squad, activePoints);
    }

    private Transform ChooseAttackerObjective(Squad squad, List<CapturePoint> activePoints)
    {
        List<CapturePoint> uncaptured = new List<CapturePoint>();
        List<CapturePoint> captured = new List<CapturePoint>();

        foreach (CapturePoint point in activePoints)
        {
            if (point.captureProgress >= 100f) captured.Add(point);
            else uncaptured.Add(point);
        }

        if (squad.Role == SquadRole.Reserve && captured.Count > 0)
        {
            return PickStable(squad, captured).transform;
        }

        if (squad.Role == SquadRole.Support && uncaptured.Count > 0)
        {
            CapturePoint mostSupportedPush = null;
            int bestFriendlyCount = int.MinValue;

            foreach (CapturePoint point in uncaptured)
            {
                if (point.attackerCount > bestFriendlyCount)
                {
                    bestFriendlyCount = point.attackerCount;
                    mostSupportedPush = point;
                }
            }

            if (mostSupportedPush != null && bestFriendlyCount > 0)
            {
                return mostSupportedPush.transform;
            }
        }

        if (uncaptured.Count > 0)
        {
            return PickStable(squad, uncaptured).transform;
        }

        return PickStable(squad, activePoints).transform;
    }

    private Transform ChooseDefenderObjective(Squad squad, List<CapturePoint> activePoints)
    {
        List<CapturePoint> threatened = new List<CapturePoint>();
        List<CapturePoint> secure = new List<CapturePoint>();

        foreach (CapturePoint point in activePoints)
        {
            if (IsThreatened(point)) threatened.Add(point);
            else secure.Add(point);
        }

        if (squad.Role == SquadRole.Support && threatened.Count > 0)
        {
            CapturePoint highestPressure = null;
            int highestAttackers = int.MinValue;

            foreach (CapturePoint point in threatened)
            {
                if (point.attackerCount > highestAttackers)
                {
                    highestAttackers = point.attackerCount;
                    highestPressure = point;
                }
            }

            if (highestPressure != null)
            {
                return highestPressure.transform;
            }
        }

        if (squad.Role == SquadRole.Reserve && secure.Count > 0)
        {
            return PickStable(squad, secure).transform;
        }

        if (threatened.Count > 0)
        {
            return PickStable(squad, threatened).transform;
        }

        if (secure.Count > 0)
        {
            return PickStable(squad, secure).transform;
        }

        return PickStable(squad, activePoints).transform;
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
}
