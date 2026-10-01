using UnityEngine;

/// <summary>
/// Recon support ability. Periodically scans visible enemies and marks a small number
/// of them as spotted for this Recon's faction. Spotting is temporary and requires line of sight.
/// </summary>
public sealed class ReconSpotter : MonoBehaviour
{
    [Header("Spotting")]
    [SerializeField] private float spottingRadius = 30f;
    [SerializeField] private float scanInterval = 1.5f;
    [SerializeField] private float spottedDuration = 5f;
    [SerializeField] private int maxTargetsPerScan = 4;

    private float nextScanTime;
    private Faction observingFaction = Faction.None;
    private string enemyTag;

    private void Start()
    {
        ResolveFaction();
        nextScanTime = Time.time + Random.Range(0f, Mathf.Max(0.1f, scanInterval));
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isTransitioningSector) return;
        if (Time.time < nextScanTime) return;

        nextScanTime = Time.time + Mathf.Max(0.1f, scanInterval);
        ScanForEnemies();
    }

    private void ResolveFaction()
    {
        if (CompareTag("Attacker"))
        {
            observingFaction = Faction.Attacker;
            enemyTag = "Defender";
        }
        else if (CompareTag("Defender"))
        {
            observingFaction = Faction.Defender;
            enemyTag = "Attacker";
        }
        else
        {
            observingFaction = Faction.None;
            enemyTag = string.Empty;
        }
    }

    private void ScanForEnemies()
    {
        if (observingFaction == Faction.None || string.IsNullOrEmpty(enemyTag))
        {
            ResolveFaction();
            if (observingFaction == Faction.None || string.IsNullOrEmpty(enemyTag)) return;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies == null || enemies.Length == 0) return;

        int spottedCount = 0;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance > spottingRadius) continue;
            if (!HasLineOfSight(enemy)) continue;

            SpottedTarget spottedTarget = SpottedTarget.GetOrCreate(enemy);
            if (spottedTarget == null) continue;

            spottedTarget.MarkSpotted(observingFaction, spottedDuration);
            spottedCount++;

            if (spottedCount >= Mathf.Max(1, maxTargetsPerScan))
            {
                break;
            }
        }
    }

    private bool HasLineOfSight(GameObject target)
    {
        if (target == null) return false;

        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = target.transform.position + Vector3.up * 0.5f;
        Vector3 direction = targetPos - rayStart;
        float distanceToTarget = direction.magnitude;

        if (distanceToTarget <= 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, direction.normalized, distanceToTarget);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.gameObject == target) continue;
            if (hit.collider.GetComponent<Projectile>() != null) continue;
            if (hit.collider.CompareTag("Cover") || hit.collider.gameObject.isStatic) return false;
        }

        return true;
    }
}
