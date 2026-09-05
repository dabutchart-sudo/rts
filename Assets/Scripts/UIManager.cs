using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Shared gameplay HUD. This object is intentionally map-independent and survives scene loads,
/// so every battlefield uses the same HUD instance and layout.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Text References")]
    public TextMeshProUGUI ticketText;
    public TextMeshProUGUI captureText;
    public TextMeshProUGUI gameOverText;

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
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void Start()
    {
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        EnsureXPText();
        RefreshHUD();
    }

    void Update()
    {
        EnsureXPText();
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

        xpText = null;
        EnsureXPText();
        RefreshHUD();
    }

    private void EnsureXPText()
    {
        if (xpText != null) return;
        if (ticketText == null) return;

        Canvas gameplayCanvas = ticketText.GetComponentInParent<Canvas>();
        if (gameplayCanvas == null) return;

        Transform existing = gameplayCanvas.transform.Find("XPText_Runtime");
        if (existing != null)
        {
            xpText = existing.GetComponent<TextMeshProUGUI>();
            if (xpText != null)
            {
                xpText.gameObject.SetActive(true);
                xpText.transform.SetAsLastSibling();
                return;
            }
        }

        // Keep XP directly on the gameplay Canvas rather than under the Tickets label's parent.
        // Some recovered HUD containers are tightly sized/masked; parenting XP there can make the
        // text exist correctly but be completely clipped from view.
        GameObject xpObject = new GameObject("XPText_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        xpObject.transform.SetParent(gameplayCanvas.transform, false);
        xpObject.transform.SetAsLastSibling();

        RectTransform xpRect = xpObject.GetComponent<RectTransform>();
        xpRect.anchorMin = new Vector2(0f, 1f);
        xpRect.anchorMax = new Vector2(0f, 1f);
        xpRect.pivot = new Vector2(0f, 1f);
        xpRect.anchoredPosition = new Vector2(12f, -38f);
        xpRect.sizeDelta = new Vector2(300f, 56f);

        xpText = xpObject.GetComponent<TextMeshProUGUI>();
        xpText.font = ticketText.font;
        xpText.fontSize = Mathf.Max(14f, ticketText.fontSize * 0.9f);
        xpText.fontStyle = FontStyles.Normal;
        xpText.color = ticketText.color;
        xpText.alignment = TextAlignmentOptions.TopLeft;
        xpText.enableWordWrapping = false;
        xpText.overflowMode = TextOverflowModes.Overflow;
        xpText.raycastTarget = false;
        xpText.text = string.Empty;

        Debug.Log($"UIManager: XP HUD created on canvas '{gameplayCanvas.name}'.");
    }

    private void RefreshHUD()
    {
        if (GameManager.Instance == null) return;

        GameManager gameManager = GameManager.Instance;

        if (ticketText != null)
        {
            ticketText.text = $"Tickets: {Mathf.Max(0, gameManager.attackerTickets)}";
        }

        if (xpText == null) return;

        // Development/Editor: both team totals are useful for AI balance observation.
        // Release build: only the player's own team XP is exposed.
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
