using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Text References")]
    public TextMeshProUGUI statsText; // This single box handles ALL tickets and XP now
    public TextMeshProUGUI captureText;
    public TextMeshProUGUI gameOverText;

    // Tracks individual capture points within the sector
    private Dictionary<string, string> capturePointStatuses = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureStatsText();
    }

    void EnsureStatsText()
    {
        if (statsText == null)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == "TicketText")
                {
                    statsText = labels[i];
                    break;
                }
            }
        }

        if (statsText == null) return;

        RectTransform rect = statsText.rectTransform;
        rect.sizeDelta = new Vector2(560f, 170f);
        statsText.fontSize = 28f;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.overflowMode = TextOverflowModes.Overflow;
    }

    void Start()
    {
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Continuously fetch and format the latest stats directly from the GameManager
        if (GameManager.Instance != null && statsText != null)
        {
            int attTickets = GameManager.Instance.attackerTickets;
            int defTickets = GameManager.Instance.defenderTickets;
            int attXP = GameManager.Instance.attackerXP;
            int defXP = GameManager.Instance.defenderXP;

            statsText.text = 
                "<color=#5A9BD5><b>ATTACKERS</b></color>\n" +
                $"Tickets: {attTickets}   |   XP: {attXP}\n\n" +
                "<color=#C00000><b>DEFENDERS</b></color>\n" +
                $"Tickets: {defTickets}   |   XP: {defXP}";
        }
    }

    // --- EMPTY STUBS TO PREVENT GAMEMANAGER ERRORS ---
    // GameManager still calls these, but we don't need them to do anything 
    // anymore because the Update() loop above is doing the heavy lifting.
    public void UpdateTickets(int tickets) { }
    public void UpdateXP(int currentXP) { }
    // -------------------------------------------------

    public void SetMatchReadoutVisible(bool visible)
    {
        if (!visible) ClearSectorStatuses();
        if (captureText != null) captureText.gameObject.SetActive(visible);
        if (statsText != null) statsText.gameObject.SetActive(visible);
        if (!visible) HideGameOver();
    }

    public void HideGameOver()
    {
        if (gameOverText == null) return;
        StopAllCoroutines();
        gameOverText.gameObject.SetActive(false);
    }

    public void UpdateCaptureStatus(string capturePointName, float progress, int attackers, int defenders)
    {
        if (captureText == null || !captureText.gameObject.activeInHierarchy) return;
        if (MapSession.phase == MapSession.Phase.Menu || MapSession.phase == MapSession.Phase.Edit) return;

        int displayPercentage = Mathf.Abs(Mathf.RoundToInt(progress));
        string statusText = "";
        string colorHex = "#FFFFFF"; 

        if (progress >= 100f)
        {
            statusText = capturePointName + ": SECURED (100%)";
            colorHex = "#00FFFF"; 
        }
        else if (progress > 0)
        {
            statusText = capturePointName + ": Capturing (" + displayPercentage + "% Atk)";
            colorHex = "#00FFFF"; 
        }
        else if (progress == 0)
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

        string combinedText = "";
        foreach (string status in capturePointStatuses.Values)
        {
            combinedText += status + "\n";
        }

        captureText.text = combinedText;
    }

    public void ShowSectorCapturedBanner(string message)
    {
        if (gameOverText != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashSectorBanner(message));
        }
    }

    public void ShowIntermissionBanner(string header, float secondsRemaining)
    {
        if (gameOverText != null)
        {
            StopAllCoroutines();
            gameOverText.gameObject.SetActive(true);
            int ceilSeconds = Mathf.CeilToInt(Mathf.Max(0, secondsRemaining));
            gameOverText.text = $"<size=110%><color=#FFD700><b>{header}</b></color></size>\n<size=85%>Next Sector In: <color=#00FFFF><b>{ceilSeconds}s</b></color></size>";
        }
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
        if (gameOverText != null)
        {
            StopAllCoroutines();
            gameOverText.gameObject.SetActive(true);
            gameOverText.text = message;
        }
    }
    
    public void ClearSectorStatuses()
    {
        capturePointStatuses.Clear();
        if (captureText != null) captureText.text = "";
    }
}