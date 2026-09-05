using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a Support specialist purchase option at runtime. Support temporarily reuses the
/// faction Assault prefab as its visual base and receives Support class identity on spawn.
/// </summary>
public sealed class SupportPurchasableBootstrap : MonoBehaviour
{
    [Header("Support Purchase Settings")]
    [Min(0)] public int supportXPCost = 100;
    public string supportDisplayName = "Support";
    public bool logConfiguration = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeBootstrap()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;
        if (scene.name != MapSelection.OriginalMapScene && scene.name != MapSelection.ChatGPTMapScene) return;

        SupportPurchasableBootstrap existing = FindAnyObjectByType<SupportPurchasableBootstrap>(FindObjectsInactive.Include);
        if (existing != null) return;

        GameObject host = new GameObject("SupportPurchasableBootstrap");
        host.AddComponent<SupportPurchasableBootstrap>();
    }

    private void Start()
    {
        ConfigureSupportPurchases();
    }

    public void ConfigureSupportPurchases()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            Debug.LogWarning("Support bootstrap: GameManager/sectors unavailable; Support purchasables were not configured.");
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

                if (attackerTemplate != null && !ContainsSupport(point.attackerPurchasables))
                {
                    point.attackerPurchasables = AppendSupport(point.attackerPurchasables, attackerTemplate);
                    attackerAdds++;
                }

                if (defenderTemplate != null && !ContainsSupport(point.defenderPurchasables))
                {
                    point.defenderPurchasables = AppendSupport(point.defenderPurchasables, defenderTemplate);
                    defenderAdds++;
                }
            }
        }

        if (logConfiguration)
        {
            Debug.Log($"🩹 SUPPORT CONFIG: Added Support purchase option to {attackerAdds} attacker and {defenderAdds} defender catalogs. Cost {supportXPCost} XP.");
        }
    }

    private PurchasableUnit[] AppendSupport(PurchasableUnit[] existing, GameObject templatePrefab)
    {
        List<PurchasableUnit> result = new List<PurchasableUnit>();
        if (existing != null)
        {
            foreach (PurchasableUnit unit in existing)
            {
                if (unit != null) result.Add(unit);
            }
        }

        result.Add(new PurchasableUnit
        {
            unitDisplayName = supportDisplayName,
            unitPrefab = templatePrefab,
            xpCost = Mathf.Max(0, supportXPCost),
            unitIcon = null,
            useExplicitClassification = true,
            unitCategory = UnitCategory.Infantry,
            unitClass = UnitClass.Support
        });

        return result.ToArray();
    }

    private bool ContainsSupport(PurchasableUnit[] units)
    {
        if (units == null) return false;

        foreach (PurchasableUnit unit in units)
        {
            if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) continue;
            if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass) && unitClass == UnitClass.Support)
            {
                return true;
            }
        }

        return false;
    }
}
