using UnityEngine;

/// <summary>
/// Gives Engineer infantry an automatic anti-vehicle rocket launcher.
/// The Engineer keeps its normal rifle combat, but periodically engages enemy vehicles
/// with a separate, slower, harder-hitting projectile.
/// </summary>
public sealed class EngineerUnitProfile : MonoBehaviour
{
    [Header("Rocket Launcher")]
    [SerializeField] private float rocketRange = 34f;
    [SerializeField] private float rocketCooldown = 5f;
    [SerializeField] private float rocketDamage = 120f;
    [SerializeField] private float rocketSpeed = 26f;
    [SerializeField] private float rocketVisualScale = 0.22f;

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

        EngineerRocket projectile = rocket.AddComponent<EngineerRocket>();
        projectile.Initialize(target, rocketDamage, rocketSpeed);
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
    private float damage;
    private float speed;
    private float expireTime;

    public void Initialize(GameObject targetObject, float damageAmount, float projectileSpeed)
    {
        target = targetObject;
        damage = Mathf.Max(0f, damageAmount);
        speed = Mathf.Max(1f, projectileSpeed);
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
            Health health = target.GetComponent<Health>();
            if (health != null) health.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
