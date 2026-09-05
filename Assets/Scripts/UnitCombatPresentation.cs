using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lightweight runtime-only presentation effects for infantry units.
/// Keeps combat logic separate from visual feedback.
/// </summary>
public sealed class UnitCombatPresentation : MonoBehaviour
{
    [Header("Hit Flash")]
    [SerializeField] private float hitFlashDuration = 0.10f;

    [Header("Spawn Drop")]
    [SerializeField] private float spawnHeight = 7f;
    [SerializeField] private float spawnDropDuration = 0.32f;

    [Header("Death Shatter")]
    [SerializeField, Range(4, 16)] private int fragmentCount = 9;
    [SerializeField] private float fragmentLifetime = 1.6f;
    [SerializeField] private float fragmentForce = 4.8f;

    private readonly List<RendererState> rendererStates = new List<RendererState>();
    private Coroutine flashRoutine;
    private bool rendererStateCached;

    private sealed class RendererState
    {
        public Renderer renderer;
        public Color color;
        public bool hasBaseColor;
        public bool hasColor;
    }

    public static UnitCombatPresentation Ensure(GameObject unit)
    {
        if (unit == null) return null;
        UnitCombatPresentation presentation = unit.GetComponent<UnitCombatPresentation>();
        return presentation != null ? presentation : unit.AddComponent<UnitCombatPresentation>();
    }

    public void PlayHitFlash()
    {
        CacheRendererState();
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    public void PlaySpawnDrop()
    {
        StartCoroutine(SpawnDropRoutine());
    }

    public void PlayDeathShatter()
    {
        CacheRendererState();
        Color factionColor = ResolveRepresentativeColor();
        Bounds bounds = ResolveBounds();
        Vector3 centre = bounds.size.sqrMagnitude > 0f ? bounds.center : transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "DeathFragment";
            fragment.transform.position = centre + Random.insideUnitSphere * 0.35f;
            fragment.transform.rotation = Random.rotation;

            float size = Random.Range(0.16f, 0.30f);
            fragment.transform.localScale = Vector3.one * size;

            Collider collider = fragment.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            Renderer renderer = fragment.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(renderer.sharedMaterial);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", factionColor);
                if (material.HasProperty("_Color")) material.SetColor("_Color", factionColor);
                renderer.material = material;
            }

            Rigidbody body = fragment.AddComponent<Rigidbody>();
            body.mass = 0.08f;
            body.useGravity = true;
            Vector3 outward = (fragment.transform.position - centre).normalized;
            if (outward == Vector3.zero) outward = Random.onUnitSphere;
            outward.y = Mathf.Abs(outward.y) + 0.35f;
            body.AddForce(outward.normalized * Random.Range(fragmentForce * 0.65f, fragmentForce * 1.25f), ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 7f, ForceMode.Impulse);

            Destroy(fragment, fragmentLifetime);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        ApplyColor(Color.white);
        yield return new WaitForSeconds(hitFlashDuration);
        RestoreColors();
        flashRoutine = null;
    }

    private IEnumerator SpawnDropRoutine()
    {
        Vector3 landingPosition = transform.position;
        Vector3 startPosition = landingPosition + Vector3.up * spawnHeight;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        bool agentWasEnabled = agent != null && agent.enabled;
        if (agentWasEnabled) agent.enabled = false;

        Combat combat = GetComponent<Combat>();
        bool combatWasEnabled = combat != null && combat.enabled;
        if (combatWasEnabled) combat.enabled = false;

        transform.position = startPosition;

        float elapsed = 0f;
        while (elapsed < spawnDropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDropDuration);
            float eased = t * t;
            transform.position = Vector3.Lerp(startPosition, landingPosition, eased);
            yield return null;
        }

        transform.position = landingPosition;

        if (agent != null && agentWasEnabled)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(landingPosition);
        }

        if (combat != null && combatWasEnabled) combat.enabled = true;

        // The strategic AI may have evaluated its objective while the NavMeshAgent was disabled.
        // In that case it can remember the objective without ever assigning a path. Explicitly
        // refresh the destination once the landing animation returns control to the agent.
        AutonomousUnit autonomousUnit = GetComponent<AutonomousUnit>();
        if (autonomousUnit != null)
        {
            autonomousUnit.UpdateDestination();
        }

        SpawnLandingPuff(landingPosition);
    }

    private void SpawnLandingPuff(Vector3 position)
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "SpawnPuff";
            puff.transform.position = position + new Vector3(Random.Range(-0.35f, 0.35f), 0.12f, Random.Range(-0.35f, 0.35f));
            puff.transform.localScale = Vector3.one * Random.Range(0.18f, 0.34f);

            Collider collider = puff.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = puff.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(renderer.sharedMaterial);
                Color smoke = new Color(0.65f, 0.65f, 0.65f, 1f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", smoke);
                if (material.HasProperty("_Color")) material.SetColor("_Color", smoke);
                renderer.material = material;
            }

            SpawnPuffLifetime life = puff.AddComponent<SpawnPuffLifetime>();
            life.Initialize(Random.Range(0.35f, 0.55f), Random.Range(0.4f, 0.8f));
        }
    }

    private void CacheRendererState()
    {
        if (rendererStateCached) return;
        rendererStateCached = true;
        rendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer) continue;
            Material material = renderer.sharedMaterial;
            if (material == null) continue;

            bool hasBase = material.HasProperty("_BaseColor");
            bool hasColor = material.HasProperty("_Color");
            if (!hasBase && !hasColor) continue;

            Color color = hasBase ? material.GetColor("_BaseColor") : material.GetColor("_Color");
            rendererStates.Add(new RendererState
            {
                renderer = renderer,
                color = color,
                hasBaseColor = hasBase,
                hasColor = hasColor
            });
        }
    }

    private void ApplyColor(Color color)
    {
        foreach (RendererState state in rendererStates)
        {
            if (state.renderer == null) continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            state.renderer.GetPropertyBlock(block);
            if (state.hasBaseColor) block.SetColor("_BaseColor", color);
            if (state.hasColor) block.SetColor("_Color", color);
            state.renderer.SetPropertyBlock(block);
        }
    }

    private void RestoreColors()
    {
        foreach (RendererState state in rendererStates)
        {
            if (state.renderer == null) continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            state.renderer.GetPropertyBlock(block);
            if (state.hasBaseColor) block.SetColor("_BaseColor", state.color);
            if (state.hasColor) block.SetColor("_Color", state.color);
            state.renderer.SetPropertyBlock(block);
        }
    }

    private Color ResolveRepresentativeColor()
    {
        foreach (RendererState state in rendererStates)
        {
            if (state.renderer != null) return state.color;
        }

        return CompareTag("Attacker") ? new Color(0.2f, 0.45f, 1f) : new Color(1f, 0.25f, 0.2f);
    }

    private Bounds ResolveBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds result = new Bounds(transform.position, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer) continue;
            if (!found)
            {
                result = renderer.bounds;
                found = true;
            }
            else
            {
                result.Encapsulate(renderer.bounds);
            }
        }

        return result;
    }
}

public sealed class SpawnPuffLifetime : MonoBehaviour
{
    private float lifetime;
    private float riseSpeed;
    private float elapsed;
    private Vector3 initialScale;

    public void Initialize(float newLifetime, float newRiseSpeed)
    {
        lifetime = newLifetime;
        riseSpeed = newRiseSpeed;
        initialScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        float t = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
        transform.localScale = initialScale * Mathf.Lerp(1f, 0.05f, t);
        if (elapsed >= lifetime) Destroy(gameObject);
    }
}
