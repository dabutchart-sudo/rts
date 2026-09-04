using UnityEngine;
using System.Collections.Generic;
using TMPro;

// This class defines what units can be bought and how much they cost
[System.Serializable]
public class PurchasableUnit
{
    public string unitDisplayName = "Heavy Tank";
    public GameObject unitPrefab;
    public int xpCost = 500;
    public Sprite unitIcon;
}

public class CapturePoint : MonoBehaviour
{
    [Header("Capture Point Identity")]
    public string capturePointName = "A1";
    public int activeDuringSectorIndex = 0;

    [Header("Capture Settings")]
    [Range(-100f, 100f)]
    public float captureProgress = -100f;

    [Tooltip("Base capture progress per second before unit advantage and the overall rate multiplier are applied.")]
    public float captureSpeed = 15f;

    [Tooltip("Maximum capture-speed multiplier produced by a numerical advantage inside the capture zone.")]
    public float maxCaptureMultiplier = 4f;

    [Tooltip("Overall capture-rate tuning control. 0.33 makes the existing captureSpeed of 15 behave like roughly 5 progress per second before numerical advantage.")]
    [Range(0.1f, 2f)]
    public float captureRateMultiplier = 0.33f;

    [Header("Current Occupants")]
    public int attackerCount = 0;
    public int defenderCount = 0;
    public bool isLocked = false;

    [Header("Team Colors")]
    public Color attackerColor = Color.red;
    public Color defenderColor = Color.blue;
    public Color neutralColor = Color.gray;

    [Header("UI")]
    public TextMeshProUGUI progressText;
    public float groundOffset = 0.15f;

    [Header("Base Production Capabilities")]
    [Tooltip("Units the Attacker can buy when they own this base")]
    public PurchasableUnit[] attackerPurchasables;

    [Tooltip("Units the Defender can buy when they own this base")]
    public PurchasableUnit[] defenderPurchasables;

    [Header("Purchase Spawn Locations")]
    [Tooltip("Drag empty child GameObjects here to set exact spawn locations for purchased vehicles/snipers")]
    public Transform[] purchaseSpawnPoints;
    private int nextSpawnIndex = 0;

    private List<GameObject> activeAttackers = new List<GameObject>();
    private List<GameObject> activeDefenders = new List<GameObject>();

    private Renderer pointRenderer;
    private Collider pointCollider;
    private Transform canvasTransform;

    void Awake()
    {
        captureProgress = -100f;
        pointCollider = GetComponent<Collider>();

        if (progressText != null && progressText.transform.parent != null)
        {
            canvasTransform = progressText.transform.parent;
            canvasTransform.SetParent(null);
            canvasTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }

        ResetCapturePoint();
    }

    public void ResetCapturePoint()
    {
        isLocked = false;
        captureProgress = -100f;
        activeAttackers.Clear();
        activeDefenders.Clear();
        UpdatePointColor();
        UpdateFloatingText();
    }

    public void LockCapturePoint()
    {
        isLocked = true;
        captureProgress = 100f; // Permanently 100% Attacker captured for this match
        activeAttackers.Clear();
        activeDefenders.Clear();
        attackerCount = 0;
        defenderCount = 0;
        UpdatePointColor();
        UpdateFloatingText();
    }

