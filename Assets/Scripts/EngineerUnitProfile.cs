using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives Engineer infantry an automatic anti-vehicle rocket launcher.
/// The Engineer keeps its normal rifle combat, but periodically engages enemy vehicles
/// with a separate, slower, harder-hitting explosive projectile.
/// </summary>
public sealed class EngineerUnitProfile : MonoBehaviour
{
    [Header("Rocket Launcher")]
    [SerializeField] private float rocketRange = 34f;
    [SerializeField] private float rocketCooldown = 5f;
    [SerializeField] private float rocketDamage = 120f;
    [SerializeField] private float rocketSpeed = 26f;
    [SerializeField] private float rocketVisualScale = 0.22f;

    [Header("Rocket Blast")]
    [SerializeField] private float blastRadius = 4.5f;
    [SerializeField, Range(0f, 1f)] private float edgeDamageMultiplier = 0.20f;

    private float nextRocketTime;

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isTransitioningSector) return;
        if (Time.time < nextRocketTime) return;

        GameObject target = FindNearestEnemyVehicle();
        if (target == null) return;

        FireRocket(target);
        nextRocketTime = Time.time + Mathf.Max(0.1f, rocketCooldown);
    }

    private GameObject FindNearestEnemyVehicle()
    {
        string enemyTag;
        if (CompareTag("Attacker")) enemyTag = "Defender";
        else if (CompareTag("Defender")) enemyTag = "Attacker";
        else return null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        GameObject nearest = null;
        float bestDistance = rocketRange;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            UnitCategoryIdentity category = enemy.GetComponent<UnitCategoryIdentity>();
            if (category == null || category.Category != UnitCategory.Vehicle) continue;

            Health health = enemy.GetComponent<Health>();
            if (health == null) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance > bestDistance) continue;
            if (!HasLineOfSight(enemy)) continue;

            bestDistance = distance;
            nearest = enemy;
        }

        return nearest;
    }

    private bool HasLineOfSight(GameObject target)
    {
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Vector3 destination = target.transform.position + Vector3.up * 0.6f;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            GameObject hitObject = hit.collider.gameObject;
            if (hitObject == gameObject || hitObject.transform.IsChildOf(transform)) continue;
            if (hitObject == target || hitObject.transform.IsChildOf(target.transform)) continue;
            if (hitObject.CompareTag("Projectile")) continue;

            if (hitObject.CompareTag("Cover") || hitObject.isStatic) return false;
        }

        return true;
    }

    private void FireRocket(GameObject target)
    {
        GameObject rocket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rocket.name = "EngineerRocket";
        rocket.transform.position = transform.position + Vector3.up * 0.75f;
        rocket.transform.localScale = Vector3.one * rocketVisualScale;

        Collider collider = rocket.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        Renderer renderer = rocket.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = new Color(1f, 0.55f, 0.1f, 1f);

        string enemyTag = CompareTag("Attacker") ? "Defender" : "Attacker";

        EngineerRocket projectile = rocket.AddComponent<EngineerRocket>();
        projectile.Initialize(target, enemyTag, rocketDamage, rocketSpeed, blastRadius, edgeDamageMultiplier);
    }

    public static void ApplyIfEngineer(GameObject unit)
    {
        if (unit == null) return;
        if (UnitClassIdentity.GetClass(unit) != UnitClass.Engineer) return;
        if (unit.GetComponent<EngineerUnitProfile>() == null) unit.AddComponent<EngineerUnitProfile>();
    }
}

public sealed class EngineerRocket : MonoBehaviour
{
    private GameObject target;
    private string enemyTag;
    private float damage;
    private float speed;
    private float blastRadius;
    private float edgeDamageMultiplier;
    private float expireTime;

    public void Initialize(GameObject targetObject, string targetFactionTag, float damageAmount, float projectileSpeed, float radius, float edgeMultiplier)
    {
        target = targetObject;
        enemyTag = targetFactionTag;
        damage = Mathf.Max(0f, damageAmount);
        speed = Mathf.Max(1f, projectileSpeed);
        blastRadius = Mathf.Max(0.1f, radius);
        edgeDamageMultiplier = Mathf.Clamp01(edgeMultiplier);
        expireTime = Time.time + 5f;
    }

    private void Update()
    {
        if (target == null || Time.time >= expireTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPoint = target.transform.position + Vector3.up * 0.5f;
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPoint) <= 0.35f)
        {
            Explode(targetPoint);
        }
    }

    private void Explode(Vector3 position)
    {
        ExplosiveImpactPresentation.Play(position, blastRadius);

        HashSet<Health> damagedTargets = new HashSet<Health>();
        Collider[] hits = Physics.OverlapSphere(position, blastRadius);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Health health = hit.GetComponentInParent<Health>();
            if (health == null || damagedTargets.Contains(health)) continue;
            if (!health.CompareTag(enemyTag)) continue;

            damagedTargets.Add(health);

            Vector3 closestPoint = hit.ClosestPoint(position);
            float distance = Vector3.Distance(position, closestPoint);
            float normalizedDistance = Mathf.Clamp01(distance / blastRadius);
            float multiplier = Mathf.Lerp(1f, edgeDamageMultiplier, normalizedDistance);
            health.TakeDamage(damage * multiplier);
        }

        // The intended vehicle must still receive the direct blast even if its collider setup
        // is unusual enough not to be returned by OverlapSphere.
        if (target != null)
        {
            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && targetHealth.CompareTag(enemyTag) && !damagedTargets.Contains(targetHealth))
            {
                targetHealth.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}
