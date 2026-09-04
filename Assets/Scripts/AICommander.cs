using UnityEngine;
using System.Collections.Generic;

public class AICommander : MonoBehaviour
{
    [Header("Commander Identity")]
    public Faction aiFaction = Faction.Defender;

    [Header("Economy (Passive Drip)")]
    public int aiCommandXP = 0;
    public int xpPerTick = 10;
    public float xpTickInterval = 3f;

    [Header("Decision Making")]
    [Tooltip("How often the AI checks the store to make purchases")]
    public float decisionInterval = 4f;

    [Header("Specialist Composition")]
    [Tooltip("When enabled, the AI strongly prioritises deploying one of each available specialist class before returning to normal purchases.")]
    public bool prioritiseInitialSpecialistMix = true;

    [Tooltip("Write specialist-priority decisions to the Console.")]
    public bool logSpecialistPriorities = true;

    [Header("Purchase Categories")]
    [Tooltip("Vehicles are not part of the current infantry-only prototype. Leave disabled until vehicle gameplay is intentionally introduced.")]
    public bool allowVehiclePurchases = false;

    [Tooltip("Allow purchasable units that cannot be resolved as Infantry or Vehicle. Normally keep disabled so classification mistakes are visible rather than silently spawned.")]
    public bool allowOtherPurchases = false;

    private float xpTimer = 0f;
    private float decisionTimer = 0f;

    private struct PurchaseOption
    {
        public PurchasableUnit unit;
        public SpawnSource source;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isTransitioningSector) return;
        if (GameManager.Instance.playerFaction == Faction.None) return;

        if (!GameManager.Instance.enableAutoTestMode && aiFaction == GameManager.Instance.playerFaction)
        {
            return;
        }

        xpTimer += Time.deltaTime;
        if (xpTimer >= xpTickInterval)
        {
            aiCommandXP += xpPerTick;
            xpTimer = 0f;
        }

