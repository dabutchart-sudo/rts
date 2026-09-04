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
        public CapturePoint sourceBase;
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
        Sector currentSector = GameManager.Instance.sectors[GameManager.Instance.currentSectorIndex];
        List<PurchaseOption> affordableOptions = new List<PurchaseOption>();

        foreach (CapturePoint cp in currentSector.capturePoints)
        {
            if (cp == null || !cp.IsControlledBy(aiFaction)) continue;

            PurchasableUnit[] availableUnits = cp.GetAvailableUnits(aiFaction);
            foreach (PurchasableUnit unit in availableUnits)
            {
                if (unit == null || aiCommandXP < unit.xpCost) continue;
                if (!IsPurchaseCategoryAllowed(unit)) continue;
                if (!IsWithinSpecialistDeploymentLimit(unit)) continue;

                affordableOptions.Add(new PurchaseOption { unit = unit, sourceBase = cp });
            }
        }

        if (affordableOptions.Count == 0) return;

        int randomIndex = Random.Range(0, affordableOptions.Count);
        PurchaseOption chosenOption = affordableOptions[randomIndex];
        ExecutePurchase(chosenOption.unit, chosenOption.sourceBase);
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

    private void ExecutePurchase(PurchasableUnit unit, CapturePoint sourceBase)
    {
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

        aiCommandXP -= unit.xpCost;
        Transform spawnLocation = sourceBase.GetNextAvailableSpawnPoint();
        GameObject spawnedUnit = Instantiate(unit.unitPrefab, spawnLocation.position, spawnLocation.rotation);
        RegisterPurchasedUnit(spawnedUnit, unit);

        Debug.Log($"🤖 AI COMMANDER ({aiFaction}): Deployed {unit.unitDisplayName} at {sourceBase.capturePointName}! Remaining XP: {aiCommandXP}");
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
