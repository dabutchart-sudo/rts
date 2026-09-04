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

        RefreshTickets();
    }

    void Update()
    {
        RefreshTickets();
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

        RefreshTickets();
    }

    private void RefreshTickets()
    {
        if (ticketText == null || GameManager.Instance == null) return;
        ticketText.text = $"Tickets: {Mathf.Max(0, GameManager.Instance.attackerTickets)}";
    }

    public void UpdateTickets(int tickets)
    {
        if (ticketText != null)
        {
            ticketText.text = $"Tickets: {Mathf.Max(0, tickets)}";
        }
    }

    // XP remains available to gameplay/debug systems, but is intentionally not part of the normal HUD.
    public void UpdateXP(int currentXP) { }

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
