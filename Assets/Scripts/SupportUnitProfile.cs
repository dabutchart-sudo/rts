using UnityEngine;

/// <summary>
/// Gives Support infantry an automatic support-bag ability. When nearby allies are hurt,
/// Support drops a temporary healing zone with a visible in-game ring.
/// </summary>
public sealed class SupportUnitProfile : MonoBehaviour
{
    [Header("Support Bag")]
    [SerializeField] private float checkInterval = 1.5f;
    [SerializeField] private float bagCooldown = 20f;
    [SerializeField] private float bagRadius = 7f;
    [SerializeField] private float bagLifetime = 18f;
    [SerializeField] private float healPerSecond = 12f;
    [SerializeField] private float allyCheckRadius = 8f;

    private float nextCheckTime;
    private float nextBagTime;

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isTransitioningSector) return;
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + Mathf.Max(0.2f, checkInterval);

        if (Time.time < nextBagTime) return;
        if (!HasWoundedAllyNearby()) return;

        DropSupportBag();
        nextBagTime = Time.time + Mathf.Max(1f, bagCooldown);
    }

    private bool HasWoundedAllyNearby()
    {
        string allyTag = CompareTag("Attacker") ? "Attacker" : CompareTag("Defender") ? "Defender" : string.Empty;
        if (string.IsNullOrEmpty(allyTag)) return false;

        GameObject[] allies = GameObject.FindGameObjectsWithTag(allyTag);
        foreach (GameObject ally in allies)
        {
            if (ally == null) continue;
            if (Vector3.Distance(transform.position, ally.transform.position) > allyCheckRadius) continue;

            Health health = ally.GetComponent<Health>();
            if (health != null && health.currentHealth < health.maxHealth - 0.1f) return true;
        }

        return false;
    }

    private void DropSupportBag()
    {
        GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bag.name = "SupportBag";
        bag.transform.position = transform.position + new Vector3(0f, 0.16f, 0f);
        bag.transform.localScale = new Vector3(0.55f, 0.25f, 0.4f);

        Collider bagCollider = bag.GetComponent<Collider>();
        if (bagCollider != null) Destroy(bagCollider);

        Renderer bagRenderer = bag.GetComponent<Renderer>();
        if (bagRenderer != null)
        {
            bagRenderer.material.color = CompareTag("Attacker")
                ? FactionVisuals.AttackerColor
                : FactionVisuals.DefenderColor;
        }

        SupportBagZone zone = bag.AddComponent<SupportBagZone>();
        zone.Initialize(CompareTag("Attacker") ? Faction.Attacker : Faction.Defender, bagRadius, bagLifetime, healPerSecond);
    }

    public static void ApplyIfSupport(GameObject unit)
    {
        if (unit == null) return;
        if (UnitClassIdentity.GetClass(unit) != UnitClass.Support) return;
        if (unit.GetComponent<SupportUnitProfile>() == null) unit.AddComponent<SupportUnitProfile>();
    }
}

public sealed class SupportBagZone : MonoBehaviour
{
    private Faction faction;
    private float radius;
    private float healPerSecond;
    private float expireTime;
    private float nextHealTick;
    private LineRenderer ring;

    public void Initialize(Faction owningFaction, float zoneRadius, float lifetime, float healingRate)
    {
        faction = owningFaction;
        radius = Mathf.Max(1f, zoneRadius);
        healPerSecond = Mathf.Max(0f, healingRate);
        expireTime = Time.time + Mathf.Max(1f, lifetime);
        CreateVisibleRing();
    }

    private void Update()
    {
        if (Time.time >= expireTime)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= nextHealTick)
        {
            nextHealTick = Time.time + 0.5f;
            HealNearbyAllies(0.5f);
        }
    }

    private void HealNearbyAllies(float tickDuration)
    {
        string allyTag = faction == Faction.Attacker ? "Attacker" : "Defender";
        GameObject[] allies = GameObject.FindGameObjectsWithTag(allyTag);

        foreach (GameObject ally in allies)
        {
            if (ally == null || Vector3.Distance(transform.position, ally.transform.position) > radius) continue;

            Health health = ally.GetComponent<Health>();
            if (health != null) health.Heal(healPerSecond * tickDuration);
        }
    }

    private void CreateVisibleRing()
    {
        ring = gameObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = 0.12f;
        ring.material = new Material(Shader.Find("Sprites/Default"));

        Color factionColor = FactionVisuals.GetColor(faction);
        Color ringColor = new Color(factionColor.r, factionColor.g, factionColor.b, 0.5f);
        ring.startColor = ringColor;
        ring.endColor = ringColor;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
        }
    }
}
