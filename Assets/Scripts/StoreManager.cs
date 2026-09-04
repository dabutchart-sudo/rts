using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class StoreManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform sidebarContainer;
    public GameObject rowPrefab;
    public GameObject unitButtonPrefab;

    [Header("Spawn Source Readability")]
    [Tooltip("Reserved width for the BASE/A1/A2 source label so it cannot be squeezed out by unit buttons.")]
    public float sourceLabelWidth = 70f;

    [Header("Diagnostics")]
    [Tooltip("Log the resolved spawn sources and purchase catalog whenever the store is rebuilt.")]
    public bool logResolvedStore = true;

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
    private string initializedCatalogSignature = string.Empty;
    private string initializedSourceSignature = string.Empty;

    void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.playerFaction == Faction.None) return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < 0.35f) return;
        refreshTimer = 0f;

        Faction playerFaction = GameManager.Instance.playerFaction;
        string currentCatalogSignature = BuildCatalogSignature(playerFaction);
        string currentSourceSignature = BuildSourceSignature(playerFaction);

        bool needsRebuild = !isInitialized ||
                            initializedSectorIndex != GameManager.Instance.currentSectorIndex ||
                            initializedCatalogSignature != currentCatalogSignature ||
                            initializedSourceSignature != currentSourceSignature;

        if (needsRebuild)
        {
            InitializeStoreUI(currentCatalogSignature, currentSourceSignature);
        }
        else
        {
            RefreshButtonStates();
        }
    }

    private void InitializeStoreUI(string catalogSignature = null, string sourceSignature = null)
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
        initializedCatalogSignature = catalogSignature ?? BuildCatalogSignature(playerFaction);
        initializedSourceSignature = sourceSignature ?? BuildSourceSignature(playerFaction);

        if (logResolvedStore)
        {
            Debug.Log(BuildResolvedStoreLog(playerFaction, sources, catalog));
        }

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

        if (rowText != null)
        {
            rowText.text = source.DisplayName;
            rowText.alignment = TextAlignmentOptions.Center;
            rowText.fontStyle = FontStyles.Bold;

            LayoutElement labelLayout = rowText.GetComponent<LayoutElement>();
            if (labelLayout == null) labelLayout = rowText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = sourceLabelWidth;
            labelLayout.preferredWidth = sourceLabelWidth;
            labelLayout.flexibleWidth = 0f;
        }

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

        Debug.Log($"🪂 PLAYER DEPLOYMENT ({playerFaction}): {unit.unitDisplayName} from {storeButton.source.DisplayName}.");
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

    private string BuildCatalogSignature(Faction faction)
    {
        List<PurchasableUnit> catalog = SpawnSourceResolver.GetCurrentSectorCatalog(faction);
        StringBuilder builder = new StringBuilder();

        foreach (PurchasableUnit unit in catalog)
        {
            if (unit == null) continue;
            builder.Append(unit.unitDisplayName).Append('|')
                   .Append(unit.xpCost).Append('|')
                   .Append(unit.GetResolvedCategory()).Append('|');

            if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass))
            {
                builder.Append(unitClass);
            }

            builder.Append(';');
        }

        return builder.ToString();
    }

    private string BuildSourceSignature(Faction faction)
    {
        List<SpawnSource> sources = SpawnSourceResolver.GetCurrentSectorSources(faction, false);
        StringBuilder builder = new StringBuilder();

        foreach (SpawnSource source in sources)
        {
            if (source == null) continue;
            builder.Append(source.Kind).Append('|')
                   .Append(source.DisplayName).Append('|')
                   .Append(source.SectorIndex).Append(';');
        }

        return builder.ToString();
    }

    private string BuildResolvedStoreLog(Faction faction, List<SpawnSource> sources, List<PurchasableUnit> catalog)
    {
        StringBuilder sourceText = new StringBuilder();
        if (sources != null)
        {
            foreach (SpawnSource source in sources)
            {
                if (source == null) continue;
                if (sourceText.Length > 0) sourceText.Append(", ");
                sourceText.Append(source.DisplayName)
                          .Append(source.IsAvailable() ? "[READY]" : "[LOCKED]");
            }
        }

        StringBuilder catalogText = new StringBuilder();
        if (catalog != null)
        {
            foreach (PurchasableUnit unit in catalog)
            {
                if (unit == null) continue;
                if (catalogText.Length > 0) catalogText.Append(", ");
                catalogText.Append(unit.unitDisplayName).Append('(').Append(unit.xpCost).Append(" XP)");
            }
        }

        return $"🛒 STORE RESOLVED ({faction}) Sector {GameManager.Instance.currentSectorIndex + 1}: Sources = {sourceText}; Catalog = {catalogText}";
    }
}
