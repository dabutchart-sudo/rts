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
            if (sectors[i].capturePoints == null) continue;
            foreach (CapturePoint cp in sectors[i].capturePoints)
            {
                if (cp != null) cp.ResetCapturePoint();
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
                if (cp.captureProgress >= 99.99f) verifiedCapturedPoints++;
            }

            if (verifiedCapturedPoints < totalPoints)
            {
                sectorHoldTimer = 0f;
                return;
            }

            sectorHoldTimer += Time.deltaTime;
            if (sectorHoldTimer < sectorHoldRequiredDuration) return;

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
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
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

        int completedSectorIndex = currentSectorIndex;
        int nextSectorIndex = completedSectorIndex + 1;
        Sector completedSector = sectors[completedSectorIndex];
        Sector nextSector = sectors[nextSectorIndex];

        // The captured objectives are now permanent attacker ground for this match.
        if (completedSector.capturePoints != null)
        {
            foreach (CapturePoint cp in completedSector.capturePoints)
            {
                if (cp != null) cp.LockCapturePoint();
            }
        }

        // Prepare the next defensive sector immediately, but do not make it active until the
        // transition timer expires. This lets defender reinforcements assemble there beforehand.
        if (nextSector.capturePoints != null)
        {
            foreach (CapturePoint cp in nextSector.capturePoints)
            {
                if (cp == null) continue;
                cp.activeDuringSectorIndex = nextSectorIndex;
                cp.ResetCapturePoint();
            }
        }

        // Award the sector bonus before replenishment so it can fund the attacking refill.
        attackerTickets += sectorCaptureTicketBonus;
        if (UIManager.Instance != null) UIManager.Instance.UpdateTickets(attackerTickets);

        // Existing defenders retreat naturally. They are not despawned or teleported.
        if (nextSector.defenderBase != null)
        {
            Transform retreatPoint = nextSector.defenderBase.spawnPoint != null
                ? nextSector.defenderBase.spawnPoint
                : nextSector.defenderBase.transform;

            GameObject[] defenders = GameObject.FindGameObjectsWithTag("Defender");
            foreach (GameObject def in defenders)
            {
                AutonomousUnit unit = def.GetComponent<AutonomousUnit>();
                if (unit != null) unit.OrderRetreat(retreatPoint);
            }
        }

        // Fill both Assault rosters during the transition. Surviving Assaults are preserved and
        // count toward the cap. Attackers reinforce from the captured sector; defenders assemble
        // in the next sector. Specialists remain untouched and carry forward naturally.
        ReplenishTransitionRosters(completedSectorIndex, nextSectorIndex);

        float timer = sectorTransitionDelay;
        float nextRosterCheck = timer - 1f;

        while (timer > 0f)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowIntermissionBanner("SECTOR SECURED! CLEAR THE SECTOR - FRONTLINE OPENS SOON", timer);
            }

            if (timer <= nextRosterCheck)
            {
                ReplenishTransitionRosters(completedSectorIndex, nextSectorIndex);
                nextRosterCheck -= 1f;
            }

            float step = Mathf.Min(0.2f, timer);
            yield return new WaitForSeconds(step);
            timer -= step;
        }

        // One final top-up immediately before opening the new frontline. This guarantees the
        // strongest possible roster given the attacker's remaining ticket pool.
        ReplenishTransitionRosters(completedSectorIndex, nextSectorIndex);

        currentSectorIndex = nextSectorIndex;

        if (attackerSpawner != null && nextSector.attackerBase != null)
        {
            Transform attSpawn = nextSector.attackerBase.spawnPoint != null ? nextSector.attackerBase.spawnPoint : nextSector.attackerBase.transform;
            attackerSpawner.transform.position = attSpawn.position;
        }

        if (defenderSpawner != null && nextSector.defenderBase != null)
        {
            Transform defSpawn = nextSector.defenderBase.spawnPoint != null ? nextSector.defenderBase.spawnPoint : nextSector.defenderBase.transform;
            defenderSpawner.transform.position = defSpawn.position;
        }

        if (SquadManager.Instance != null) SquadManager.Instance.ClearStrategicObjectives();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearSectorStatuses();
            UIManager.Instance.ShowSectorCapturedBanner(nextSector.sectorAnnouncementText);
        }

        // Open the frontline before refreshing destinations. This is important: strategic AI is
        // intentionally suspended while the transition flag is true.
        isTransitioningSector = false;

        AutonomousUnit[] allUnits = FindObjectsByType<AutonomousUnit>(FindObjectsInactive.Exclude);
        foreach (AutonomousUnit unit in allUnits)
        {
            if (unit != null) unit.UpdateDestination();
        }

        int attackerReady = attackerSpawner != null ? attackerSpawner.GetAliveAssaultCount() : 0;
        int defenderReady = defenderSpawner != null ? defenderSpawner.GetAliveAssaultCount() : 0;
        int attackerCap = attackerSpawner != null ? attackerSpawner.GetAttackerAssaultCap() : 0;
        int defenderCap = defenderSpawner != null ? defenderSpawner.GetDefenderAssaultCap() : 0;

        Debug.Log($"🚩 SECTOR {currentSectorIndex + 1} OPEN: Attackers {attackerReady}/{attackerCap} Assaults, Defenders {defenderReady}/{defenderCap}, attacker tickets {attackerTickets}.");
    }

    private void ReplenishTransitionRosters(int attackerSpawnSectorIndex, int defenderSpawnSectorIndex)
    {
        if (attackerSpawner != null)
        {
            attackerSpawner.ReplenishAssaultsForTransition(attackerSpawnSectorIndex, true);
        }

        if (defenderSpawner != null)
        {
            defenderSpawner.ReplenishAssaultsForTransition(defenderSpawnSectorIndex, true);
        }
    }

    /// <summary>
    /// During a sector transition, attackers pursue retreating defenders only while those
    /// defenders remain inside the sector just captured. Once no defender remains there, the
    /// attacker is given a holding destination at the new frontline and waits for the timer.
    /// </summary>
    public bool TryGetAttackerTransitionDestination(GameObject attacker, out Vector3 destination)
    {
        destination = attacker != null ? attacker.transform.position : Vector3.zero;
        if (!isTransitioningSector || attacker == null || sectors == null ||
            currentSectorIndex < 0 || currentSectorIndex >= sectors.Length)
        {
            return false;
        }

        Sector completedSector = sectors[currentSectorIndex];
        GameObject nearestDefender = null;
        float nearestSqrDistance = float.MaxValue;

        GameObject[] defenders = GameObject.FindGameObjectsWithTag("Defender");
        foreach (GameObject defender in defenders)
        {
            if (defender == null) continue;

            if (completedSector.sectorBounds.size.sqrMagnitude > 0.01f &&
                !completedSector.sectorBounds.Contains(defender.transform.position))
            {
                continue;
            }

            float sqrDistance = (defender.transform.position - attacker.transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestDefender = defender;
            }
        }

        if (nearestDefender != null)
        {
            destination = nearestDefender.transform.position;
            return true;
        }

        destination = GetTransitionFrontlineHoldPoint(completedSector, attacker.transform.position);
        return true;
    }

    private Vector3 GetTransitionFrontlineHoldPoint(Sector completedSector, Vector3 attackerPosition)
    {
        if (completedSector == null || completedSector.sectorBounds.size.sqrMagnitude <= 0.01f)
        {
            return attackerPosition;
        }

        Vector3 directionTarget = completedSector.sectorBounds.center;
        int nextIndex = currentSectorIndex + 1;
        if (sectors != null && nextIndex >= 0 && nextIndex < sectors.Length)
        {
            Sector next = sectors[nextIndex];
            if (next != null && next.defenderBase != null)
            {
                Transform target = next.defenderBase.spawnPoint != null ? next.defenderBase.spawnPoint : next.defenderBase.transform;
                if (target != null) directionTarget = target.position;
            }
            else if (next != null && next.sectorBounds.size.sqrMagnitude > 0.01f)
            {
                directionTarget = next.sectorBounds.center;
            }
        }

        Vector3 edge = completedSector.sectorBounds.ClosestPoint(directionTarget);
        Vector3 inward = completedSector.sectorBounds.center - edge;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.01f) edge += inward.normalized * 1.5f;

        // Spread units slightly along the line so a full assault roster does not stack into one cube pile.
        int stable = GetStrategicAssignmentId(null) + Mathf.Abs(attackerPosition.GetHashCode());
        float lateral = ((stable % 7) - 3) * 0.65f;
        Vector3 forward = directionTarget - completedSector.sectorBounds.center;
        forward.y = 0f;
        Vector3 side = forward.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, forward.normalized) : Vector3.right;
        edge += side * lateral;

        return completedSector.sectorBounds.ClosestPoint(edge);
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
                    foreach (float matchDuration in TestDashboardOverlay.MatchDurations) averageDuration += matchDuration;
                    averageDuration /= TestDashboardOverlay.MatchDurations.Count;
                }

                int finishedMatches = TestDashboardOverlay.TotalAttackerWins + TestDashboardOverlay.TotalDefenderWins;
                float attackerWinRate = finishedMatches > 0 ? (float)TestDashboardOverlay.TotalAttackerWins / finishedMatches * 100f : 0f;
                Debug.Log($"🏁 BATCH TEST COMPLETED! Matches: {finishedMatches} | Attackers {TestDashboardOverlay.TotalAttackerWins} ({attackerWinRate:F1}%) - Defenders {TestDashboardOverlay.TotalDefenderWins} | Avg duration {averageDuration:F1}s");
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
                writer.WriteLine("Winner,Match Time (s),Attacker Tickets,Defender Tickets,Atk Deaths,Def Deaths,Avg Atk Lifespan (s),Avg Def Lifespan (s)");

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

        return true;
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

                int capturedIdx = (assignmentId / 4) % capturedPoints.Count;
                return capturedPoints[capturedIdx].transform;
            }
            else if (uncapturedPoints.Count > 0)
            {
                int idx = assignmentId % uncapturedPoints.Count;
                return uncapturedPoints[idx].transform;
            }

            int fallbackIdx = assignmentId % activePoints.Count;
            return activePoints[fallbackIdx].transform;
        }

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

            int secureIdx = (assignmentId / 4) % securePoints.Count;
            return securePoints[secureIdx].transform;
        }
        else if (threatenedPoints.Count > 0)
        {
            int idx = assignmentId % threatenedPoints.Count;
            return threatenedPoints[idx].transform;
        }

        int defenderFallbackIdx = assignmentId % activePoints.Count;
        return activePoints[defenderFallbackIdx].transform;
    }

    private int GetStrategicAssignmentId(GameObject unit)
    {
        if (unit == null) return 0;

        SquadMember squadMember = unit.GetComponent<SquadMember>();
        if (squadMember != null && squadMember.Squad != null)
        {
            string squadId = squadMember.Squad.SquadId;
            if (!string.IsNullOrEmpty(squadId)) return GetStablePositiveHash(squadId);
        }

        string fallbackIdentity = $"{unit.name}:{unit.transform.GetSiblingIndex()}";
        return GetStablePositiveHash(fallbackIdentity);
    }

    private int GetStablePositiveHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = (hash * 31) + value[i];
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
            bool isCurrent = i == currentSectorIndex;

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
