using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;

[System.Serializable]
public class Sector
{
    public string sectorName = "Sector A";
    public string sectorAnnouncementText = "SECURE ALL OBJECTIVES";
    public CapturePoint[] capturePoints;

    [Header("Base Allocations")]
    public BaseZone attackerBase;
    public BaseZone defenderBase;

    [Header("Zone Bounds")]
    public Bounds sectorBounds;
}

public enum Faction { None, Attacker, Defender }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Automated Testing")]
    public bool enableAutoTestMode = false;

    [Tooltip("Simulation speed used while automated testing is active.")]
    public float testTimeMultiplier = 20f;

    [Tooltip("Number of matches to run before the automated batch stops. Minimum 1.")]
    [Min(1)] public int autoTestMatchCount = 20;

    private string logFilePath;

    [Header("Telemetry Data")]
    public int attackerDeaths = 0;
    public int defenderDeaths = 0;
    public float attackerTotalLifespan = 0f;
    public float defenderTotalLifespan = 0f;
    private float matchStartTime;

    [Header("Faction & AI Director")]
    public Faction playerFaction = Faction.None;
    public bool attackerAIEnabled = true;

    [Header("Match Settings")]
    public int attackerTickets = 150;
    public int defenderTickets = 100;
    public float sectorTransitionDelay = 12f;
    public int sectorCaptureTicketBonus = 30;
    public float sectorHoldRequiredDuration = 1.5f;
    private float sectorHoldTimer = 0f;
    public Sector[] sectors;
    public int currentSectorIndex = 0;

    [Header("Player Stats")]
    public int commandXP = 0;
    public int attackerXP = 0;
    public int defenderXP = 0;

    [Header("Spawners")]
    public UnitSpawner attackerSpawner;
    public UnitSpawner defenderSpawner;

    [Header("UI Panels & Directors")]
    public GameObject factionSelectionUI;
    public GameObject playerGameplayUI;
    public EnemyDirector enemyDirector;

    [Header("Rocket League Perspective Flip")]
    public Transform battlefieldParent;

    private bool isGameOver = false;
    public bool isTransitioningSector = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        logFilePath = Application.dataPath + "/MatchBalanceLogs.csv";
        currentSectorIndex = 0;

        if (TestDashboardOverlay.CurrentMatchNumber > 1)
        {
            enableAutoTestMode = true;
            StartCoroutine(StartAutoTest());
        }
        else if (enableAutoTestMode)
        {
            PrepareNewAutoTestBatch();
            StartCoroutine(StartAutoTest());
        }
    }

    private void PrepareNewAutoTestBatch()
    {
        TestDashboardOverlay.ResetBatchStats();
        TestDashboardOverlay.TargetMatchCount = Mathf.Max(1, autoTestMatchCount);
        TestDashboardOverlay.CurrentMatchNumber = 1;

        Debug.Log($"AUTO TEST: Starting batch of {TestDashboardOverlay.TargetMatchCount} matches at {testTimeMultiplier:0.#}x speed.");
    }

    private IEnumerator StartAutoTest()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        matchStartTime = Time.time;
        SelectDefenderFaction();
        Time.timeScale = testTimeMultiplier;
    }

    public void StartAutoTestFromMenu()
    {
        enableAutoTestMode = true;
        PrepareNewAutoTestBatch();
        SelectDefenderFaction();
        matchStartTime = Time.time;
        Time.timeScale = testTimeMultiplier;
    }

    void Start()
    {
        if (!enableAutoTestMode) matchStartTime = Time.time;
        ResetAllSectorsCompletely();
    }

    private void ResetAllSectorsCompletely()
    {
        if (sectors == null) return;
        for (int i = 0; i < sectors.Length; i++)
        {
            if (sectors[i].capturePoints != null)
            {
                foreach (CapturePoint cp in sectors[i].capturePoints)
                {
                    if (cp != null) cp.ResetCapturePoint();
                }
            }
        }
    }

    void Update()
    {
        if (isGameOver || playerFaction == Faction.None) return;
        CheckWinConditions();
        CheckSectorProgression();
    }

    public void SetAutoTestMode(bool isEnabled)
    {
        enableAutoTestMode = isEnabled;
    }

    public void SetTestSpeed(float speed)
    {
        testTimeMultiplier = Mathf.Clamp(speed, 1f, 100f);
        if (enableAutoTestMode && playerFaction != Faction.None && !isGameOver)
        {
            Time.timeScale = testTimeMultiplier;
        }
    }

    public void SelectAttackerFaction()
    {
        playerFaction = Faction.Attacker;
        if (battlefieldParent != null) battlefieldParent.rotation = Quaternion.Euler(0, 0, 0);

        if (attackerSpawner != null) attackerSpawner.BeginSpawning();
        if (defenderSpawner != null) defenderSpawner.BeginSpawning();
        if (enemyDirector != null) enemyDirector.ActivateEnemyDirector();

        StartMatch();
    }

    public void SelectDefenderFaction()
    {
        playerFaction = Faction.Defender;
        if (battlefieldParent != null) battlefieldParent.rotation = Quaternion.Euler(0, 180, 0);

        if (attackerSpawner != null) attackerSpawner.BeginSpawning();
        if (defenderSpawner != null) defenderSpawner.BeginSpawning();
        if (enemyDirector != null) enemyDirector.ActivateEnemyDirector();

        StartMatch();
    }

    private void StartMatch()
    {
        if (factionSelectionUI != null) factionSelectionUI.SetActive(false);
        if (playerGameplayUI != null) playerGameplayUI.SetActive(true);
        matchStartTime = Time.time;
        Time.timeScale = enableAutoTestMode ? testTimeMultiplier : 1f;
    }

    private void CheckSectorProgression()
    {
        try
        {
            if (isTransitioningSector || sectors == null || currentSectorIndex >= sectors.Length)
            {
                sectorHoldTimer = 0f;
                return;
            }

            Sector currentSector = sectors[currentSectorIndex];
            if (currentSector.capturePoints == null || currentSector.capturePoints.Length == 0)
            {
                sectorHoldTimer = 0f;
                return;
            }

            int totalPoints = currentSector.capturePoints.Length;
            int verifiedCapturedPoints = 0;
            string pointStatusSummary = "";

            for (int i = 0; i < totalPoints; i++)
            {
                CapturePoint cp = currentSector.capturePoints[i];
                if (cp == null)
                {
                    Debug.LogError($"🚨 CRITICAL INSPECTOR ERROR: CapturePoint at Index {i} in '{currentSector.sectorName}' is MISSING or NULL! The game cannot progress properly!");
                    sectorHoldTimer = 0f;
                    return;
                }

                cp.activeDuringSectorIndex = currentSectorIndex;
                pointStatusSummary += $"_{cp.capturePointName}-{cp.captureProgress:F0}";

                if (cp.captureProgress >= 99.99f)
                {
                    verifiedCapturedPoints++;
                }
            }

            if (verifiedCapturedPoints < totalPoints)
            {
                sectorHoldTimer = 0f;
                return;
            }

            sectorHoldTimer += Time.deltaTime;
            if (sectorHoldTimer < sectorHoldRequiredDuration)
            {
                return;
            }

            sectorHoldTimer = 0f;
            Debug.Log($"🎯 SECTOR COMPLETED: {currentSector.sectorName} ({verifiedCapturedPoints}/{totalPoints} points held at 100% for {sectorHoldRequiredDuration}s).");

            CaptureSectorScreenshot(currentSector.sectorName, pointStatusSummary);

            int nextSectorIndex = currentSectorIndex + 1;
            if (nextSectorIndex < sectors.Length)
            {
                StartCoroutine(HandleSectorTransition());
            }
            else
            {
                Debug.Log($"🏆 FINAL SECTOR CONQUERED: {currentSector.sectorName} is the last sector. Attackers win!");
                TriggerGameOver("Attackers");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"🔥 FATAL ERROR in CheckSectorProgression: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CaptureSectorScreenshot(string sectorName, string pointSummary)
    {
        try
        {
            string folderPath = Application.dataPath + "/../Screenshots/";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string sanitizedSectorName = sectorName.Replace(" ", "_");
            string filePath = $"{folderPath}{sanitizedSectorName}_Captured{pointSummary}_{timestamp}.png";

            ScreenCapture.CaptureScreenshot(filePath);
            Debug.Log($"📸 SCREENSHOT SAVED: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"📸 SCREENSHOT FAILED: {ex.Message}");
        }
    }

    private IEnumerator HandleSectorTransition()
    {
        isTransitioningSector = true;

        if (currentSectorIndex + 1 < sectors.Length && sectors[currentSectorIndex + 1].defenderBase != null)
        {
            Transform retreatPoint = sectors[currentSectorIndex + 1].defenderBase.spawnPoint;
            if (retreatPoint == null) retreatPoint = sectors[currentSectorIndex + 1].defenderBase.transform;

            GameObject[] defenders = GameObject.FindGameObjectsWithTag("Defender");
            foreach (GameObject def in defenders)
            {
                AutonomousUnit unit = def.GetComponent<AutonomousUnit>();
                if (unit != null) unit.OrderRetreat(retreatPoint);
            }
        }

        Sector completedSector = sectors[currentSectorIndex];
        if (completedSector.capturePoints != null)
        {
            foreach (CapturePoint cp in completedSector.capturePoints)
            {
                if (cp != null) cp.LockCapturePoint();
            }
        }

        float timer = sectorTransitionDelay;
        while (timer > 0f)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowIntermissionBanner("SECTOR SECURED! DEFENDERS RETREATING", timer);
            }
            float step = Mathf.Min(0.2f, timer);
            yield return new WaitForSeconds(step);
            timer -= step;
        }

        currentSectorIndex++;

        attackerTickets += sectorCaptureTicketBonus;
        if (UIManager.Instance != null) UIManager.Instance.UpdateTickets(attackerTickets);

        Sector activeSector = sectors[currentSectorIndex];

        if (activeSector.capturePoints != null)
        {
            foreach (CapturePoint cp in activeSector.capturePoints)
            {
                if (cp != null)
                {
                    cp.activeDuringSectorIndex = currentSectorIndex;
                    cp.ResetCapturePoint();
                }
            }
        }

        if (attackerSpawner != null && activeSector.attackerBase != null)
        {
            Transform attSpawn = activeSector.attackerBase.spawnPoint != null ? activeSector.attackerBase.spawnPoint : activeSector.attackerBase.transform;
            attackerSpawner.transform.position = attSpawn.position;
        }

        if (defenderSpawner != null && activeSector.defenderBase != null)
        {
            Transform defSpawn = activeSector.defenderBase.spawnPoint != null ? activeSector.defenderBase.spawnPoint : activeSector.defenderBase.transform;
            defenderSpawner.transform.position = defSpawn.position;
        }

        int attackerReplenished = attackerSpawner != null
            ? attackerSpawner.ReplenishAssaultsForSectorStart()
            : 0;

        int defenderReplenished = defenderSpawner != null
            ? defenderSpawner.ReplenishAssaultsForSectorStart()
            : 0;

        Debug.Log(
            $"🚩 SECTOR {currentSectorIndex + 1} READY: " +
            $"Attackers replenished {attackerReplenished}, Defenders replenished {defenderReplenished}, " +
            $"Attacker tickets remaining {attackerTickets}.");

        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.ClearStrategicObjectives();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearSectorStatuses();
            UIManager.Instance.ShowSectorCapturedBanner(activeSector.sectorAnnouncementText);
        }

        AutonomousUnit[] allUnits = FindObjectsByType<AutonomousUnit>(FindObjectsInactive.Exclude);
        foreach (AutonomousUnit unit in allUnits) unit.UpdateDestination();

        yield return new WaitForSeconds(1.5f);
        isTransitioningSector = false;
    }

    private void CheckWinConditions()
    {
        if (attackerTickets <= 0) TriggerGameOver("Defenders");
    }

    private void TriggerGameOver(string winner)
    {
        isGameOver = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowGameOver($"{winner.ToUpper()} WIN!");

        if (enableAutoTestMode)
        {
            float duration = Time.time - matchStartTime;
            LogMatchData(winner);
            TestDashboardOverlay.RecordMatchCompleted(winner, duration);
            AutoTestTelemetry.RecordMatchCompleted(winner, duration);

            int targetMatches = Mathf.Max(1, TestDashboardOverlay.TargetMatchCount);
            bool shouldContinueBatch = TestDashboardOverlay.CurrentMatchNumber < targetMatches;

            if (shouldContinueBatch)
            {
                TestDashboardOverlay.CurrentMatchNumber++;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else
            {
                float averageDuration = 0f;
                if (TestDashboardOverlay.MatchDurations.Count > 0)
                {
                    foreach (float matchDuration in TestDashboardOverlay.MatchDurations)
                    {
                        averageDuration += matchDuration;
                    }
                    averageDuration /= TestDashboardOverlay.MatchDurations.Count;
                }

                int finishedMatches = TestDashboardOverlay.TotalAttackerWins + TestDashboardOverlay.TotalDefenderWins;
                float attackerWinRate = finishedMatches > 0
                    ? (float)TestDashboardOverlay.TotalAttackerWins / finishedMatches * 100f
                    : 0f;

                Debug.Log(
                    $"🏁 BATCH TEST COMPLETED! Matches: {finishedMatches} | " +
                    $"Attackers {TestDashboardOverlay.TotalAttackerWins} ({attackerWinRate:F1}%) - " +
                    $"Defenders {TestDashboardOverlay.TotalDefenderWins} | Avg duration {averageDuration:F1}s");

                Time.timeScale = 0f;
            }
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    private void LogMatchData(string winner)
    {
        bool isNewFile = !File.Exists(logFilePath);
        float matchDuration = Time.time - matchStartTime;

        float avgAttackerLife = attackerDeaths > 0 ? attackerTotalLifespan / attackerDeaths : 0f;
        float avgDefenderLife = defenderDeaths > 0 ? defenderTotalLifespan / defenderDeaths : 0f;

        using (StreamWriter writer = new StreamWriter(logFilePath, true))
        {
            if (isNewFile)
            {
                writer.WriteLine("Winner,Match Time (s),Attacker Tickets,Defender Tickets,Atk Deaths,Def Deaths,Avg Atk Lifespan (s),Avg Def Lifespan (s)");
            }

            writer.WriteLine($"{winner},{matchDuration:F1},{Mathf.Max(0, attackerTickets)},{Mathf.Max(0, defenderTickets)},{attackerDeaths},{defenderDeaths},{avgAttackerLife:F1},{avgDefenderLife:F1}");
        }

        Debug.Log($"📊 LOGGED: {winner} Won | Time: {matchDuration:F1}s | Avg Atk Life: {avgAttackerLife:F1}s | Avg Def Lifespan: {avgDefenderLife:F1}s");
    }

    public void AddXP(int amount)
    {
        commandXP += amount;
        attackerXP += amount;
    }

    public void AddXP(int amount, string team)
    {
        if (team == "Attacker")
        {
            commandXP += amount;
            attackerXP += amount;
        }
        else if (team == "Defender")
        {
            defenderXP += amount;
        }
    }

    public bool SpendTickets(bool isAttacker, int amount)
    {
        if (isAttacker)
        {
            if (attackerTickets >= amount)
            {
                attackerTickets -= amount;
                if (UIManager.Instance != null) UIManager.Instance.UpdateTickets(attackerTickets);
                return true;
            }
            return false;
        }
        else
        {
            return true;
        }
    }

    public bool SpendTickets(int amount)
    {
        if (attackerTickets >= amount)
        {
            attackerTickets -= amount;
            if (UIManager.Instance != null) UIManager.Instance.UpdateTickets(attackerTickets);
            return true;
        }
        return false;
    }

    public Transform GetCurrentTarget(GameObject unit, bool isAttacker)
    {
        if (sectors == null || sectors.Length == 0) return null;
        if (isTransitioningSector) return null;
        if (currentSectorIndex >= sectors.Length) return null;

        Sector currentSector = sectors[currentSectorIndex];
        if (currentSector.capturePoints == null || currentSector.capturePoints.Length == 0) return null;

        List<CapturePoint> activePoints = new List<CapturePoint>();
        foreach (CapturePoint cp in currentSector.capturePoints)
        {
            if (cp != null) activePoints.Add(cp);
        }

        if (activePoints.Count == 0) return null;
        if (activePoints.Count == 1) return activePoints[0].transform;

        int assignmentId = GetStrategicAssignmentId(unit);

        if (isAttacker)
        {
            List<CapturePoint> uncapturedPoints = new List<CapturePoint>();
            List<CapturePoint> capturedPoints = new List<CapturePoint>();

            foreach (CapturePoint cp in activePoints)
            {
                if (cp.captureProgress >= 100f) capturedPoints.Add(cp);
                else uncapturedPoints.Add(cp);
            }

            if (uncapturedPoints.Count > 0 && capturedPoints.Count > 0)
            {
                bool isAssaultSquad = (assignmentId % 4) != 0;
                if (isAssaultSquad)
                {
                    int idx = (assignmentId / 4) % uncapturedPoints.Count;
                    return uncapturedPoints[idx].transform;
                }
                else
                {
                    int idx = (assignmentId / 4) % capturedPoints.Count;
                    return capturedPoints[idx].transform;
                }
            }
            else if (uncapturedPoints.Count > 0)
            {
                int idx = assignmentId % uncapturedPoints.Count;
                return uncapturedPoints[idx].transform;
            }
            else
            {
                int idx = assignmentId % activePoints.Count;
                return activePoints[idx].transform;
            }
        }
        else
        {
            List<CapturePoint> threatenedPoints = new List<CapturePoint>();
            List<CapturePoint> securePoints = new List<CapturePoint>();

            foreach (CapturePoint cp in activePoints)
            {
                if (cp.captureProgress > -100f || cp.attackerCount > 0) threatenedPoints.Add(cp);
                else securePoints.Add(cp);
            }

            if (threatenedPoints.Count > 0 && securePoints.Count > 0)
            {
                bool isCounterAttackSquad = (assignmentId % 4) != 0;
                if (isCounterAttackSquad)
                {
                    int idx = (assignmentId / 4) % threatenedPoints.Count;
                    return threatenedPoints[idx].transform;
                }
                else
                {
                    int idx = (assignmentId / 4) % securePoints.Count;
                    return securePoints[idx].transform;
                }
            }
            else if (threatenedPoints.Count > 0)
            {
                int idx = assignmentId % threatenedPoints.Count;
                return threatenedPoints[idx].transform;
            }
            else
            {
                int idx = assignmentId % activePoints.Count;
                return activePoints[idx].transform;
            }
        }
    }

    private int GetStrategicAssignmentId(GameObject unit)
    {
        if (unit == null) return 0;

        SquadMember squadMember = unit.GetComponent<SquadMember>();
        if (squadMember != null && squadMember.Squad != null)
        {
            string squadId = squadMember.Squad.SquadId;
            if (!string.IsNullOrEmpty(squadId))
            {
                return GetStablePositiveHash(squadId);
            }
        }

        string fallbackIdentity = $"{unit.name}:{unit.transform.GetSiblingIndex()}";
        return GetStablePositiveHash(fallbackIdentity);
    }

    private int GetStablePositiveHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            if (hash == int.MinValue) return int.MaxValue;
            return Mathf.Abs(hash);
        }
    }

    void OnDrawGizmos()
    {
        if (sectors == null) return;

        for (int i = 0; i < sectors.Length; i++)
        {
            Sector sector = sectors[i];
            bool isCurrent = (i == currentSectorIndex);

            if (sector.capturePoints != null)
            {
                foreach (CapturePoint cp in sector.capturePoints)
                {
                    if (cp == null) continue;

                    if (sector.attackerBase != null)
                    {
                        Gizmos.color = Color.red;
                        Vector3 spawnPos = sector.attackerBase.spawnPoint != null ? sector.attackerBase.spawnPoint.position : sector.attackerBase.transform.position;
                        Gizmos.DrawLine(spawnPos, cp.transform.position);
                    }
                    if (sector.defenderBase != null)
                    {
                        Gizmos.color = Color.blue;
                        Vector3 spawnPos = sector.defenderBase.spawnPoint != null ? sector.defenderBase.spawnPoint.position : sector.defenderBase.transform.position;
                        Gizmos.DrawLine(spawnPos, cp.transform.position);
                    }
                }
            }

            if (sector.sectorBounds.size != Vector3.zero)
            {
                Gizmos.color = isCurrent ? Color.green : Color.yellow;
                Gizmos.DrawWireCube(sector.sectorBounds.center, sector.sectorBounds.size);
            }
        }
    }
}
