using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Adds a first-pass Recon purchase option to capture points at runtime without forcing
/// scene/prefab YAML edits. Recon temporarily reuses the faction Assault prefab as its
/// visual base, then receives explicit Recon class identity and ReconUnitProfile when bought.
/// </summary>
public sealed class ReconPurchasableBootstrap : MonoBehaviour
{
    [Header("Recon Purchase Settings")]
    [Min(0)] public int reconXPCost = 100;
    public string reconDisplayName = "Recon";
    public bool logConfiguration = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeBootstrap()
    {
        ReconPurchasableBootstrap existing = FindAnyObjectByType<ReconPurchasableBootstrap>(FindObjectsInactive.Include);
        if (existing != null) return;

        GameObject host = new GameObject("ReconPurchasableBootstrap");
        host.AddComponent<ReconPurchasableBootstrap>();
    }

    private void Start()
    {
        ConfigureReconPurchases();
    }

    public void ConfigureReconPurchases()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            Debug.LogWarning("Recon bootstrap: GameManager/sectors unavailable; Recon purchasables were not configured.");
            return;
        }

        GameObject attackerTemplate = GameManager.Instance.attackerSpawner != null
            ? GameManager.Instance.attackerSpawner.assaultPrefab
            : null;

        GameObject defenderTemplate = GameManager.Instance.defenderSpawner != null
            ? GameManager.Instance.defenderSpawner.assaultPrefab
            : null;

        int attackerAdds = 0;
        int defenderAdds = 0;

        foreach (Sector sector in GameManager.Instance.sectors)
        {
            if (sector == null || sector.capturePoints == null) continue;

            foreach (CapturePoint point in sector.capturePoints)
            {
                if (point == null) continue;

                if (attackerTemplate != null && !ContainsRecon(point.attackerPurchasables))
                {
                    point.attackerPurchasables = AppendRecon(point.attackerPurchasables, attackerTemplate);
                    attackerAdds++;
                }

                if (defenderTemplate != null && !ContainsRecon(point.defenderPurchasables))
                {
                    point.defenderPurchasables = AppendRecon(point.defenderPurchasables, defenderTemplate);
                    defenderAdds++;
                }
            }
        }

        if (logConfiguration)
        {
            Debug.Log($"🔭 RECON CONFIG: Added Recon purchase option to {attackerAdds} attacker and {defenderAdds} defender capture-point stores. Cost {reconXPCost} XP.");
        }
    }

    private PurchasableUnit[] AppendRecon(PurchasableUnit[] existing, GameObject templatePrefab)
    {
        List<PurchasableUnit> result = new List<PurchasableUnit>();
        if (existing != null)
        {
            foreach (PurchasableUnit unit in existing)
            {
                if (unit != null) result.Add(unit);
            }
        }

        PurchasableUnit recon = new PurchasableUnit
        {
            unitDisplayName = reconDisplayName,
            unitPrefab = templatePrefab,
            xpCost = Mathf.Max(0, reconXPCost),
            unitIcon = null,
            useExplicitClassification = true,
            unitCategory = UnitCategory.Infantry,
            unitClass = UnitClass.Recon
        };

        result.Add(recon);
        return result.ToArray();
    }

    private bool ContainsRecon(PurchasableUnit[] units)
    {
        if (units == null) return false;

        foreach (PurchasableUnit unit in units)
        {
            if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) continue;
            if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass) && unitClass == UnitClass.Recon)
            {
                return true;
            }
        }

        return false;
    }
}
