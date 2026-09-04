using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AutoTestTelemetry : MonoBehaviour
{
    private static AutoTestTelemetry instance;
    private static string runId;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 1f;
    [SerializeField] private bool logPerMatchSummary = true;
    [SerializeField] private bool logSquadStateSamples = false;

    [Header("Output")]
    [SerializeField] private string matchSummaryFileName = "AutoTestMatchMetrics_v1.csv";
    [SerializeField] private string squadSampleFileName = "AutoTestSquadSamples_v1.csv";

    private GameManager gameManager;
    private float matchStartTime;
    private float nextSampleTime;
    private int lastSectorIndex = -1;
    private bool matchRecorded;

    private readonly List<float> sectorEntryTimes = new List<float>();
    private readonly List<float> sectorCaptureTimes = new List<float>();

    private int sampleCount;
    private float attackerAliveTotal;
    private float defenderAliveTotal;
    private float healthySquadsTotal;
    private float depletedSquadsTotal;
    private float criticalSquadsTotal;
    private float recoveringSquadsTotal;

    private int maxAttackerAlive;
    private int maxDefenderAlive;
    private int maxSectorReached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeTelemetry()
    {
        if (instance != null) return;

        AutoTestTelemetry existing = FindAnyObjectByType<AutoTestTelemetry>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("AutoTestTelemetry");
        host.AddComponent<AutoTestTelemetry>();
    }

    public static void RecordMatchCompleted(string winner, float duration)
    {
        if (instance == null) return;
        instance.RecordCompletedMatch(winner, duration);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (string.IsNullOrEmpty(runId))
        {
            runId = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }
    }

    private void Start()
    {
        BindToCurrentMatch();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindToCurrentMatch();
    }

    private void BindToCurrentMatch()
    {
        gameManager = GameManager.Instance;

        if (!IsAutoTestActive())
        {
            enabled = false;
            return;
        }

        enabled = true;
        ResetMatchState();

        Debug.Log($"TEST TELEMETRY: Run {runId}, match {GetMatchNumber()} started.");
    }

    private void ResetMatchState()
    {
        matchStartTime = Time.time;
        nextSampleTime = Time.time;
        lastSectorIndex = gameManager.currentSectorIndex;
        matchRecorded = false;

        sectorEntryTimes.Clear();
        sectorCaptureTimes.Clear();

        sampleCount = 0;
        attackerAliveTotal = 0f;
        defenderAliveTotal = 0f;
        healthySquadsTotal = 0f;
        depletedSquadsTotal = 0f;
        criticalSquadsTotal = 0f;
        recoveringSquadsTotal = 0f;
        maxAttackerAlive = 0;
        maxDefenderAlive = 0;
        maxSectorReached = lastSectorIndex;

        EnsureSectorCapacity(lastSectorIndex);
        sectorEntryTimes[lastSectorIndex] = 0f;
    }

    private void Update()
    {
        if (!IsAutoTestActive() || matchRecorded) return;

        TrackSectorProgress();

        if (Time.time >= nextSampleTime)
        {
            SampleMatchState();
            nextSampleTime = Time.time + Mathf.Max(0.1f, sampleInterval);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    private bool IsAutoTestActive()
    {
        return gameManager != null && gameManager.enableAutoTestMode;
    }

    private void TrackSectorProgress()
    {
        int currentSector = gameManager.currentSectorIndex;
        if (currentSector == lastSectorIndex) return;

        float elapsed = GetElapsedTime();

        if (currentSector > lastSectorIndex)
        {
            EnsureSectorCapacity(lastSectorIndex);
            sectorCaptureTimes[lastSectorIndex] = elapsed;

            EnsureSectorCapacity(currentSector);
            sectorEntryTimes[currentSector] = elapsed;

            maxSectorReached = Mathf.Max(maxSectorReached, currentSector);
        }

        lastSectorIndex = currentSector;
    }

    private void SampleMatchState()
    {
        int attackerAlive = GameObject.FindGameObjectsWithTag("Attacker").Length;
        int defenderAlive = GameObject.FindGameObjectsWithTag("Defender").Length;

        attackerAliveTotal += attackerAlive;
        defenderAliveTotal += defenderAlive;
        maxAttackerAlive = Mathf.Max(maxAttackerAlive, attackerAlive);
        maxDefenderAlive = Mathf.Max(maxDefenderAlive, defenderAlive);

        int healthy = 0;
        int depleted = 0;
        int critical = 0;
        int recovering = 0;

        SquadManager squadManager = SquadManager.Instance;
        SquadAIController aiController = SquadAIController.Instance;

        if (squadManager != null && aiController != null)
        {
            CountSquadStates(squadManager.AttackerSquads, aiController, ref healthy, ref depleted, ref critical, ref recovering);
            CountSquadStates(squadManager.DefenderSquads, aiController, ref healthy, ref depleted, ref critical, ref recovering);
        }

        healthySquadsTotal += healthy;
        depletedSquadsTotal += depleted;
        criticalSquadsTotal += critical;
        recoveringSquadsTotal += recovering;
        sampleCount++;

        if (logSquadStateSamples)
        {
            AppendSquadSample(attackerAlive, defenderAlive, healthy, depleted, critical, recovering);
        }
    }

    private void CountSquadStates(
        IReadOnlyList<Squad> squads,
        SquadAIController controller,
        ref int healthy,
        ref int depleted,
        ref int critical,
        ref int recovering)
    {
        if (squads == null) return;

        foreach (Squad squad in squads)
        {
            if (squad == null || squad.MemberCount <= 0) continue;

            switch (controller.GetStrengthState(squad))
            {
                case SquadStrengthState.Healthy:
                    healthy++;
                    break;
                case SquadStrengthState.Depleted:
                    depleted++;
                    break;
                case SquadStrengthState.Critical:
                    critical++;
                    break;
            }

            if (controller.GetRecoveryState(squad) == SquadRecoveryState.Recovering)
            {
                recovering++;
            }
        }
    }

    private void RecordCompletedMatch(string winner, float duration)
    {
        if (matchRecorded || gameManager == null) return;

        TrackSectorProgress();

        if (winner == "Attackers" && gameManager.sectors != null && gameManager.sectors.Length > 0)
        {
            int finalSector = gameManager.sectors.Length - 1;
            EnsureSectorCapacity(finalSector);
            sectorCaptureTimes[finalSector] = Mathf.Max(duration, GetElapsedTime());
            maxSectorReached = Mathf.Max(maxSectorReached, finalSector);
        }

        if (sampleCount == 0)
        {
            SampleMatchState();
        }

        if (logPerMatchSummary)
        {
            AppendMatchSummary(winner, duration);
        }

        matchRecorded = true;
    }

    private void AppendMatchSummary(string winner, float duration)
    {
        string path = Path.Combine(Application.dataPath, matchSummaryFileName);
        bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

        float elapsed = Mathf.Max(duration, GetElapsedTime());
        float avgAttackerLife = gameManager.attackerDeaths > 0
            ? gameManager.attackerTotalLifespan / gameManager.attackerDeaths
            : 0f;
        float avgDefenderLife = gameManager.defenderDeaths > 0
            ? gameManager.defenderTotalLifespan / gameManager.defenderDeaths
            : 0f;

        float avgAttackerAlive = sampleCount > 0 ? attackerAliveTotal / sampleCount : 0f;
        float avgDefenderAlive = sampleCount > 0 ? defenderAliveTotal / sampleCount : 0f;
        float avgHealthySquads = sampleCount > 0 ? healthySquadsTotal / sampleCount : 0f;
        float avgDepletedSquads = sampleCount > 0 ? depletedSquadsTotal / sampleCount : 0f;
        float avgCriticalSquads = sampleCount > 0 ? criticalSquadsTotal / sampleCount : 0f;
        float avgRecoveringSquads = sampleCount > 0 ? recoveringSquadsTotal / sampleCount : 0f;

        int sectorsCaptured = CountCapturedSectors();
        string sectorTimes = BuildSectorTimesValue();

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (writeHeader)
            {
                writer.WriteLine(
                    "RunId,Match,Winner,DurationSeconds,SectorsCaptured,MaxSectorReached,AttackerTicketsRemaining," +
                    "AttackerDeaths,DefenderDeaths,AvgAttackerLifespan,AvgDefenderLifespan," +
                    "AvgAttackerAlive,AvgDefenderAlive,MaxAttackerAlive,MaxDefenderAlive," +
                    "AvgHealthySquads,AvgDepletedSquads,AvgCriticalSquads,AvgRecoveringSquads,SectorCaptureTimes");
            }

            writer.WriteLine(string.Join(",",
                Escape(runId),
                GetMatchNumber().ToString(CultureInfo.InvariantCulture),
                Escape(winner),
                elapsed.ToString("F1", CultureInfo.InvariantCulture),
                sectorsCaptured.ToString(CultureInfo.InvariantCulture),
                (maxSectorReached + 1).ToString(CultureInfo.InvariantCulture),
                Mathf.Max(0, gameManager.attackerTickets).ToString(CultureInfo.InvariantCulture),
                gameManager.attackerDeaths.ToString(CultureInfo.InvariantCulture),
                gameManager.defenderDeaths.ToString(CultureInfo.InvariantCulture),
                avgAttackerLife.ToString("F1", CultureInfo.InvariantCulture),
                avgDefenderLife.ToString("F1", CultureInfo.InvariantCulture),
                avgAttackerAlive.ToString("F2", CultureInfo.InvariantCulture),
                avgDefenderAlive.ToString("F2", CultureInfo.InvariantCulture),
                maxAttackerAlive.ToString(CultureInfo.InvariantCulture),
                maxDefenderAlive.ToString(CultureInfo.InvariantCulture),
                avgHealthySquads.ToString("F2", CultureInfo.InvariantCulture),
                avgDepletedSquads.ToString("F2", CultureInfo.InvariantCulture),
                avgCriticalSquads.ToString("F2", CultureInfo.InvariantCulture),
                avgRecoveringSquads.ToString("F2", CultureInfo.InvariantCulture),
                Escape(sectorTimes)));
        }

        Debug.Log(
            $"TEST TELEMETRY: Match {GetMatchNumber()} logged | {winner} | {elapsed:F1}s | " +
            $"Sectors {sectorsCaptured} | Atk deaths {gameManager.attackerDeaths} | Def deaths {gameManager.defenderDeaths}.");
    }

    private void AppendSquadSample(int attackerAlive, int defenderAlive, int healthy, int depleted, int critical, int recovering)
    {
        string path = Path.Combine(Application.dataPath, squadSampleFileName);
        bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (writeHeader)
            {
                writer.WriteLine("RunId,Match,ElapsedSeconds,Sector,AttackerAlive,DefenderAlive,HealthySquads,DepletedSquads,CriticalSquads,RecoveringSquads");
            }

            writer.WriteLine(string.Join(",",
                Escape(runId),
                GetMatchNumber().ToString(CultureInfo.InvariantCulture),
                GetElapsedTime().ToString("F1", CultureInfo.InvariantCulture),
                (gameManager.currentSectorIndex + 1).ToString(CultureInfo.InvariantCulture),
                attackerAlive.ToString(CultureInfo.InvariantCulture),
                defenderAlive.ToString(CultureInfo.InvariantCulture),
                healthy.ToString(CultureInfo.InvariantCulture),
                depleted.ToString(CultureInfo.InvariantCulture),
                critical.ToString(CultureInfo.InvariantCulture),
                recovering.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private int CountCapturedSectors()
    {
        int count = 0;
        for (int i = 0; i < sectorCaptureTimes.Count; i++)
        {
            if (sectorCaptureTimes[i] > 0f) count++;
        }
        return count;
    }

    private string BuildSectorTimesValue()
    {
        if (gameManager.sectors == null || gameManager.sectors.Length == 0) return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < gameManager.sectors.Length; i++)
        {
            if (i > 0) builder.Append(";");

            EnsureSectorCapacity(i);
            builder.Append("S");
            builder.Append(i + 1);
            builder.Append("=");

            if (sectorCaptureTimes[i] > 0f)
            {
                builder.Append(sectorCaptureTimes[i].ToString("F1", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append("-");
            }
        }

        return builder.ToString();
    }

    private void EnsureSectorCapacity(int sectorIndex)
    {
        if (sectorIndex < 0) return;

        while (sectorEntryTimes.Count <= sectorIndex)
        {
            sectorEntryTimes.Add(-1f);
        }

        while (sectorCaptureTimes.Count <= sectorIndex)
        {
            sectorCaptureTimes.Add(-1f);
        }
    }

    private int GetMatchNumber()
    {
        return Mathf.Max(1, TestDashboardOverlay.CurrentMatchNumber);
    }

    private float GetElapsedTime()
    {
        return Mathf.Max(0f, Time.time - matchStartTime);
    }

    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n")) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
