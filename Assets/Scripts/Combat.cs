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
        nextFireTime = Time.time + Random.Range(0f, 0.5f);
    }

    void Update()
    {
        FindTarget();

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (dist <= attackRange && HasLineOfSight(currentTarget))
            {
                if (turretTransform != null)
                {
                    Vector3 targetDir = currentTarget.transform.position - turretTransform.position;
                    targetDir.y = 0;

                    if (targetDir != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, Time.deltaTime * turretTurnSpeed);
                    }
                }
                else
                {
                    Vector3 lookDir = (currentTarget.transform.position - transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
                }

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
        Faction ownFaction = GetOwnFaction();
        bool currentTargetValid = IsValidTarget(currentTarget);
        bool currentTargetSpotted = currentTargetValid && IsSpottedForOwnFaction(currentTarget, ownFaction);

        if (currentTargetValid && currentTargetSpotted)
        {
            return;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies == null || enemies.Length == 0)
        {
            currentTarget = null;
            return;
        }

        List<GameObject> spottedEnemies = new List<GameObject>();
        List<GameObject> validEnemies = new List<GameObject>();

        foreach (GameObject enemy in enemies)
        {
            if (!IsValidTarget(enemy)) continue;

            validEnemies.Add(enemy);

            if (IsSpottedForOwnFaction(enemy, ownFaction))
            {
                spottedEnemies.Add(enemy);
            }
        }

        if (spottedEnemies.Count > 0)
        {
            currentTarget = PickNearest(spottedEnemies);
            return;
        }

        if (currentTargetValid)
        {
            return;
        }

        currentTarget = validEnemies.Count > 0
            ? validEnemies[Random.Range(0, validEnemies.Count)]
            : null;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;

        float distance = Vector3.Distance(transform.position, target.transform.position);
        return distance <= attackRange && HasLineOfSight(target);
    }

    private Faction GetOwnFaction()
    {
        if (CompareTag("Attacker")) return Faction.Attacker;
        if (CompareTag("Defender")) return Faction.Defender;
        return Faction.None;
    }

    private bool IsSpottedForOwnFaction(GameObject target, Faction ownFaction)
    {
        if (target == null || ownFaction == Faction.None) return false;

        SpottedTarget spotted = target.GetComponent<SpottedTarget>();
        return spotted != null && spotted.IsSpottedFor(ownFaction);
    }

    private GameObject PickNearest(List<GameObject> candidates)
    {
        GameObject nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null) continue;

            float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
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

        if (muzzleEffectPrefab != null && firePoint != null)
        {
            GameObject smoke = Instantiate(muzzleEffectPrefab, firePoint.position, firePoint.rotation);
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
