using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public bool isDefenderSpawner = false;
    public GameObject assaultPrefab;
    public int initialSpawnCount = 15;
    
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

    public void BeginSpawning()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        string teamName = isDefenderSpawner ? "Defenders" : "Attackers";
        
        Debug.Log($"🔢 SPAWN COUNT CHECK: Spawning {initialSpawnCount} units for {teamName}...");

        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnSingleUnit();
        }
    }

    public void SpawnWave(int count)
    {
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
                
                for (int i = 0; i < defenderWaveSize; i++)
                {
                    if (GameManager.Instance != null && GameManager.Instance.SpendTickets(false, 1))
                    {
                        SpawnSingleUnit();
                    }
                    else
                    {
                        break; 
                    }
                }
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
        Instantiate(assaultPrefab, finalPos, Quaternion.identity);
    }
}