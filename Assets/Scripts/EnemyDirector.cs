using System.Collections;
using UnityEngine;

public class EnemyDirector : MonoBehaviour
{
    [Header("Legacy Director Status")]
    [Tooltip("Legacy attacker wave spawner from the original prototype. Leave disabled while UnitSpawner/AICommander own reinforcement and specialist spawning.")]
    public bool enableLegacySpawning = false;

    [Tooltip("Runtime status. Activation requests are ignored unless legacy spawning is explicitly enabled.")]
    public bool isDirectorActive = false;

    [Header("Wave Settings")]
    public float timeBetweenWaves = 12f;
    public int unitsPerWave = 3;

    [Header("AI Resources")]
    public int aiTickets = 50;
    public int unitCost = 1;
    public float spawnSpreadRadius = 2.5f;

    [Header("Unit Prefabs")]
    public GameObject assaultAttackerPrefab;
    public GameObject engineerAttackerPrefab;

    void Start()
    {
        if (!enableLegacySpawning)
        {
            isDirectorActive = false;
        }

        StartCoroutine(WaveCycle());
    }

    IEnumerator WaveCycle()
    {
        yield return new WaitForSeconds(5f);

        while (true)
        {
            if (enableLegacySpawning &&
                isDirectorActive &&
                aiTickets >= unitCost &&
                GameManager.Instance != null)
            {
                int spawnCount = Mathf.Min(unitsPerWave, aiTickets / unitCost);

                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnUnit();
                    yield return new WaitForSeconds(0.5f);
                }
            }

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnUnit()
    {
        if (!enableLegacySpawning || !isDirectorActive) return;
        if (aiTickets < unitCost) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.sectors == null) return;

        int sectorIndex = GameManager.Instance.currentSectorIndex;
        if (sectorIndex < 0 || sectorIndex >= GameManager.Instance.sectors.Length) return;

        Sector sector = GameManager.Instance.sectors[sectorIndex];
        if (sector == null || sector.attackerBase == null) return;

        Transform baseSpawnPoint = sector.attackerBase.spawnPoint != null
            ? sector.attackerBase.spawnPoint
            : sector.attackerBase.transform;

        if (baseSpawnPoint == null) return;

        Vector2 randomCircle = Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 randomOffset = new Vector3(randomCircle.x, 0f, randomCircle.y);
        Vector3 safeSpawnLocation = baseSpawnPoint.position + randomOffset;

        GameObject prefabToSpawn = Random.value > 0.3f
            ? assaultAttackerPrefab
            : engineerAttackerPrefab;

        if (prefabToSpawn == null) return;

        Instantiate(prefabToSpawn, safeSpawnLocation, baseSpawnPoint.rotation);
        aiTickets -= unitCost;
    }

    public void ActivateEnemyDirector()
    {
        if (!enableLegacySpawning)
        {
            isDirectorActive = false;
            Debug.Log("LEGACY ENEMY DIRECTOR: Activation ignored. UnitSpawner/AICommander are the active spawning authorities.");
            return;
        }

        isDirectorActive = true;
    }
}
