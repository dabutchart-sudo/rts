using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Shared gameplay HUD used by every battlefield through the shared gameplay systems prefab.
/// A fresh HUD instance is created with each battlefield scene.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Text References")]
    public TextMeshProUGUI ticketText;
    public TextMeshProUGUI captureText;
    public TextMeshProUGUI gameOverText;

    private GameObject xpHudRoot;
    private TextMeshProUGUI xpText;
    private readonly Dictionary<string, string> capturePointStatuses = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        if (xpHudRoot != null)
        {
            Destroy(xpHudRoot);
            xpHudRoot = null;
            xpText = null;
        }
    }

    void Start()
    {
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        EnsureXPHUD();
        RefreshHUD();
    }

    void Update()
    {
        EnsureXPHUD();
        RefreshHUD();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        capturePointStatuses.Clear();
        if (captureText != null) captureText.text = string.Empty;

        if (gameOverText != null)
        {
            StopAllCoroutines();
            gameOverText.gameObject.SetActive(false);
        }

        EnsureXPHUD();
        RefreshHUD();
    }

    private void EnsureXPHUD()
    {
        if (xpText != null && xpHudRoot != null) return;

        if (xpHudRoot != null)
        {
            Destroy(xpHudRoot);
            xpHudRoot = null;
            xpText = null;
        }

        // Use a dedicated overlay canvas so recovered scene layout groups, masks, scaling and
        // sibling order cannot clip or hide the XP block. This deliberately has no GraphicRaycaster
        // because it is display-only and needs no EventSystem.
        xpHudRoot = new GameObject("XP_HUD_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(xpHudRoot);

        Canvas canvas = xpHudRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = xpHudRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        GameObject xpObject = new GameObject("XPText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        xpObject.transform.SetParent(xpHudRoot.transform, false);

        RectTransform rect = xpObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -42f);
        rect.sizeDelta = new Vector2(360f, 70f);

        xpText = xpObject.GetComponent<TextMeshProUGUI>();
        if (ticketText != null && ticketText.font != null)
        {
            xpText.font = ticketText.font;
            xpText.color = ticketText.color;
        }
        else
        {
            xpText.color = Color.white;
        }

        xpText.fontSize = 20f;
        xpText.fontStyle = FontStyles.Bold;
        xpText.alignment = TextAlignmentOptions.TopLeft;
        xpText.enableWordWrapping = false;
        xpText.overflowMode = TextOverflowModes.Overflow;
        xpText.raycastTarget = false;
        xpText.text = "XP HUD INITIALISING";

        Debug.Log("UIManager: dedicated XP overlay canvas created.");
    }

    private void RefreshHUD()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

        if (ticketText != null)
        {
            ticketText.text = $"Tickets: {Mathf.Max(0, gameManager.attackerTickets)}";
        }

        if (xpText == null) return;

        // Editor/development builds expose both team totals for AI tuning and observation.
        // Normal release builds expose only the local player's team total.
        if (Application.isEditor || Debug.isDebugBuild)
        {
            xpText.text =
                $"Attacker XP: {Mathf.Max(0, gameManager.attackerXP)}\n" +
                $"Defender XP: {Mathf.Max(0, gameManager.defenderXP)}";
        }
        else
        {
            switch (gameManager.playerFaction)
            {
                case Faction.Attacker:
                    xpText.text = $"Team XP: {Mathf.Max(0, gameManager.attackerXP)}";
                    break;

                case Faction.Defender:
                    xpText.text = $"Team XP: {Mathf.Max(0, gameManager.defenderXP)}";
                    break;

                default:
                    xpText.text = string.Empty;
                    break;
            }
        }
    }

    public void UpdateTickets(int tickets)
    {
        RefreshHUD();
    }

    public void UpdateXP(int currentXP)
    {
        RefreshHUD();
    }

    public void UpdateCaptureStatus(string capturePointName, float progress, int attackers, int defenders)
    {
        if (captureText == null) return;

        int displayPercentage = Mathf.Abs(Mathf.RoundToInt(progress));
        string statusText;
        string colorHex;

        if (progress >= 100f)
        {
            statusText = capturePointName + ": SECURED (100%)";
            colorHex = "#00FFFF";
        }
        else if (progress > 0f)
        {
            statusText = capturePointName + ": Capturing (" + displayPercentage + "% Atk)";
            colorHex = "#00FFFF";
        }
        else if (Mathf.Approximately(progress, 0f))
        {
            statusText = capturePointName + ": Neutralized (0%)";
            colorHex = "#FFFFFF";
        }
        else if (progress > -100f)
        {
            if (attackers > defenders)
            {
                statusText = capturePointName + ": Neutralizing (" + displayPercentage + "% Def)";
                colorHex = "#FFA500";
            }
            else
            {
                statusText = capturePointName + ": Defenders Fortifying (" + displayPercentage + "% Def)";
                colorHex = "#FF0000";
            }
        }
        else
        {
            statusText = capturePointName + ": Defender Controlled (100%)";
            colorHex = "#FF0000";
        }

        capturePointStatuses[capturePointName] = $"<color={colorHex}>{statusText}</color>";

        string combinedText = string.Empty;
        foreach (string status in capturePointStatuses.Values)
        {
            combinedText += status + "\n";
        }

        captureText.text = combinedText;
    }

    public void ShowSectorCapturedBanner(string message)
    {
        if (gameOverText == null) return;
        StopAllCoroutines();
        StartCoroutine(FlashSectorBanner(message));
    }

    public void ShowIntermissionBanner(string header, float secondsRemaining)
    {
        if (gameOverText == null) return;

        StopAllCoroutines();
        gameOverText.gameObject.SetActive(true);
        int ceilSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
        gameOverText.text =
            $"<size=110%><color=#FFD700><b>{header}</b></color></size>\n" +
            $"<size=85%>Next Sector In: <color=#00FFFF><b>{ceilSeconds}s</b></color></size>";
    }

    private IEnumerator FlashSectorBanner(string message)
    {
        gameOverText.gameObject.SetActive(true);
        gameOverText.text = message;
        yield return new WaitForSeconds(3.5f);
        gameOverText.gameObject.SetActive(false);
    }

    public void ShowGameOver(string message)
    {
        if (gameOverText == null) return;

        StopAllCoroutines();
        gameOverText.gameObject.SetActive(true);
        gameOverText.text = message;
    }

    public void ClearSectorStatuses()
    {
        capturePointStatuses.Clear();
        if (captureText != null) captureText.text = string.Empty;
    }
}
