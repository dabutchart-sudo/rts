using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StoreManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform sidebarContainer;
    public GameObject rowPrefab;
    public GameObject unitButtonPrefab;

    private class StoreButton
    {
        public Button button;
        public TextMeshProUGUI buttonText;
        public PurchasableUnit unit;
        public CapturePoint captureSource;
        public BaseZone baseSource;
    }

    private class StoreRow
    {
        public GameObject rowObject;
        public CapturePoint captureSource;
        public BaseZone baseSource;
    }

    private readonly List<StoreButton> allStoreButtons = new List<StoreButton>();
    private readonly List<StoreRow> allStoreRows = new List<StoreRow>();
    private float refreshTimer = 0f;
    private bool isInitialized = false;
    private int initializedSectorIndex = -1;

    void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.playerFaction == Faction.None) return;

        if (!isInitialized || initializedSectorIndex != GameManager.Instance.currentSectorIndex)
        {
            InitializeStoreUI();
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 0.35f)
        {
            RefreshButtonStates();
            refreshTimer = 0f;
        }
    }

    private void InitializeStoreUI()
    {
        ClearStoreUI();

        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return;

        Faction playerFaction = GameManager.Instance.playerFaction;
        int sectorIndex = GameManager.Instance.currentSectorIndex;
        if (sectorIndex < 0 || sectorIndex >= GameManager.Instance.sectors.Length) return;

        Sector sector = GameManager.Instance.sectors[sectorIndex];
        if (sector == null) return;

        BaseZone playerBase = playerFaction == Faction.Attacker ? sector.attackerBase : sector.defenderBase;
        List<PurchasableUnit> sectorCatalog = BuildSectorCatalog(sector, playerFaction);

        if (playerBase != null && sectorCatalog.Count > 0)
        {
            CreateBaseRow(playerBase, sectorCatalog);
        }

        if (sector.capturePoints != null)
        {
            foreach (CapturePoint cp in sector.capturePoints)
            {
                if (cp == null) continue;
                CreateCapturePointRow(cp, playerFaction);
            }
        }

        isInitialized = true;
        initializedSectorIndex = sectorIndex;
        RefreshButtonStates();
    }

    private void ClearStoreUI()
    {
        allStoreButtons.Clear();
        allStoreRows.Clear();

        if (sidebarContainer != null)
        {
            for (int i = sidebarContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(sidebarContainer.GetChild(i).gameObject);
            }
        }
    }

    private List<PurchasableUnit> BuildSectorCatalog(Sector sector, Faction faction)
    {
        List<PurchasableUnit> catalog = new List<PurchasableUnit>();
        HashSet<string> seen = new HashSet<string>();

        if (sector == null || sector.capturePoints == null) return catalog;

        foreach (CapturePoint cp in sector.capturePoints)
        {
            if (cp == null) continue;

            PurchasableUnit[] units = faction == Faction.Attacker
                ? cp.attackerPurchasables
                : cp.defenderPurchasables;

            if (units == null) continue;

            foreach (PurchasableUnit unit in units)
            {
                if (unit == null || unit.unitPrefab == null) continue;

                string key = $"{unit.unitDisplayName}|{unit.xpCost}|{unit.GetResolvedCategory()}";
                if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass)) key += $"|{unitClass}";

                if (seen.Add(key)) catalog.Add(unit);
            }
        }

        return catalog;
    }

    private void CreateBaseRow(BaseZone baseZone, List<PurchasableUnit> units)
    {
        GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
        TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
        if (rowText != null) rowText.text = "BASE";

        allStoreRows.Add(new StoreRow
        {
            rowObject = newRow,
            baseSource = baseZone
        });

        foreach (PurchasableUnit unit in units)
        {
            CreateUnitButton(newRow.transform, unit, null, baseZone);
        }
    }

    private void CreateCapturePointRow(CapturePoint cp, Faction faction)
    {
        GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
        TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
        if (rowText != null) rowText.text = cp.capturePointName;

        allStoreRows.Add(new StoreRow
        {
            rowObject = newRow,
            captureSource = cp
        });

        PurchasableUnit[] unitsToDisplay = faction == Faction.Attacker
            ? cp.attackerPurchasables
            : cp.defenderPurchasables;

        if (unitsToDisplay == null) return;

        foreach (PurchasableUnit unit in unitsToDisplay)
        {
            if (unit == null) continue;
            CreateUnitButton(newRow.transform, unit, cp, null);
        }
    }

    private void CreateUnitButton(Transform rowTransform, PurchasableUnit unit, CapturePoint captureSource, BaseZone baseSource)
    {
        GameObject btnObj = Instantiate(unitButtonPrefab, rowTransform);
        StoreButton storeBtn = new StoreButton
        {
            button = btnObj.GetComponent<Button>(),
            buttonText = btnObj.GetComponentInChildren<TextMeshProUGUI>(),
            unit = unit,
            captureSource = captureSource,
            baseSource = baseSource
        };

        if (storeBtn.button != null)
        {
            storeBtn.button.onClick.AddListener(() => PurchaseUnit(storeBtn));
        }

        allStoreButtons.Add(storeBtn);
    }

    private void RefreshButtonStates()
    {
        if (GameManager.Instance == null) return;

        Faction playerFaction = GameManager.Instance.playerFaction;
        int currentXP = playerFaction == Faction.Attacker
            ? GameManager.Instance.attackerXP
            : GameManager.Instance.defenderXP;

        foreach (StoreRow row in allStoreRows)
        {
            if (row.rowObject == null) continue;

            bool visible = row.baseSource != null
                ? row.baseSource.IsCurrentBaseFor(playerFaction)
                : row.captureSource != null && row.captureSource.IsControlledBy(playerFaction);

            row.rowObject.SetActive(visible);
        }

        foreach (StoreButton sb in allStoreButtons)
        {
            if (sb.button == null || sb.unit == null) continue;

            bool validSource = sb.baseSource != null
                ? sb.baseSource.IsCurrentBaseFor(playerFaction)
                : sb.captureSource != null && sb.captureSource.IsControlledBy(playerFaction);

            bool canAfford = currentXP >= sb.unit.xpCost;
            bool withinDeploymentLimit = IsWithinSpecialistDeploymentLimit(sb.unit, playerFaction);
            sb.button.interactable = validSource && canAfford && withinDeploymentLimit;

            if (sb.buttonText != null)
            {
                sb.buttonText.text = BuildButtonText(sb.unit, playerFaction, canAfford, withinDeploymentLimit);
            }
        }
    }

    private string BuildButtonText(PurchasableUnit unit, Faction faction, bool canAfford, bool withinDeploymentLimit)
    {
        string text = $"{unit.unitDisplayName}\n{unit.xpCost} XP";

        if (unit.GetResolvedCategory() == UnitCategory.Infantry &&
            unit.TryGetResolvedInfantryClass(out UnitClass unitClass) &&
            unitClass != UnitClass.Assault)
        {
            SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
            if (tracker != null)
            {
                int used = tracker.GetDeploymentCount(faction, unitClass);
                int limit = tracker.GetLimit(unitClass);
                text += $"\n{used}/{limit} DEPLOYED";
            }
        }

        if (!withinDeploymentLimit) text += "\nLIMIT REACHED";
        else if (!canAfford) text += "\nNEED XP";

        return text;
    }

    private bool IsWithinSpecialistDeploymentLimit(PurchasableUnit unit, Faction faction)
    {
        if (unit == null || unit.GetResolvedCategory() != UnitCategory.Infantry) return true;
        if (!unit.TryGetResolvedInfantryClass(out UnitClass unitClass)) return true;
        if (unitClass == UnitClass.Assault) return true;

        SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
        return tracker == null || tracker.CanDeploy(faction, unitClass);
    }

    private void PurchaseUnit(StoreButton storeButton)
    {
        if (GameManager.Instance == null || storeButton == null || storeButton.unit == null) return;

        PurchasableUnit unit = storeButton.unit;
        Faction playerFaction = GameManager.Instance.playerFaction;

        bool validSource = storeButton.baseSource != null
            ? storeButton.baseSource.IsCurrentBaseFor(playerFaction)
            : storeButton.captureSource != null && storeButton.captureSource.IsControlledBy(playerFaction);

        if (!validSource) return;

        int currentXP = playerFaction == Faction.Attacker
            ? GameManager.Instance.attackerXP
            : GameManager.Instance.defenderXP;

        if (currentXP < unit.xpCost) return;

        UnitClass resolvedClass = UnitClass.Assault;
        bool isSpecialist = unit.GetResolvedCategory() == UnitCategory.Infantry &&
                            unit.TryGetResolvedInfantryClass(out resolvedClass) &&
                            resolvedClass != UnitClass.Assault;

        if (isSpecialist)
        {
            SpecialistDeploymentTracker tracker = SpecialistDeploymentTracker.EnsureInstance();
            if (tracker != null && !tracker.TryRegisterDeployment(playerFaction, resolvedClass))
            {
                RefreshButtonStates();
                return;
            }
        }

        if (playerFaction == Faction.Attacker) GameManager.Instance.attackerXP -= unit.xpCost;
        else if (playerFaction == Faction.Defender) GameManager.Instance.defenderXP -= unit.xpCost;

        Transform spawnLoc = storeButton.baseSource != null
            ? storeButton.baseSource.GetSpawnPoint()
            : storeButton.captureSource.GetNextAvailableSpawnPoint();

        if (spawnLoc == null || unit.unitPrefab == null) return;

        GameObject spawnedUnit = Instantiate(unit.unitPrefab, spawnLoc.position, spawnLoc.rotation);
        RegisterPurchasedUnit(spawnedUnit, unit);
        RefreshButtonStates();
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
