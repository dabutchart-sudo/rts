using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public bool isDefenderSpawner = false;
    public GameObject assaultPrefab;
    public int initialSpawnCount = 15;
    
    [Header("Assault Population Caps")]
    [Tooltip("Maximum number of attacker assault troops alive at once.")]
    [Min(1)] public int attackerAssaultPopulationCap = 16;

    [Tooltip("Maximum number of defender assault troops alive at once.")]
    [Min(1)] public int defenderAssaultPopulationCap = 16;

    [Header("Reinforcements")]
    public float attackerRespawnDelay = 5f;
    
    [Tooltip("How often a new defender wave spawns to reinforce the point.")]
    public float defenderAutoSpawnInterval = 10f; 
    
    [Tooltip("How many defenders spawn together as a reinforcement squad.")]
    public int defenderWaveSize = 5; 

    private float defenderTimer = 0f;
    private bool hasSpawned = false;

    void Awake()
    {
        hasSpawned = false;
    }

    public void PrepareForRematch()
    {
        CancelInvoke();
        hasSpawned = false;
        defenderTimer = 0f;
    }

    public void BeginSpawning()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        int target = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int sectorIndex = GameManager.Instance != null ? GameManager.Instance.currentSectorIndex : 0;
        int spawned = SpawnAssaultsUpToLimit(target, false, sectorIndex, false);
        string teamName = isDefenderSpawner ? "Defenders" : "Attackers";

        Debug.Log($"🔢 SPAWN COUNT CHECK: Deployed {spawned} Assaults for {teamName}; target population {target}.");
    }

    public void SpawnWave(int count)
    {
        if (count <= 0) return;

        int sectorIndex = GameManager.Instance != null ? GameManager.Instance.currentSectorIndex : 0;
        int spawned = SpawnAssaultsUpToLimit(count, false, sectorIndex, true);
        int alive = GetAliveAssaultCount();
        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        string teamName = isDefenderSpawner ? "DEFENDER" : "ATTACKER";

        Debug.Log($"🌊 {teamName} REINFORCEMENT: Requested {count}, deployed {spawned}, alive Assaults {alive}/{cap}.");
    }

    public int ReplenishAssaultsForTransition(int spawnSectorIndex, bool preferOwnedCapturePoints)
    {
        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int before = GetAliveAssaultCount();
        int missing = Mathf.Max(0, cap - before);
        bool spendTickets = !isDefenderSpawner;
        int spawned = SpawnAssaultsUpToLimit(missing, spendTickets, spawnSectorIndex, preferOwnedCapturePoints);
        int after = GetAliveAssaultCount();
        string teamName = isDefenderSpawner ? "DEFENDER" : "ATTACKER";
        string funding = isDefenderSpawner ? "the defender pool" : "attacker tickets";

        Debug.Log($"🚩 {teamName} TRANSITION READY: Preserved {before} surviving Assaults, replenished {spawned} using {funding}, ready {after}/{cap}.");
        return spawned;
    }

    void Update()
    {
        if (!hasSpawned) return;

        if (GameManager.Instance != null && GameManager.Instance.isTransitioningSector) return;

        if (isDefenderSpawner)
        {
            defenderTimer += Time.deltaTime;
            if (defenderTimer >= defenderAutoSpawnInterval)
            {
                defenderTimer = 0f;
                int sectorIndex = GameManager.Instance != null ? GameManager.Instance.currentSectorIndex : 0;
                SpawnAssaultsUpToLimit(defenderWaveSize, false, sectorIndex, true);
            }
        }
    }

    public void RespawnAssaultUnit()
    {
        if (isDefenderSpawner) return;
        Invoke(nameof(SpawnAssaultDelayed), attackerRespawnDelay);
    }

    void SpawnAssaultDelayed()
    {
        int sectorIndex = GameManager.Instance != null ? GameManager.Instance.currentSectorIndex : 0;
        SpawnAssaultsUpToLimit(1, true, sectorIndex, true);
    }

    int SpawnAssaultsUpToLimit(int requestedCount, bool spendTicket, int spawnSectorIndex, bool preferOwnedCapturePoints)
    {
        if (requestedCount <= 0) return 0;

        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int alive = GetAliveAssaultCount();
        int unitsToSpawn = Mathf.Min(requestedCount, Mathf.Max(0, cap - alive));

        int spawned = 0;
        for (int i = 0; i < unitsToSpawn; i++)
        {
            if (spendTicket && GameManager.Instance != null && !GameManager.Instance.SpendTickets(!isDefenderSpawner, 1))
            {
                break;
            }

            SpawnSingleUnit(spawnSectorIndex, preferOwnedCapturePoints);
            spawned++;
        }

        return spawned;
    }

    public int GetAttackerAssaultCap()
    {
        return Mathf.Max(1, attackerAssaultPopulationCap);
    }

    public int GetDefenderAssaultCap()
    {
        return Mathf.Max(1, defenderAssaultPopulationCap);
    }

    public int GetAliveAssaultCount()
    {
        string factionTag = isDefenderSpawner ? "Defender" : "Attacker";
        GameObject[] units = GameObject.FindGameObjectsWithTag(factionTag);
        int count = 0;

        foreach (GameObject unit in units)
        {
            if (unit == null) continue;
            AutonomousUnit autonomous = unit.GetComponent<AutonomousUnit>();
            if (autonomous != null && autonomous.isTank) continue;
            count++;
        }

        return count;
    }

    void SpawnSingleUnit(int spawnSectorIndex, bool preferOwnedCapturePoints)
    {
        if (assaultPrefab == null) return;

        Vector3 spawnPos = ResolveSpawnPosition(spawnSectorIndex, preferOwnedCapturePoints);
        Vector2 randomOffset = Random.insideUnitCircle * 2.5f;
        Vector3 finalPos = spawnPos + new Vector3(randomOffset.x, 0f, randomOffset.y);
        GameObject spawnedUnit = Instantiate(assaultPrefab, finalPos, Quaternion.identity);

        SquadManager squadManager = SquadManager.EnsureInstance();
        if (squadManager != null)
        {
            squadManager.RegisterAssaultUnit(spawnedUnit);
        }
    }

    Vector3 ResolveSpawnPosition(int sectorIndex, bool preferOwnedCapturePoints)
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null ||
            sectorIndex < 0 || sectorIndex >= GameManager.Instance.sectors.Length)
        {
            return transform.position;
        }

        Sector sector = GameManager.Instance.sectors[sectorIndex];
        Faction faction = isDefenderSpawner ? Faction.Defender : Faction.Attacker;

        if (preferOwnedCapturePoints)
        {
            Transform captureSpawn = GetOwnedCapturePointSpawn(sector, faction);
            if (captureSpawn != null) return captureSpawn.position;
        }

        BaseZone factionBase = isDefenderSpawner ? sector.defenderBase : sector.attackerBase;
        if (factionBase != null)
        {
            Transform baseSpawn = factionBase.spawnPoint != null ? factionBase.spawnPoint : factionBase.transform;
            if (baseSpawn != null) return baseSpawn.position;
        }

        return transform.position;
    }

    static Transform GetOwnedCapturePointSpawn(Sector sector, Faction faction)
    {
        if (sector == null || sector.capturePoints == null) return null;

        var owned = new System.Collections.Generic.List<CapturePoint>();
        foreach (CapturePoint cp in sector.capturePoints)
        {
            if (cp != null && cp.IsControlledBy(faction)) owned.Add(cp);
        }

        if (owned.Count == 0) return null;
        return owned[Random.Range(0, owned.Count)].GetNextAvailableSpawnPoint();
    }
}
