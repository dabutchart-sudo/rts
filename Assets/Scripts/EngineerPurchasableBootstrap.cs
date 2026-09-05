using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures Engineer is available in the runtime specialist catalog for both factions.
/// Prefers an existing Engineer purchasable prefab if one is already configured anywhere
/// in the scene; otherwise temporarily falls back to the faction Assault prefab.
/// </summary>
public sealed class EngineerPurchasableBootstrap : MonoBehaviour
{
    [Header("Engineer Purchase Settings")]
    [Min(0)] public int engineerXPCost = 100;
    public string engineerDisplayName = "Engineer";
    public bool logConfiguration = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeBootstrap()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;
        if (scene.name != MapSelection.OriginalMapScene && scene.name != MapSelection.ChatGPTMapScene) return;

        EngineerPurchasableBootstrap existing = FindAnyObjectByType<EngineerPurchasableBootstrap>(FindObjectsInactive.Include);
        if (existing != null) return;

        GameObject host = new GameObject("EngineerPurchasableBootstrap");
        host.AddComponent<EngineerPurchasableBootstrap>();
    }

    private void Start()
    {
        ConfigureEngineerPurchases();
    }

    public void ConfigureEngineerPurchases()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            Debug.LogWarning("Engineer bootstrap: GameManager/sectors unavailable; Engineer purchasables were not configured.");
            return;
        }

        GameObject attackerTemplate = FindExistingEngineerTemplate(Faction.Attacker);
        GameObject defenderTemplate = FindExistingEngineerTemplate(Faction.Defender);

        if (attackerTemplate == null && GameManager.Instance.attackerSpawner != null)
        {
            attackerTemplate = GameManager.Instance.attackerSpawner.assaultPrefab;
        }

        if (defenderTemplate == null && GameManager.Instance.defenderSpawner != null)
        {
            defenderTemplate = GameManager.Instance.defenderSpawner.assaultPrefab;
        }

        int attackerAdds = 0;
        int defenderAdds = 0;

        foreach (Sector sector in GameManager.Instance.sectors)
        {
            if (sector == null || sector.capturePoints == null) continue;

            foreach (CapturePoint point in sector.capturePoints)
            {
                if (point == null) continue;

                if (attackerTemplate != null && !ContainsEngineer(point.attackerPurchasables))
                {
                    point.attackerPurchasables = AppendEngineer(point.attackerPurchasables, attackerTemplate);
                    attackerAdds++;
                }

                if (defenderTemplate != null && !ContainsEngineer(point.defenderPurchasables))
                {
                    point.defenderPurchasables = AppendEngineer(point.defenderPurchasables, defenderTemplate);
                    defenderAdds++;
                }
            }
        }

        if (logConfiguration)
        {
            Debug.Log($"ENGINEER CONFIG: Added Engineer purchase option to {attackerAdds} attacker and {defenderAdds} defender catalogs. Cost {engineerXPCost} XP.");
        }
    }

    private GameObject FindExistingEngineerTemplate(Faction faction)
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return null;

        foreach (Sector sector in GameManager.Instance.sectors)
        {
            if (sector == null || sector.capturePoints == null) continue;

            foreach (CapturePoint point in sector.capturePoints)
            {
                if (point == null) continue;

                PurchasableUnit[] units = faction == Faction.Attacker
                    ? point.attackerPurchasables
                    : point.defenderPurchasables;

                if (units == null) continue;

                foreach (PurchasableUnit unit in units)
                {
                    if (unit == null || unit.unitPrefab == null) continue;
                    if (unit.GetResolvedCategory() != UnitCategory.Infantry) continue;

                    if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass) && unitClass == UnitClass.Engineer)
                    {
                        return unit.unitPrefab;
                    }
                }
            }
        }

        return null;
    }

    private PurchasableUnit[] AppendEngineer(PurchasableUnit[] existing, GameObject templatePrefab)
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
            unitDisplayName = engineerDisplayName,
            unitPrefab = templatePrefab,
            xpCost = Mathf.Max(0, engineerXPCost),
            unitIcon = null,
            useExplicitClassification = true,
            unitCategory = UnitCategory.Infantry,
            unitClass = UnitClass.Engineer
        });

        return result.ToArray();
    }

    private bool ContainsEngineer(PurchasableUnit[] units)
    {
        if (units == null) return false;

        foreach (PurchasableUnit unit in units)
        {
            if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) continue;
            if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass) && unitClass == UnitClass.Engineer)
            {
                return true;
            }
        }

        return false;
    }
}
