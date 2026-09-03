using System.Collections;
using UnityEngine;

public class EnemyDirector : MonoBehaviour
{
    [Header("Director Status")]
    public bool isDirectorActive = false; 

    [Header("Wave Settings")]
    public float timeBetweenWaves = 12f;
    public int unitsPerWave = 3; 
    
    [Header("AI Resources")]
    public int aiTickets = 50;
    public int unitCost = 1;
    public float spawnSpreadRadius = 2.5f; // How far apart units spawn to prevent physics explosions

    [Header("Unit Prefabs")]
    public GameObject assaultAttackerPrefab;
    public GameObject engineerAttackerPrefab;

    void Start()
    {
        StartCoroutine(WaveCycle());
    }

    IEnumerator WaveCycle()
    {
        yield return new WaitForSeconds(5f);

        while (true)
        {
            if (isDirectorActive && aiTickets >= unitCost && GameManager.Instance != null)
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
        if (aiTickets < unitCost) return;

        // 1. Get the current sector's single attacker spawn point from the GameManager
        int sectorIndex = GameManager.Instance.currentSectorIndex;
        Transform baseSpawnPoint = GameManager.Instance.sectors[sectorIndex].attackerBase.spawnPoint;

        if (baseSpawnPoint == null) return;

        // 2. Create a random offset so they don't spawn inside each other
        Vector2 randomCircle = Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
        Vector3 safeSpawnLocation = baseSpawnPoint.position + randomOffset;

        // 3. Pick the unit
        GameObject prefabToSpawn = (Random.value > 0.3f) ? assaultAttackerPrefab : engineerAttackerPrefab;

        // 4. Spawn it at the safe, slightly offset location
        Instantiate(prefabToSpawn, safeSpawnLocation, baseSpawnPoint.rotation);
        aiTickets -= unitCost;
    }
    
    public void ActivateEnemyDirector()
    {
        isDirectorActive = true;
    }
}