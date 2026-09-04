using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TestDashboardOverlay : MonoBehaviour
{
    public static TestDashboardOverlay Instance { get; private set; }

    [Header("Batch Testing State (Idea B)")]
    public static int TargetMatchCount = 0;
    public static int CurrentMatchNumber = 0;
    public static int TotalAttackerWins = 0;
    public static int TotalDefenderWins = 0;
    public static List<float> MatchDurations = new List<float>();

    [Header("Choke Point Death Markers (Idea D)")]
    public bool enableDeathMarkers = true;
    public float markerDuration = 15f;
    private static List<DeathMarker> activeDeathMarkers = new List<DeathMarker>();

    private struct DeathMarker
    {
        public Vector3 position;
        public Color color;
        public float expireTime;
    }

    private struct ClassCounts
    {
        public int assault;
        public int engineer;
        public int recon;
        public int support;

        public int Total => assault + engineer + recon + support;
    }

    [Header("UI References (Idea A)")]
    private GameObject overlayPanel;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI batchSummaryText;

    private float updateTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateOverlayUI();
    }

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.enableAutoTestMode)
        {
            if (CurrentMatchNumber == 0)
            {
                CurrentMatchNumber = 1;
            }
            if (overlayPanel != null) overlayPanel.SetActive(true);
        }
        else
        {
            if (overlayPanel != null) overlayPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.enableAutoTestMode)
        {
            if (overlayPanel != null && overlayPanel.activeSelf) overlayPanel.SetActive(false);
            return;
        }

        if (overlayPanel != null && !overlayPanel.activeSelf)
        {
            overlayPanel.SetActive(true);
        }

        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer >= 0.25f)
        {
            updateTimer = 0f;
            RefreshDashboard();
        }

        float now = Time.time;
        activeDeathMarkers.RemoveAll(m => now >= m.expireTime);
    }

    public static void RecordUnitDeath(Vector3 worldPos, bool isAttacker)
    {
        if (Instance != null && !Instance.enableDeathMarkers) return;

        Color markerCol = isAttacker ? new Color(1f, 0.2f, 0.2f, 0.85f) : new Color(0.2f, 0.5f, 1f, 0.85f);
        activeDeathMarkers.Add(new DeathMarker
        {
            position = worldPos,
            color = markerCol,
            expireTime = Time.time + (Instance != null ? Instance.markerDuration : 15f)
        });

        if (activeDeathMarkers.Count > 200)
        {
            activeDeathMarkers.RemoveAt(0);
        }
    }

    public static void RecordMatchCompleted(string winner, float duration)
    {
        if (winner == "Attackers") TotalAttackerWins++;
        else if (winner == "Defenders") TotalDefenderWins++;

        MatchDurations.Add(duration);
        AutoTestTelemetry.RecordMatchCompleted(winner, duration);
    }

    public static void ResetBatchStats()
    {
        CurrentMatchNumber = 0;
        TotalAttackerWins = 0;
        TotalDefenderWins = 0;
        MatchDurations.Clear();
        activeDeathMarkers.Clear();
    }

    private void RefreshDashboard()
    {
        if (statsText == null || GameManager.Instance == null) return;

        var gm = GameManager.Instance;
        float elapsed = Time.time - (float)typeof(GameManager).GetField("matchStartTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm);
        float elapsedMin = elapsed / 60f;

        float atkBurnRate = elapsedMin > 0.05f ? (150 - gm.attackerTickets) / elapsedMin : 0f;
        float defBurnRate = elapsedMin > 0.05f ? (100 - gm.defenderTickets) / elapsedMin : 0f;

        float avgAtkLife = gm.attackerDeaths > 0 ? gm.attackerTotalLifespan / gm.attackerDeaths : 0f;
        float avgDefLife = gm.defenderDeaths > 0 ? gm.defenderTotalLifespan / gm.defenderDeaths : 0f;

        ClassCounts attackerClasses = CountClasses("Attacker");
        ClassCounts defenderClasses = CountClasses("Defender");

        string activeSectorName = (gm.sectors != null && gm.currentSectorIndex < gm.sectors.Length)
            ? gm.sectors[gm.currentSectorIndex].sectorName
            : $"Sector {gm.currentSectorIndex}";

        int totalMatches = TotalAttackerWins + TotalDefenderWins;
        float atkWsPct = totalMatches > 0 ? (float)TotalAttackerWins / totalMatches * 100f : 0f;
        float defWsPct = totalMatches > 0 ? (float)TotalDefenderWins / totalMatches * 100f : 0f;

        float avgMatchDuration = 0f;
        if (MatchDurations.Count > 0)
        {
            float totalDuration = 0f;
            foreach (float duration in MatchDurations) totalDuration += duration;
            avgMatchDuration = totalDuration / MatchDurations.Count;
        }

        string batchHeader = TargetMatchCount > 0
            ? $"<b>BATCH PROGRESS: {CurrentMatchNumber}/{TargetMatchCount}</b>"
            : $"<b>TEST RUN #{CurrentMatchNumber}</b>";

        statsText.text =
            $"{batchHeader}\n" +
            $"<color=#FFD700>Speed:</color> {Time.timeScale:0}x | <color=#FFD700>Sector:</color> {activeSectorName}\n" +
            $"<color=#5A9BD5><b>ATTACKERS</b></color> [Tickets: {Mathf.Max(0, gm.attackerTickets)} | XP: {gm.attackerXP}]\n" +
            $"Live: A {attackerClasses.assault} | E {attackerClasses.engineer} | R {attackerClasses.recon} | S {attackerClasses.support} | <b>Total {attackerClasses.Total}</b>\n" +
            $"Deaths: {gm.attackerDeaths} | Avg Life: {avgAtkLife:F1}s | Burn: {atkBurnRate:F1} t/m\n\n" +
            $"<color=#C00000><b>DEFENDERS</b></color> [Tickets: {Mathf.Max(0, gm.defenderTickets)} | XP: {gm.defenderXP}]\n" +
            $"Live: A {defenderClasses.assault} | E {defenderClasses.engineer} | R {defenderClasses.recon} | S {defenderClasses.support} | <b>Total {defenderClasses.Total}</b>\n" +
            $"Deaths: {gm.defenderDeaths} | Avg Life: {avgDefLife:F1}s | Burn: {defBurnRate:F1} t/m\n\n" +
            $"<b>BATCH AGGREGATE ({totalMatches} Finished):</b>\n" +
            $"Atk Wins: {TotalAttackerWins} ({atkWsPct:F0}%) | Def Wins: {TotalDefenderWins} ({defWsPct:F0}%)\n" +
            $"Avg Match: {avgMatchDuration:F1}s | Detailed CSV telemetry enabled";
    }

    private ClassCounts CountClasses(string factionTag)
    {
        ClassCounts counts = new ClassCounts();
        GameObject[] units = GameObject.FindGameObjectsWithTag(factionTag);

        foreach (GameObject unit in units)
        {
            if (unit == null) continue;

            UnitClassIdentity identity = unit.GetComponent<UnitClassIdentity>();
            UnitClass unitClass = identity != null ? identity.Class : UnitClass.Assault;

            switch (unitClass)
            {
                case UnitClass.Engineer:
                    counts.engineer++;
                    break;
                case UnitClass.Recon:
                    counts.recon++;
                    break;
                case UnitClass.Support:
                    counts.support++;
                    break;
                default:
                    counts.assault++;
                    break;
            }
        }

        return counts;
    }

    private void CreateOverlayUI()
    {
        var gameplayCanvas = GameObject.Find("Canvas_Gameplay");
        if (gameplayCanvas == null) return;

        Transform existing = gameplayCanvas.transform.Find("TestDashboardPanel");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        overlayPanel = new GameObject("TestDashboardPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        overlayPanel.transform.SetParent(gameplayCanvas.transform, false);

        var img = overlayPanel.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

        var rt = overlayPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-15f, -15f);
        rt.sizeDelta = new Vector2(440f, 335f);

        GameObject textGo = new GameObject("StatsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(overlayPanel.transform, false);

        statsText = textGo.GetComponent<TextMeshProUGUI>();
        statsText.fontSize = 13;
        statsText.color = Color.white;
        statsText.alignment = TextAlignmentOptions.TopLeft;

        var anyTmp = Object.FindAnyObjectByType<TextMeshProUGUI>();
        if (anyTmp != null) statsText.font = anyTmp.font;

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 12f);
        textRt.offsetMax = new Vector2(-12f, -12f);
    }

    void OnDrawGizmos()
    {
        if (!enableDeathMarkers || activeDeathMarkers == null || activeDeathMarkers.Count == 0) return;

        float now = Time.time;
        for (int i = 0; i < activeDeathMarkers.Count; i++)
        {
            var m = activeDeathMarkers[i];
            float lifeLeft = m.expireTime - now;
            if (lifeLeft <= 0) continue;

            float alpha = Mathf.Clamp01(lifeLeft / markerDuration);
            Gizmos.color = new Color(m.color.r, m.color.g, m.color.b, alpha * 0.75f);
            Gizmos.DrawSphere(m.position + Vector3.up * 0.2f, 0.6f);
            Gizmos.DrawWireSphere(m.position + Vector3.up * 0.2f, 0.8f);
        }
    }
}