        decisionTimer += Time.deltaTime;
        if (decisionTimer >= decisionInterval)
        {
            MakeTacticalPurchase();
            decisionTimer = 0f;
        }
    }

    private void MakeTacticalPurchase()
    {
        List<PurchaseOption> affordableOptions = BuildAffordableOptions();
        if (affordableOptions.Count == 0) return;

        if (prioritiseInitialSpecialistMix && TryChooseMissingSpecialist(affordableOptions, out PurchaseOption specialistChoice))
        {
            ExecutePurchase(specialistChoice);
            return;
        }

        int randomIndex = Random.Range(0, affordableOptions.Count);
        ExecutePurchase(affordableOptions[randomIndex]);
    }

    private List<PurchaseOption> BuildAffordableOptions()
    {
        List<PurchaseOption> affordableOptions = new List<PurchaseOption>();
        List<PurchasableUnit> catalog = SpawnSourceResolver.GetCurrentSectorCatalog(aiFaction);
        List<SpawnSource> availableSources = SpawnSourceResolver.GetCurrentSectorSources(aiFaction, true);

        if (catalog.Count == 0 || availableSources.Count == 0)
        {
            return affordableOptions;
        }

        SpawnSource preferredSource = SpawnSourceResolver.ChooseBestAISource(aiFaction, availableSources);
        if (preferredSource == null) return affordableOptions;

        foreach (PurchasableUnit unit in catalog)
        {
            if (unit == null || unit.unitPrefab == null) continue;
            if (aiCommandXP < unit.xpCost) continue;
            if (!IsPurchaseCategoryAllowed(unit)) continue;
            if (!IsWithinSpecialistDeploymentLimit(unit)) continue;

            affordableOptions.Add(new PurchaseOption
            {
                unit = unit,
                source = preferredSource
            });
        }

        return affordableOptions;
    }

    private bool TryChooseMissingSpecialist(List<PurchaseOption> options, out PurchaseOption chosen)
    {
        chosen = default;

        SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
        if (tracker == null) return false;

        List<PurchaseOption> missingSpecialists = new List<PurchaseOption>();

        foreach (PurchaseOption option in options)
        {
            PurchasableUnit unit = option.unit;
            if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) continue;
            if (!unit.TryGetResolvedInfantryClass(out UnitClass unitClass)) continue;
            if (unitClass == UnitClass.Assault) continue;

            if (tracker.GetDeploymentCount(aiFaction, unitClass) == 0)
            {
                missingSpecialists.Add(option);
            }
        }

        if (missingSpecialists.Count == 0) return false;

        int bestCost = int.MaxValue;
        List<PurchaseOption> cheapestMissing = new List<PurchaseOption>();

        foreach (PurchaseOption option in missingSpecialists)
        {
            int cost = option.unit != null ? option.unit.xpCost : int.MaxValue;

            if (cost < bestCost)
            {
                bestCost = cost;
                cheapestMissing.Clear();
                cheapestMissing.Add(option);
            }
            else if (cost == bestCost)
            {
                cheapestMissing.Add(option);
            }
        }

        if (cheapestMissing.Count == 0) return false;

        chosen = cheapestMissing[Random.Range(0, cheapestMissing.Count)];

        if (logSpecialistPriorities && chosen.unit != null &&
            chosen.unit.TryGetResolvedInfantryClass(out UnitClass chosenClass))
        {
            Debug.Log($"🤖 AI COMMANDER ({aiFaction}): Prioritising first {chosenClass} deployment while specialist mix is incomplete.");
        }

        return true;
    }

    private bool IsPurchaseCategoryAllowed(PurchasableUnit unit)
    {
        if (unit == null) return false;

        UnitCategory category = unit.GetResolvedCategory();

        if (category == UnitCategory.Infantry)
        {
            return true;
        }

        if (category == UnitCategory.Vehicle)
        {
            return allowVehiclePurchases;
        }

        return allowOtherPurchases;
    }

    private bool IsWithinSpecialistDeploymentLimit(PurchasableUnit unit)
    {
        if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) return true;
        if (!unit.TryGetResolvedInfantryClass(out UnitClass unitClass)) return true;
        if (unitClass == UnitClass.Assault) return true;

        SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
        return tracker == null || tracker.CanDeploy(aiFaction, unitClass);
    }

    private void ExecutePurchase(PurchaseOption option)
    {
        PurchasableUnit unit = option.unit;
        SpawnSource source = option.source;

        if (unit == null || source == null || unit.unitPrefab == null || !source.IsAvailable()) return;

        UnitClass resolvedClass = UnitClass.Assault;
        bool isSpecialist = unit.GetResolvedCategory() == UnitCategory.Infantry &&
                            unit.TryGetResolvedInfantryClass(out resolvedClass) &&
                            resolvedClass != UnitClass.Assault;

        if (isSpecialist)
        {
            SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
            if (tracker != null && !tracker.TryRegisterDeployment(aiFaction, resolvedClass))
            {
                return;
            }
        }

        Transform spawnLocation = source.GetNextSpawnTransform();
        if (spawnLocation == null) return;

        aiCommandXP -= unit.xpCost;
        GameObject spawnedUnit = Instantiate(unit.unitPrefab, spawnLocation.position, spawnLocation.rotation);
        RegisterPurchasedUnit(spawnedUnit, unit);

        Debug.Log($"🤖 AI COMMANDER ({aiFaction}): Deployed {unit.unitDisplayName} at {source.DisplayName}! Remaining XP: {aiCommandXP}");
    }

    private void RegisterPurchasedUnit(GameObject spawnedUnit, PurchasableUnit unit)
    {
        if (spawnedUnit == null || unit == null) return;

        UnitCategory category = unit.GetResolvedCategory();
        UnitCategoryIdentity.Ensure(spawnedUnit, category);

        if (category != UnitCategory.Infantry) return;
        if (!unit.TryGetResolvedInfantryClass(out UnitClass unitClass)) return;

        UnitClassIdentity.Ensure(spawnedUnit, unitClass);
        ReconUnitProfile.ApplyIfRecon(spawnedUnit);

        if (unitClass == UnitClass.Assault) return;

        SquadManager squadManager = SquadManager.EnsureInstance();
        if (squadManager != null)
        {
            squadManager.RegisterSpecialistUnit(spawnedUnit, unitClass);
        }
    }
}