    void Start()
    {
        pointRenderer = GetComponent<Renderer>();
        if (pointCollider == null) pointCollider = GetComponent<Collider>();

        captureProgress = -100f;
        ResetCapturePoint();

        if (UIManager.Instance != null && GameManager.Instance != null && GameManager.Instance.currentSectorIndex == activeDuringSectorIndex)
        {
            UIManager.Instance.UpdateCaptureStatus(capturePointName, captureProgress, attackerCount, defenderCount);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isLocked) return;
        if (GameManager.Instance != null && GameManager.Instance.currentSectorIndex != activeDuringSectorIndex) return;

        if (other.CompareTag("Attacker") && !activeAttackers.Contains(other.gameObject))
        {
            activeAttackers.Add(other.gameObject);
        }
        else if (other.CompareTag("Defender") && !activeDefenders.Contains(other.gameObject))
        {
            activeDefenders.Add(other.gameObject);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (isLocked) return;
        if (other.CompareTag("Attacker"))
            activeAttackers.Remove(other.gameObject);
        else if (other.CompareTag("Defender"))
            activeDefenders.Remove(other.gameObject);
    }

    void Update()
    {
        UpdateFlatTextPosition();

        if (isLocked)
        {
            activeAttackers.Clear();
            activeDefenders.Clear();
            attackerCount = 0;
            defenderCount = 0;
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.currentSectorIndex != activeDuringSectorIndex)
        {
            activeAttackers.Clear();
            activeDefenders.Clear();
            attackerCount = 0;
            defenderCount = 0;
            return;
        }

        activeAttackers.RemoveAll(item => item == null);
        activeDefenders.RemoveAll(item => item == null);

        attackerCount = activeAttackers.Count;
        defenderCount = activeDefenders.Count;

        int balance = attackerCount - defenderCount;

        if (balance != 0)
        {
            float advantage = Mathf.Abs(balance);
            float currentMultiplier = Mathf.Min(advantage, maxCaptureMultiplier);
            float direction = Mathf.Sign(balance);
            float effectiveCaptureSpeed = captureSpeed * Mathf.Max(0f, captureRateMultiplier);

            captureProgress += direction * currentMultiplier * effectiveCaptureSpeed * Time.deltaTime;
            captureProgress = Mathf.Clamp(captureProgress, -100f, 100f);

            UpdatePointColor();
            UpdateFloatingText();
        }

        if (UIManager.Instance != null && GameManager.Instance.currentSectorIndex == activeDuringSectorIndex)
        {
            UIManager.Instance.UpdateCaptureStatus(capturePointName, captureProgress, attackerCount, defenderCount);
        }
    }

    // --- XP STORE METHODS ---

    // A quick helper method for the UI to ask: "What can I build here?"
    public PurchasableUnit[] GetAvailableUnits(Faction playerFaction)
    {
        if (playerFaction == Faction.Attacker && captureProgress >= 100f)
            return attackerPurchasables;
        if (playerFaction == Faction.Defender && captureProgress <= -100f)
            return defenderPurchasables;

        return new PurchasableUnit[0];
    }

    // The Store script will call this to get a safe location to drop the new unit
    public Transform GetNextAvailableSpawnPoint()
    {
        if (purchaseSpawnPoints == null || purchaseSpawnPoints.Length == 0) return transform;

        Transform spawnPoint = purchaseSpawnPoints[nextSpawnIndex];

        nextSpawnIndex++;
        if (nextSpawnIndex >= purchaseSpawnPoints.Length) nextSpawnIndex = 0;

        return spawnPoint;
    }

    // Checks if the player's faction currently owns this point
    public bool IsControlledBy(Faction requestingFaction)
    {
        if (requestingFaction == Faction.Attacker && captureProgress >= 100f) return true;
        if (requestingFaction == Faction.Defender && captureProgress <= -100f) return true;
        return false;
    }

    // --- VISUAL UPDATES ---

    void UpdatePointColor()
    {
        if (pointRenderer == null) return;
        if (captureProgress > 0) pointRenderer.material.color = Color.Lerp(neutralColor, attackerColor, captureProgress / 100f);
        else if (captureProgress < 0) pointRenderer.material.color = Color.Lerp(neutralColor, defenderColor, Mathf.Abs(captureProgress) / 100f);
        else pointRenderer.material.color = neutralColor;
    }

    void UpdateFloatingText()
    {
        if (progressText != null)
        {
            if (isLocked)
            {
                progressText.text = "SECURED\nLOCKED";
                progressText.color = attackerColor;
                return;
            }

            float displayPercent = Mathf.Abs(captureProgress);
            if (captureProgress > 0) { progressText.text = $"ATTACKER\n{displayPercent:F0}%"; progressText.color = attackerColor; }
            else if (captureProgress < 0) { progressText.text = $"DEFENDER\n{displayPercent:F0}%"; progressText.color = defenderColor; }
            else { progressText.text = $"NEUTRAL\n0%"; progressText.color = neutralColor; }
        }
    }

    void UpdateFlatTextPosition()
    {
        if (canvasTransform == null) return;
        canvasTransform.position = transform.position + (Vector3.up * groundOffset);
        canvasTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}