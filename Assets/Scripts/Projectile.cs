using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 40f;
    public float damage = 25f;
    public float lifetime = 2.5f;
    public string targetTag = "Defender";

    [Header("Armor Penetration")]
    public bool canDamageHeavyArmor = false;

    [Header("Visual Effects")]
    [Tooltip("The explosion or spark effect to spawn on impact.")]
    public GameObject impactEffectPrefab;

    private Vector3 moveDirection = Vector3.forward;

    public void Initialize(Transform target, string enemyTag, float bulletDamage)
    {
        targetTag = enemyTag;
        damage = bulletDamage;

        if (target != null)
        {
            Vector3 targetCenter = target.position + Vector3.up * 0.5f;
            Vector3 calculatedDir = targetCenter - transform.position;

            calculatedDir.y = 0f;

            if (calculatedDir != Vector3.zero) moveDirection = calculatedDir.normalized;
            else moveDirection = transform.forward;
        }
        else
        {
            moveDirection = transform.forward;
            moveDirection.y = 0f;
            moveDirection = moveDirection.normalized;
        }

        transform.forward = moveDirection;
        Destroy(gameObject, lifetime);
    }

    void Start()
    {
        moveDirection.y = 0f;
        if (moveDirection == Vector3.zero) moveDirection = transform.forward;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Projectile>() != null) return;

        if (other.CompareTag("Cover"))
        {
            SpawnImpactEffect(false);
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag(targetTag))
        {
            Health enemyHealth = other.GetComponent<Health>();
            if (enemyHealth != null)
            {
                if (enemyHealth.armor == Health.ArmorType.Heavy && !canDamageHeavyArmor)
                {
                    SpawnImpactEffect(true);
                    Destroy(gameObject);
                    return;
                }

                enemyHealth.TakeDamage(damage);
            }

            SpawnImpactEffect(false);
            Destroy(gameObject);
        }
    }

    void SpawnImpactEffect(bool heavyRicochet)
    {
        CombatShotPresentation.PlayImpact(transform.position, heavyRicochet);

        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
}
