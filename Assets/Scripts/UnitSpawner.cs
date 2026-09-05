using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public bool isDefenderSpawner = false;
    public GameObject assaultPrefab;
    public int initialSpawnCount = 15;

    [Header("Assault Population Caps")]
    [Tooltip("Maximum number of attacker Assault units alive at once.")]
    [Min(1)] public int attackerAssaultPopulationCap = 16;

    [Tooltip("Maximum number of defender Assault units alive at once.")]
    [Min(1)] public int defenderAssaultPopulationCap = 16;

    [Header("Reinforcements")]
    public float attackerRespawnDelay = 5f;

    [Tooltip("How often the defender spawner checks whether Assault reinforcements are needed.")]
    public float defenderAutoSpawnInterval = 10f;

    [Tooltip("Maximum number of defender Assault units added during one reinforcement check.")]
    [Min(1)] public int defenderWaveSize = 5;

    private float defenderTimer = 0f;
    private bool hasSpawned = false;

    void Awake()
    {
        hasSpawned = false;
    }

    public void BeginSpawning()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        int target = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int spawned = SpawnAssaultsUpToLimit(target, false);
        string teamName = isDefenderSpawner ? "Defenders" : "Attackers";

        Debug.Log($"🔢 SPAWN COUNT CHECK: Deployed {spawned} Assaults for {teamName}; target population {target}.");
    }

    public void SpawnWave(int count)
    {
        if (count <= 0) return;

        int spawned = SpawnAssaultsUpToLimit(count, false);
        int alive = GetAliveAssaultCount();
        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        string teamName = isDefenderSpawner ? "DEFENDER" : "ATTACKER";

        Debug.Log($"🌊 {teamName} REINFORCEMENT: Requested {count}, deployed {spawned}, alive Assaults {alive}/{cap}.");
    }

    public int ReplenishAssaultsForSectorStart()
    {
        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int before = GetAliveAssaultCount();
        int missing = Mathf.Max(0, cap - before);

        bool spendTickets = !isDefenderSpawner;
        int spawned = SpawnAssaultsUpToLimit(missing, spendTickets);

        int after = GetAliveAssaultCount();
        string teamName = isDefenderSpawner ? "DEFENDER" : "ATTACKER";
        string funding = isDefenderSpawner ? "unlimited defender reinforcements" : "attacker tickets";

        Debug.Log($"🚩 {teamName} SECTOR START: Preserved {before} surviving Assaults, replenished {spawned} using {funding}, ready {after}/{cap}.");
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
                SpawnAssaultsUpToLimit(defenderWaveSize, true);
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
        SpawnAssaultsUpToLimit(1, true);
    }

    private int SpawnAssaultsUpToLimit(int requestedCount, bool spendTicket)
    {
        if (requestedCount <= 0) return 0;

        int cap = isDefenderSpawner ? GetDefenderAssaultCap() : GetAttackerAssaultCap();
        int alive = GetAliveAssaultCount();
        int availableSlots = Mathf.Max(0, cap - alive);
        int unitsToSpawn = Mathf.Min(requestedCount, availableSlots);

        int spawned = 0;
        for (int i = 0; i < unitsToSpawn; i++)
        {
            if (spendTicket && GameManager.Instance != null)
            {
                bool isAttacker = !isDefenderSpawner;
                if (!GameManager.Instance.SpendTickets(isAttacker, 1))
                {
                    break;
                }
            }

            SpawnSingleUnit();
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

            UnitCategoryIdentity categoryIdentity = unit.GetComponent<UnitCategoryIdentity>();
            if (categoryIdentity != null && categoryIdentity.Category != UnitCategory.Infantry)
            {
                continue;
            }

            UnitClassIdentity classIdentity = unit.GetComponent<UnitClassIdentity>();
            if (classIdentity != null && classIdentity.Class == UnitClass.Assault)
            {
                count++;
            }
        }

        return count;
    }

    void SpawnSingleUnit()
    {
        if (assaultPrefab == null) return;

        Vector3 spawnPos = transform.position;

        if (GameManager.Instance != null &&
            GameManager.Instance.sectors != null &&
            GameManager.Instance.sectors.Length > GameManager.Instance.currentSectorIndex)
        {
            Sector currentSector = GameManager.Instance.sectors[GameManager.Instance.currentSectorIndex];

            if (isDefenderSpawner)
            {
                if (currentSector.defenderBase != null && currentSector.defenderBase.spawnPoint != null)
                {
                    spawnPos = currentSector.defenderBase.spawnPoint.position;
                }
                else if (currentSector.attackerBase != null && currentSector.attackerBase.spawnPoint != null)
                {
                    spawnPos = -currentSector.attackerBase.spawnPoint.position;
                    spawnPos.y = currentSector.attackerBase.spawnPoint.position.y;
                }
            }
            else if (currentSector.attackerBase != null && currentSector.attackerBase.spawnPoint != null)
            {
                spawnPos = currentSector.attackerBase.spawnPoint.position;
            }
        }

        Vector2 randomOffset = Random.insideUnitCircle * 8f;
        Vector3 finalPos = spawnPos + new Vector3(randomOffset.x, 0, randomOffset.y);
        GameObject spawnedUnit = Instantiate(assaultPrefab, finalPos, Quaternion.identity);

        UnitCategoryIdentity.Ensure(spawnedUnit, UnitCategory.Infantry);
        UnitClassIdentity.Ensure(spawnedUnit, UnitClass.Assault);

        UnitCombatPresentation presentation = UnitCombatPresentation.Ensure(spawnedUnit);
        if (presentation != null)
        {
            presentation.PlaySpawnDrop();
        }

        SquadManager squadManager = SquadManager.EnsureInstance();
        if (squadManager != null)
        {
            squadManager.RegisterAssaultUnit(spawnedUnit);
        }
    }
}
