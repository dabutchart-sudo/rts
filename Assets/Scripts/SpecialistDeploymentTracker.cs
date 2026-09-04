using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared authority for specialist deployment limits. Both human store purchases and
/// AI Commander purchases pass through this tracker, so neither side can bypass the cap.
/// Limits are deployments per round, not simultaneous living units.
/// </summary>
public sealed class SpecialistDeploymentTracker : MonoBehaviour
{
    public static SpecialistDeploymentTracker Instance { get; private set; }

    [Header("Per-Faction Round Deployment Limits")]
    [Min(0)] public int engineerLimit = 4;
    [Min(0)] public int reconLimit = 4;
    [Min(0)] public int supportLimit = 4;

    [Tooltip("Write specialist deployment counts to the Console.")]
    public bool logDeployments = true;

    private readonly Dictionary<string, int> deploymentCounts = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeTracker()
    {
        EnsureInstance();
    }

    public static SpecialistDeploymentTracker EnsureInstance()
    {
        if (Instance != null) return Instance;

        SpecialistDeploymentTracker existing = FindAnyObjectByType<SpecialistDeploymentTracker>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("SpecialistDeploymentTracker");
        return host.AddComponent<SpecialistDeploymentTracker>();
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

    public bool CanDeploy(Faction faction, UnitClass unitClass)
    {
        if (unitClass == UnitClass.Assault) return true;

        int limit = GetLimit(unitClass);
        if (limit <= 0) return false;

        return GetDeploymentCount(faction, unitClass) < limit;
    }

    public bool TryRegisterDeployment(Faction faction, UnitClass unitClass)
    {
        if (unitClass == UnitClass.Assault) return true;
        if (!CanDeploy(faction, unitClass)) return false;

        string key = BuildKey(faction, unitClass);
        int newCount = GetDeploymentCount(faction, unitClass) + 1;
        deploymentCounts[key] = newCount;

        if (logDeployments)
        {
            Debug.Log($"🎯 SPECIALIST DEPLOYMENT: {faction} {unitClass} {newCount}/{GetLimit(unitClass)} this round.");
        }

        return true;
    }

    public int GetDeploymentCount(Faction faction, UnitClass unitClass)
    {
        if (unitClass == UnitClass.Assault) return 0;
        return deploymentCounts.TryGetValue(BuildKey(faction, unitClass), out int count) ? count : 0;
    }

    public int GetRemainingDeployments(Faction faction, UnitClass unitClass)
    {
        if (unitClass == UnitClass.Assault) return int.MaxValue;
        return Mathf.Max(0, GetLimit(unitClass) - GetDeploymentCount(faction, unitClass));
    }

    public int GetLimit(UnitClass unitClass)
    {
        switch (unitClass)
        {
            case UnitClass.Engineer:
                return Mathf.Max(0, engineerLimit);
            case UnitClass.Recon:
                return Mathf.Max(0, reconLimit);
            case UnitClass.Support:
                return Mathf.Max(0, supportLimit);
            default:
                return int.MaxValue;
        }
    }

    public void ResetRoundCounts()
    {
        deploymentCounts.Clear();
    }

    private string BuildKey(Faction faction, UnitClass unitClass)
    {
        return $"{faction}:{unitClass}";
    }
}
