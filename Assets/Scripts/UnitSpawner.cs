using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public bool isDefenderSpawner = false;
    public GameObject assaultPrefab;
    public int initialSpawnCount = 15;

    [Header("Reinforcements")]
    public float attackerRespawnDelay = 5f;

    [Tooltip("How often the defender spawner checks whether Assault reinforcements are needed.")]
    public float defenderAutoSpawnInterval = 10f;

    [Tooltip("Maximum number of defender Assault units that one defender spawner will maintain alive at once.")]
    [Min(1)] public int defenderAssaultPopulationCap = 16;

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

        string teamName = isDefenderSpawner ? "Defenders" : "Attackers";

        Debug.Log($"🔢 SPAWN COUNT CHECK: Spawning {initialSpawnCount} units for {teamName}...");

        if (isDefenderSpawner)
        {
            SpawnDefenderAssaultsUpToLimit(initialSpawnCount);
            return;
        }

        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnSingleUnit();
        }
    }

    public void SpawnWave(int count)
    {
        if (count <= 0) return;

        if (isDefenderSpawner)
        {
            int spawned = SpawnDefenderAssaultsUpToLimit(count);
            Debug.Log($"🌊 DEFENDER REINFORCEMENT: Requested {count}, deployed {spawned}, alive Assaults {GetAliveDefenderAssaultCount()}/{GetDefenderAssaultCap()}.");
            return;
        }

        Debug.Log($"🌊 SPAWN WAVE: Deploying {count} reinforcements!");
        for (int i = 0; i < count; i++)
        {
            SpawnSingleUnit();
        }
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
                SpawnDefenderAssaultsUpToLimit(defenderWaveSize);
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
        if (GameManager.Instance != null && GameManager.Instance.SpendTickets(true, 1))
        {
            SpawnSingleUnit();
        }
    }

    private int SpawnDefenderAssaultsUpToLimit(int requestedCount)
    {
        if (!isDefenderSpawner || requestedCount <= 0) return 0;

        int cap = GetDefenderAssaultCap();
        int alive = GetAliveDefenderAssaultCount();
        int availableSlots = Mathf.Max(0, cap - alive);
        int unitsToSpawn = Mathf.Min(requestedCount, availableSlots);

        int spawned = 0;
        for (int i = 0; i < unitsToSpawn; i++)
        {
            if (GameManager.Instance != null && !GameManager.Instance.SpendTickets(false, 1))
            {
                break;
            }

            SpawnSingleUnit();
            spawned++;
        }

        return spawned;
    }

    private int GetDefenderAssaultCap()
    {
        return Mathf.Max(1, defenderAssaultPopulationCap);
    }

    private int GetAliveDefenderAssaultCount()
    {
        GameObject[] defenders = GameObject.FindGameObjectsWithTag("Defender");
        int count = 0;

        foreach (GameObject defender in defenders)
        {
            if (defender == null) continue;

            UnitClassIdentity identity = defender.GetComponent<UnitClassIdentity>();
            if (identity == null || identity.Class == UnitClass.Assault)
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

        if (GameManager.Instance != null && GameManager.Instance.sectors != null && GameManager.Instance.sectors.Length > GameManager.Instance.currentSectorIndex)
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
            else
            {
                if (currentSector.attackerBase != null && currentSector.attackerBase.spawnPoint != null)
                {
                    spawnPos = currentSector.attackerBase.spawnPoint.position;
                }
            }
        }

        Vector2 randomOffset = Random.insideUnitCircle * 8f;
        Vector3 finalPos = spawnPos + new Vector3(randomOffset.x, 0, randomOffset.y);
        GameObject spawnedUnit = Instantiate(assaultPrefab, finalPos, Quaternion.identity);

        UnitClassIdentity.Ensure(spawnedUnit, UnitClass.Assault);

        SquadManager squadManager = SquadManager.EnsureInstance();
        if (squadManager != null)
        {
            squadManager.RegisterAssaultUnit(spawnedUnit);
        }
    }
}
