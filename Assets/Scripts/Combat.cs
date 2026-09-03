using UnityEngine;
using System.Collections.Generic;

public class Combat : MonoBehaviour
{
    [Header("Targeting")]
    public string enemyTag = "Attacker";
    public float attackRange = 12f;

    [Header("Weapon Stats")]
    public GameObject projectilePrefab;
    public float fireRate = 1.2f;
    public float damagePerShot = 20f;
    public float accuracySpread = 1.5f; 

    [Header("Turret Tracking (Optional)")]

    [Tooltip("The smoke puff prefab to spawn when firing")]
    public GameObject muzzleEffectPrefab;
    [Tooltip("Assign the Turret mesh here to make it rotate independently")]
    public Transform turretTransform; 
    [Tooltip("Assign an empty object at the tip of the barrel")]
    public Transform firePoint; 
    public float turretTurnSpeed = 8f;

    private float nextFireTime = 0f;
    private GameObject currentTarget;

    void Start()
    {
        // Stagger the first shot so units don't all fire on the exact same frame
        nextFireTime = Time.time + Random.Range(0f, 0.5f);
    }

    void Update()
    {
        FindTarget();

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            
            // Double check range and Line of Sight before firing
            if (dist <= attackRange && HasLineOfSight(currentTarget))
            {
                // --- NEW TURRET TRACKING LOGIC ---
                if (turretTransform != null)
                {
                    // Calculate direction to target, locking the Y axis so the turret stays flat
                    Vector3 targetDir = currentTarget.transform.position - turretTransform.position;
                    targetDir.y = 0; 

                    if (targetDir != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                        // Smoothly rotate the turret toward the target
                        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, Time.deltaTime * turretTurnSpeed);
                    }
                }
                else
                {
                    // Fallback: If no turret is assigned, rotate the whole unit like before
                    Vector3 lookDir = (currentTarget.transform.position - transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
                }

                // Shoot when ready
                if (Time.time >= nextFireTime)
                {
                    Shoot();
                    nextFireTime = Time.time + (1f / fireRate) + Random.Range(-0.1f, 0.1f);
                }
            }
        }
    }

    void FindTarget()
    {
        if (currentTarget != null && currentTarget.activeInHierarchy)
        {
            float currentDist = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (currentDist <= attackRange && HasLineOfSight(currentTarget))
            {
                return; 
            }
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies == null || enemies.Length == 0)
        {
            currentTarget = null;
            return;
        }

        List<GameObject> validEnemies = new List<GameObject>();
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            
            if (dist <= attackRange && HasLineOfSight(enemy))
            {
                validEnemies.Add(enemy);
            }
        }

        if (validEnemies.Count > 0)
        {
            currentTarget = validEnemies[Random.Range(0, validEnemies.Count)];
        }
        else
        {
            currentTarget = null;
        }
    }

    bool HasLineOfSight(GameObject target)
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = target.transform.position + Vector3.up * 0.5f;
        Vector3 dir = targetPos - rayStart;
        float distanceToTarget = dir.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, dir.normalized, distanceToTarget);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject || hit.collider.gameObject == target) continue;
            if (hit.collider.GetComponent<Projectile>() != null) continue;
            if (hit.collider.CompareTag("Cover") || hit.collider.gameObject.isStatic) return false;
        }
        
        return true;
    }

    void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        // --- NEW FIRE LOCATION LOGIC ---
        // If a FirePoint exists, use it. Otherwise, fall back to the old math.
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + (transform.forward * 1.0f) + (Vector3.up * 0.5f);
        Vector3 aimDir = firePoint != null ? firePoint.forward : (currentTarget.transform.position - spawnPos);
        
        if (aimDir == Vector3.zero) aimDir = transform.forward;

        Quaternion shootRotation;
        Vector3 normalizedAim = aimDir.normalized;
        
        if (normalizedAim != Vector3.up && normalizedAim != Vector3.down)
        {
            Vector3 spread = Random.insideUnitSphere * (accuracySpread * 0.05f);
            shootRotation = Quaternion.LookRotation(normalizedAim + spread);
        }
        else
        {
            shootRotation = transform.rotation;
        }

        // Spawn the smoke puff at the tip of the barrel
        if (muzzleEffectPrefab != null && firePoint != null)
        {
            GameObject smoke = Instantiate(muzzleEffectPrefab, firePoint.position, firePoint.rotation);
            // Automatically clean up the smoke object so it doesn't clutter memory
            Destroy(smoke, 1f); 
        }

        GameObject proj = Instantiate(projectilePrefab, spawnPos, shootRotation);
        
        Projectile projScript = proj.GetComponent<Projectile>();
        if (projScript != null)
        {
            projScript.Initialize(currentTarget.transform, enemyTag, damagePerShot);
        }
    }
}