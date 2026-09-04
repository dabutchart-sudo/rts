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
        public PurchasableUnit unit;
        public CapturePoint sourceBase;
    }

    private List<StoreButton> allStoreButtons = new List<StoreButton>();
    private float refreshTimer = 0f;
    private bool isInitialized = false;

    void Update()
    {
        if (GameManager.Instance == null) return;

        if (!isInitialized && GameManager.Instance.playerFaction != Faction.None)
        {
            InitializeStoreUI();
            isInitialized = true;
        }

        if (isInitialized)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 0.5f)
            {
                RefreshButtonStates();
                refreshTimer = 0f;
            }
        }
    }

    private void InitializeStoreUI()
    {
        if (GameManager.Instance == null) return;
        Faction playerFaction = GameManager.Instance.playerFaction;

        foreach (Sector sector in GameManager.Instance.sectors)
        {
            foreach (CapturePoint cp in sector.capturePoints)
            {
                if (cp == null) continue;

                GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
                Debug.Log($"🛠️ UI CHECK: Created row for {cp.capturePointName}");

                TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
                if (rowText != null) rowText.text = cp.capturePointName;

                PurchasableUnit[] unitsToDisplay = playerFaction == Faction.Attacker
                    ? cp.attackerPurchasables
                    : cp.defenderPurchasables;

                foreach (PurchasableUnit unit in unitsToDisplay)
                {
                    GameObject btnObj = Instantiate(unitButtonPrefab, newRow.transform);

                    StoreButton storeBtn = new StoreButton
                    {
                        button = btnObj.GetComponent<Button>(),
                        unit = unit,
                        sourceBase = cp
                    };

                    TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null) btnText.text = $"{unit.unitDisplayName}\n{unit.xpCost} XP";

                    storeBtn.button.onClick.AddListener(() => PurchaseUnit(storeBtn.unit, storeBtn.sourceBase));
                    allStoreButtons.Add(storeBtn);
                }
            }
        }
    }

    private void RefreshButtonStates()
    {
        if (GameManager.Instance == null) return;

        Faction playerFaction = GameManager.Instance.playerFaction;
        int currentXP = playerFaction == Faction.Attacker
            ? GameManager.Instance.attackerXP
            : GameManager.Instance.defenderXP;

        foreach (StoreButton sb in allStoreButtons)
        {
            bool canAfford = currentXP >= sb.unit.xpCost;
            bool ownsBase = sb.sourceBase.IsControlledBy(playerFaction);
            sb.button.interactable = canAfford && ownsBase;
        }
    }

    private void PurchaseUnit(PurchasableUnit unit, CapturePoint sourceBase)
    {
        if (GameManager.Instance == null || unit == null || sourceBase == null) return;

        Faction playerFaction = GameManager.Instance.playerFaction;
        int currentXP = playerFaction == Faction.Attacker
            ? GameManager.Instance.attackerXP
            : GameManager.Instance.defenderXP;

        if (currentXP < unit.xpCost || !sourceBase.IsControlledBy(playerFaction)) return;

        if (playerFaction == Faction.Attacker)
        {
            GameManager.Instance.attackerXP -= unit.xpCost;
        }
        else if (playerFaction == Faction.Defender)
        {
            GameManager.Instance.defenderXP -= unit.xpCost;
        }

        Transform spawnLoc = sourceBase.GetNextAvailableSpawnPoint();
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

        if (unitClass == UnitClass.Assault) return;

        SquadManager squadManager = SquadManager.EnsureInstance();
        if (squadManager != null)
        {
            squadManager.RegisterSpecialistUnit(spawnedUnit, unitClass);
        }
    }
}
