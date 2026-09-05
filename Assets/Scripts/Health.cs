using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public enum ArmorType { Light, Heavy }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public ArmorType armor = ArmorType.Light;

    [Header("UI Reference")]
    public Slider healthSlider;

    private float spawnTime;
    private bool isDead = false;
    private UnitCombatPresentation presentation;

    void Awake()
    {
        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }

        presentation = UnitCombatPresentation.Ensure(gameObject);
    }

    void Start()
    {
        currentHealth = maxHealth;
        spawnTime = Time.time;
        isDead = false;
        UpdateHealthBar();
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthBar();

        if (presentation != null)
        {
            presentation.PlayHitFlash();
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float healAmount)
    {
        if (isDead || healAmount <= 0f || currentHealth >= maxHealth) return;

        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0f, maxHealth);
        UpdateHealthBar();
    }

    public void UpdateHealthBar()
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        float lifespan = Time.time - spawnTime;

        if (gameObject.CompareTag("Defender"))
        {
            TestDashboardOverlay.RecordUnitDeath(transform.position, false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddXP(5, "Attacker");
                GameManager.Instance.defenderDeaths++;
                GameManager.Instance.defenderTotalLifespan += lifespan;
            }
        }
        else if (gameObject.CompareTag("Attacker"))
        {
            TestDashboardOverlay.RecordUnitDeath(transform.position, true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddXP(5, "Defender");
                GameManager.Instance.attackerDeaths++;
                GameManager.Instance.attackerTotalLifespan += lifespan;

                if (GameManager.Instance.attackerSpawner != null)
                {
                    GameManager.Instance.attackerSpawner.RespawnAssaultUnit();
                }
            }
        }

        if (presentation != null)
        {
            presentation.PlayDeathShatter();
        }

        Destroy(gameObject);
    }
}
