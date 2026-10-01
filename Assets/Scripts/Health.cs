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

    private float spawnTime; // NEW: Track when they spawned

    void Awake()
    {
        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }
    }

    void Start()
    {
        currentHealth = maxHealth;
        spawnTime = Time.time; 
        UpdateHealthBar();
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
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
        float lifespan = Time.time - spawnTime; // NEW: Calculate how long they lived

        if (gameObject.CompareTag("Defender"))
        {
            TestDashboardOverlay.RecordUnitDeath(transform.position, false);
            if (GameManager.Instance != null) 
            {
                GameManager.Instance.AddXP(5, "Attacker");
                GameManager.Instance.defenderDeaths++; 
                GameManager.Instance.defenderTotalLifespan += lifespan; // NEW: Send to GameManager
            }
        }
        else if (gameObject.CompareTag("Attacker"))
        {
            TestDashboardOverlay.RecordUnitDeath(transform.position, true);
            if (GameManager.Instance != null) 
            {
                GameManager.Instance.AddXP(5, "Defender");
                GameManager.Instance.attackerDeaths++; 
                GameManager.Instance.attackerTotalLifespan += lifespan; // NEW: Send to GameManager
                
                if (GameManager.Instance.attackerSpawner != null)
                {
                    GameManager.Instance.attackerSpawner.RespawnAssaultUnit();
                }
            }
        }

        Destroy(gameObject);
    }
}