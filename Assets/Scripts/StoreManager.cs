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
        public SpawnSource source;
    }

    private class StoreRow
    {
        public GameObject rowObject;
        public SpawnSource source;
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

        if (GameManager.Instance == null) return;

        Faction playerFaction = GameManager.Instance.playerFaction;
        List<PurchasableUnit> catalog = SpawnSourceResolver.GetCurrentSectorCatalog(playerFaction);
        List<SpawnSource> sources = SpawnSourceResolver.GetCurrentSectorSources(playerFaction, false);

        foreach (SpawnSource source in sources)
        {
            if (source == null) continue;
            CreateSourceRow(source, catalog);
        }

        isInitialized = true;
        initializedSectorIndex = GameManager.Instance.currentSectorIndex;
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

    private void CreateSourceRow(SpawnSource source, List<PurchasableUnit> catalog)
    {
        if (source == null || sidebarContainer == null || rowPrefab == null) return;

        GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
        TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
        if (rowText != null) rowText.text = source.DisplayName;

        allStoreRows.Add(new StoreRow
        {
            rowObject = newRow,
            source = source
        });

        if (catalog == null) return;

        foreach (PurchasableUnit unit in catalog)
        {
            if (unit == null) continue;
            CreateUnitButton(newRow.transform, unit, source);
        }
    }

    private void CreateUnitButton(Transform rowTransform, PurchasableUnit unit, SpawnSource source)
    {
        if (unitButtonPrefab == null || rowTransform == null) return;

        GameObject btnObj = Instantiate(unitButtonPrefab, rowTransform);
        StoreButton storeBtn = new StoreButton
        {
            button = btnObj.GetComponent<Button>(),
            buttonText = btnObj.GetComponentInChildren<TextMeshProUGUI>(),
            unit = unit,
            source = source
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
            if (row.rowObject == null || row.source == null) continue;
            row.rowObject.SetActive(row.source.IsAvailable());
        }

        foreach (StoreButton sb in allStoreButtons)
        {
            if (sb.button == null || sb.unit == null || sb.source == null) continue;

            bool validSource = sb.source.IsAvailable();
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
        if (GameManager.Instance == null || storeButton == null || storeButton.unit == null || storeButton.source == null) return;

        PurchasableUnit unit = storeButton.unit;
        Faction playerFaction = GameManager.Instance.playerFaction;

        if (!storeButton.source.IsAvailable()) return;

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

        Transform spawnLoc = storeButton.source.GetNextSpawnTransform();
        if (spawnLoc == null || unit.unitPrefab == null) return;

        if (playerFaction == Faction.Attacker) GameManager.Instance.attackerXP -= unit.xpCost;
        else if (playerFaction == Faction.Defender) GameManager.Instance.defenderXP -= unit.xpCost;

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
