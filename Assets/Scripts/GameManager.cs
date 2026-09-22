using UnityEngine;
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
    public float testTimeMultiplier = 20f;
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
    private int startingAttackerTickets = 150;
    private int startingDefenderTickets = 100; 
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
    private int startingCommandXP;
    private int startingAttackerXP;
    private int startingDefenderXP;

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
    public bool IsMatchOver => isGameOver;
    public bool isTransitioningSector = false;

    public void UseSectors(Sector[] nextSectors)
    {
        sectors = nextSectors ?? new Sector[0];
        currentSectorIndex = 0;
        isTransitioningSector = false;
        sectorHoldTimer = 0f;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // SampleScene still stores an old 15-ticket test value. One wipe then ends the match
        // during the first sector. The match pool is the script default of 150.
        if (attackerTickets < 50) attackerTickets = 150;
        startingAttackerTickets = attackerTickets;
        startingDefenderTickets = defenderTickets;
        startingCommandXP = commandXP;
        startingAttackerXP = attackerXP;
        startingDefenderXP = defenderXP;
        
        logFilePath = Application.dataPath + "/MatchBalanceLogs.csv";

        currentSectorIndex = 0;

        if (TestDashboardOverlay.CurrentMatchNumber > 1) 
        {
            enableAutoTestMode = true; 
            StartCoroutine(StartAutoTest());
        }
        else if (enableAutoTestMode) 
        {
            StartCoroutine(StartAutoTest());
        }
        else 
        {
            // Time.timeScale = 0f;
        }
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
        BeginWorkshopTest();
    }

    public void BeginWorkshopPlay(Faction faction)
    {
        enableAutoTestMode = false;
        if (faction == Faction.None)
        {
            faction = UnityEngine.Random.value < 0.5f ? Faction.Attacker : Faction.Defender;
        }

        if (faction == Faction.Defender) SelectDefenderFaction();
        else SelectAttackerFaction();
        Time.timeScale = 1f;
    }

    public void BeginWorkshopTest()
    {
        enableAutoTestMode = true;
        if (MapSession.testRunsFinished == 0)
        {
            TestDashboardOverlay.ResetBatchStats();
            TestDashboardOverlay.CurrentMatchNumber = 1;
            TestDashboardOverlay.TargetMatchCount = Mathf.Max(1, MapSession.activeTestRuns);
        }

        SelectDefenderFaction();
        matchStartTime = Time.time;
        Time.timeScale = testTimeMultiplier;
    }

    public void PrepareForRematch()
    {
        StopAllCoroutines();
        isGameOver = false;
        isTransitioningSector = false;
        sectorHoldTimer = 0f;
        currentSectorIndex = 0;
        attackerTickets = startingAttackerTickets;
        defenderTickets = startingDefenderTickets;
        attackerDeaths = 0;
        defenderDeaths = 0;
        attackerTotalLifespan = 0f;
        defenderTotalLifespan = 0f;
        commandXP = startingCommandXP;
        attackerXP = startingAttackerXP;
        defenderXP = startingDefenderXP;
        playerFaction = Faction.None;
        enableAutoTestMode = false;
        Time.timeScale = 1f;

        DestroyTeam("Attacker");
        DestroyTeam("Defender");
        ResetAllSectorsCompletely();

        if (attackerSpawner != null) attackerSpawner.PrepareForRematch();
        if (defenderSpawner != null) defenderSpawner.PrepareForRematch();
        if (enemyDirector != null) enemyDirector.PrepareForRematch();
        if (SquadManager.Instance != null) SquadManager.Instance.ClearRostersForRematch();
        if (SquadAIController.Instance != null) SquadAIController.Instance.ClearForRematch();
        if (SelectionManager.Instance != null) SelectionManager.Instance.ClearSelection();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideGameOver();
            UIManager.Instance.ClearSectorStatuses();
            UIManager.Instance.UpdateTickets(attackerTickets);
        }
    }

    private void DestroyTeam(string teamTag)
    {
        GameObject[] units = GameObject.FindGameObjectsWithTag(teamTag);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null) Destroy(units[i]);
        }
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
        CheckSectorProgression();
        CheckWinConditions();
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
        BreakthroughFrontlineSystem.EnsureInstance();
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
            Debug.Log($"🎯 SECTOR COMPLETED: {currentSector.sectorName} ({verifiedCapturedPoints}/{totalPoints} points held at 100% for {sectorHoldRequiredDuration}s). Sector {currentSectorIndex + 1} of {sectors.Length}.");

            CaptureSectorScreenshot(currentSector.sectorName, pointStatusSummary);

            int nextSectorIndex = currentSectorIndex + 1;
            if (nextSectorIndex < sectors.Length)
            {
                AwardSectorCaptureTickets();
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

    private void AwardSectorCaptureTickets()
    {
        attackerTickets += sectorCaptureTicketBonus;
        if (UIManager.Instance != null) UIManager.Instance.UpdateTickets(attackerTickets);
    }

    private IEnumerator HandleSectorTransition()
    {
        isTransitioningSector = true;
        BreakthroughFrontlineSystem.EnsureInstance();

        int completedSectorIndex = currentSectorIndex;
        int nextSectorIndex = completedSectorIndex + 1;
        if (sectors == null || nextSectorIndex >= sectors.Length)
        {
            isTransitioningSector = false;
            yield break;
        }

        Sector completedSector = sectors[completedSectorIndex];
        Sector nextSector = sectors[nextSectorIndex];

        if (completedSector != null && completedSector.capturePoints != null)
        {
            foreach (CapturePoint cp in completedSector.capturePoints)
            {
                if (cp != null) cp.LockCapturePoint();
            }
        }

        if (nextSector != null && nextSector.capturePoints != null)
        {
            foreach (CapturePoint cp in nextSector.capturePoints)
            {
                if (cp == null) continue;
                cp.activeDuringSectorIndex = nextSectorIndex;
                cp.ResetCapturePoint();
            }
        }

        if (nextSector != null && nextSector.defenderBase != null)
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

        ReplenishTransitionRosters(completedSectorIndex, nextSectorIndex);
        currentSectorIndex = nextSectorIndex;

        if (attackerSpawner != null && nextSector != null && nextSector.attackerBase != null)
        {
            Transform attSpawn = nextSector.attackerBase.spawnPoint != null ? nextSector.attackerBase.spawnPoint : nextSector.attackerBase.transform;
            attackerSpawner.transform.position = attSpawn.position;
        }

        if (defenderSpawner != null && nextSector != null && nextSector.defenderBase != null)
        {
            Transform defSpawn = nextSector.defenderBase.spawnPoint != null ? nextSector.defenderBase.spawnPoint : nextSector.defenderBase.transform;
            defenderSpawner.transform.position = defSpawn.position;
        }

        if (SquadManager.Instance != null) SquadManager.Instance.ClearStrategicObjectives();
        if (SquadAIController.Instance != null) SquadAIController.Instance.ClearForRematch();

        isTransitioningSector = false;

        if (UIManager.Instance != null && nextSector != null)
        {
            UIManager.Instance.ClearSectorStatuses();
            UIManager.Instance.ShowSectorCapturedBanner(nextSector.sectorAnnouncementText);
        }

        AutonomousUnit[] allUnits = FindObjectsByType<AutonomousUnit>(FindObjectsInactive.Exclude);
        foreach (AutonomousUnit unit in allUnits)
        {
            if (unit != null) unit.UpdateDestination();
        }
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

            if (completedSector != null && completedSector.sectorBounds.size.sqrMagnitude > 0.01f &&
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

        if (enableAutoTestMode)
        {
            float duration = Time.time - matchStartTime;
            LogMatchData(winner);
            TestDashboardOverlay.RecordMatchCompleted(winner, duration);

            int runLimit = MapSession.activeTestRuns;
            if (runLimit > 0)
            {
                MapSession.testRunsFinished++;
                if (MapSession.testRunsFinished < runLimit)
                {
                    TestDashboardOverlay.CurrentMatchNumber = MapSession.testRunsFinished + 1;
                    MapSession.continueTest = true;
                    return;
                }

                if (UIManager.Instance != null) UIManager.Instance.ShowGameOver($"{winner.ToUpper()} WIN!");
                Debug.Log($"🏁 BATCH TEST RUN COMPLETED! Total Matches: {MapSession.testRunsFinished}. Final Score: Attackers {TestDashboardOverlay.TotalAttackerWins} - Defenders {TestDashboardOverlay.TotalDefenderWins}");
                Time.timeScale = 0f;
                return;
            }

            if (MapSession.returnAfterMatch)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowGameOver($"{winner.ToUpper()} WIN!");
                Time.timeScale = 0f;
                return;
            }

            TestDashboardOverlay.CurrentMatchNumber++;
            MapSession.continueTest = true;
            return;
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowGameOver($"{winner.ToUpper()} WIN!");
        Time.timeScale = 0f;
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