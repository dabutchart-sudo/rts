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

        // Wait until the player actually chooses a faction before building the store!
        if (!isInitialized && GameManager.Instance.playerFaction != Faction.None)
        {
            InitializeStoreUI();
            isInitialized = true;
        }

        // Only run the refresh loop if the store has actually been built
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

        // Generate a row for every capture point across all sectors
        foreach (Sector sector in GameManager.Instance.sectors)
        {
            foreach (CapturePoint cp in sector.capturePoints)
            {
                if (cp == null) continue;

                GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
                Debug.Log($"🛠️ UI CHECK: Created row for {cp.capturePointName}"); 
                
                TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
                if (rowText != null) rowText.text = cp.capturePointName;

                PurchasableUnit[] unitsToDisplay = (playerFaction == Faction.Attacker) ? cp.attackerPurchasables : cp.defenderPurchasables;

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
        
        // Check the correct XP pool based on the player's faction
        int currentXP = (playerFaction == Faction.Attacker) ? GameManager.Instance.attackerXP : GameManager.Instance.defenderXP;

        foreach (StoreButton sb in allStoreButtons)
        {
            bool canAfford = currentXP >= sb.unit.xpCost;
            bool ownsBase = sb.sourceBase.IsControlledBy(playerFaction);
            
            // Button is ONLY interactable if you have the XP AND currently own the base
            sb.button.interactable = (canAfford && ownsBase);
        }
    }

    private void PurchaseUnit(PurchasableUnit unit, CapturePoint sourceBase)
    {
        if (GameManager.Instance == null) return;
        Faction playerFaction = GameManager.Instance.playerFaction;

        // Get the current XP pool
        int currentXP = (playerFaction == Faction.Attacker) ? GameManager.Instance.attackerXP : GameManager.Instance.defenderXP;

        if (currentXP >= unit.xpCost && sourceBase.IsControlledBy(playerFaction))
        {
            // Deduct the cost from the correct faction's pool
            if (playerFaction == Faction.Attacker)
            {
                GameManager.Instance.attackerXP -= unit.xpCost;
            }
            else if (playerFaction == Faction.Defender)
            {
                GameManager.Instance.defenderXP -= unit.xpCost;
            }
            
            Transform spawnLoc = sourceBase.GetNextAvailableSpawnPoint();
            Instantiate(unit.unitPrefab, spawnLoc.position, spawnLoc.rotation);
            
            RefreshButtonStates(); 
        }
    }
}