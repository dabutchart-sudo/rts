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
            PresentMatchStore();
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

    public static void ShowForCurrentMatch()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None) return;
        StoreManager store = FindAnyObjectByType<StoreManager>(FindObjectsInactive.Include);
        if (store == null)
        {
            GameObject host = new GameObject("StoreManager");
            store = host.AddComponent<StoreManager>();
        }

        store.PresentMatchStore();
    }

    public static void Hide()
    {
        StoreManager store = FindAnyObjectByType<StoreManager>(FindObjectsInactive.Include);
        if (store == null) return;
        store.isInitialized = false;
        store.allStoreButtons.Clear();
        if (store.sidebarContainer != null) store.sidebarContainer.gameObject.SetActive(false);
    }

    void PresentMatchStore()
    {
        EnsureSidebar();
        EnsurePrefabs();
        if (sidebarContainer == null || rowPrefab == null || unitButtonPrefab == null)
        {
            Debug.LogError("The original unit store could not be built. The row or button prefab is missing.");
            return;
        }

        sidebarContainer.gameObject.SetActive(true);
        sidebarContainer.SetAsLastSibling();
        for (int i = sidebarContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(sidebarContainer.GetChild(i).gameObject);
        }

        allStoreButtons.Clear();
        StockEmptyCatalogs();
        InitializeStoreUI();
        isInitialized = true;
        RefreshButtonStates();
    }

    void EnsureSidebar()
    {
        if (sidebarContainer != null) return;

        Canvas canvas = null;
        TextMeshProUGUI[] labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == "TicketText")
            {
                canvas = labels[i].canvas;
                break;
            }
        }

        if (canvas == null) return;

        Transform existing = canvas.transform.Find("Sidebar");
        if (existing != null)
        {
            sidebarContainer = existing;
            ApplyOriginalSidebarLayout(existing as RectTransform, existing.GetComponent<Image>());
            return;
        }

        GameObject sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        sidebar.transform.SetParent(canvas.transform, false);
        Image image = sidebar.GetComponent<Image>();
        RectTransform rect = sidebar.GetComponent<RectTransform>();
        ApplyOriginalSidebarLayout(rect, image);
        VerticalLayoutGroup layout = sidebar.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        sidebarContainer = sidebar.transform;
    }

    static void ApplyOriginalSidebarLayout(RectTransform rect, Image image)
    {
        if (image != null) image.color = new Color(0.3f, 0.3f, 0.3f, 0.18f);
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(20f, 0f);
        rect.sizeDelta = new Vector2(450f, 500f);
    }

    void EnsurePrefabs()
    {
        if (rowPrefab == null) rowPrefab = LoadGuid("5d7898dec4b40456cb816a7b26090238");
        if (unitButtonPrefab == null) unitButtonPrefab = LoadGuid("e264411283ec7400e8a63994aa6b08cf");
    }

    void StockEmptyCatalogs()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return;

        GameObject attackerEngineer = LoadGuid("fd2a0695a04c94bceab4cd3bcf1eb749");
        GameObject defenderEngineer = LoadGuid("16b76f60c4407480d93619799eb5b015");
        GameObject attackerTank = LoadGuid("26caaaf0ac1904b319b62c4efa9fea2c");
        GameObject defenderAssault = LoadGuid("6fef076922fa44142a5c66daa6436955");

        foreach (Sector sector in GameManager.Instance.sectors)
        {
            if (sector == null || sector.capturePoints == null) continue;
            foreach (CapturePoint point in sector.capturePoints)
            {
                if (point == null) continue;
                if (point.attackerPurchasables == null || point.attackerPurchasables.Length == 0)
                {
                    point.attackerPurchasables = new PurchasableUnit[]
                    {
                        MakeUnit("Engineer", attackerEngineer, 80),
                        MakeUnit("Tank", attackerTank, 5)
                    };
                }

                if (point.defenderPurchasables == null || point.defenderPurchasables.Length == 0)
                {
                    point.defenderPurchasables = new PurchasableUnit[]
                    {
                        MakeUnit("Engineer", defenderEngineer, 80),
                        MakeUnit("Assault", defenderAssault, 40)
                    };
                }
            }
        }
    }

    static PurchasableUnit MakeUnit(string displayName, GameObject prefab, int cost)
    {
        return new PurchasableUnit
        {
            unitDisplayName = displayName,
            unitPrefab = prefab,
            xpCost = cost
        };
    }

    static GameObject LoadGuid(string guid)
    {
#if UNITY_EDITOR
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private void InitializeStoreUI()
    {
        if (GameManager.Instance == null) return;
        Faction playerFaction = GameManager.Instance.playerFaction;

        // Generate a row for every capture point across all sectors
        foreach (Sector sector in GameManager.Instance.sectors)
        {
            if (sector == null || sector.capturePoints == null) continue;
            foreach (CapturePoint cp in sector.capturePoints)
            {
                if (cp == null) continue;

                GameObject newRow = Instantiate(rowPrefab, sidebarContainer);
                Debug.Log($"🛠️ UI CHECK: Created row for {cp.capturePointName}"); 
                
                TextMeshProUGUI rowText = newRow.GetComponentInChildren<TextMeshProUGUI>();
                if (rowText != null) rowText.text = cp.capturePointName;

                PurchasableUnit[] unitsToDisplay = (playerFaction == Faction.Attacker) ? cp.attackerPurchasables : cp.defenderPurchasables;
                if (unitsToDisplay == null) continue;

                foreach (PurchasableUnit unit in unitsToDisplay)
                {
                    if (unit == null || unit.unitPrefab == null) continue;
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