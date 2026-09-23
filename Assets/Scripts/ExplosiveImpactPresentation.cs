using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight runtime presentation for explosive impacts.
/// Uses temporary primitives so the effect does not require scene or prefab wiring.
/// </summary>
public sealed class ExplosiveImpactPresentation : MonoBehaviour
{
    public static void Play(Vector3 position, float blastRadius)
    {
        GameObject root = new GameObject("ExplosionPresentation");
        root.transform.position = position;
        ExplosiveImpactPresentation effect = root.AddComponent<ExplosiveImpactPresentation>();
        effect.StartCoroutine(effect.PlayRoutine(Mathf.Max(0.5f, blastRadius)));
    }

    private IEnumerator PlayRoutine(float blastRadius)
    {
        CreateFlash(blastRadius);
        CreateDust(blastRadius);
        CreateDebris(blastRadius);

        yield return new WaitForSeconds(1.35f);
        Destroy(gameObject);
    }

    private void CreateFlash(float blastRadius)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "BlastFlash";
        flash.transform.SetParent(transform, false);
        flash.transform.localPosition = Vector3.up * 0.22f;
        flash.transform.localScale = Vector3.one * Mathf.Clamp(blastRadius * 0.20f, 0.7f, 1.5f);
        DisableCollider(flash);
        SetColour(flash, new Color(1f, 0.52f, 0.10f, 1f));
        flash.AddComponent<ExplosionPieceLifetime>().Initialize(0.16f, Vector3.one * 1.8f, Vector3.up * 0.15f);
    }

    private void CreateDust(float blastRadius)
    {
        int puffCount = 7;
        for (int i = 0; i < puffCount; i++)
        {
            float angle = (360f / puffCount) * i + Random.Range(-14f, 14f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "BlastDust";
            puff.transform.SetParent(transform, false);
            puff.transform.localPosition = direction * Random.Range(0.12f, 0.45f) + Vector3.up * Random.Range(0.10f, 0.35f);
            float size = Random.Range(0.32f, 0.58f) * Mathf.Clamp(blastRadius / 4f, 0.8f, 1.35f);
            puff.transform.localScale = Vector3.one * size;
            DisableCollider(puff);
            SetColour(puff, new Color(0.34f, 0.31f, 0.25f, 1f));
            puff.AddComponent<ExplosionPieceLifetime>().Initialize(Random.Range(0.65f, 1.05f), Vector3.one * Random.Range(1.4f, 2.2f), direction * Random.Range(0.8f, 1.6f) + Vector3.up * Random.Range(0.35f, 0.8f));
        }
    }

    private void CreateDebris(float blastRadius)
    {
        int debrisCount = 5;
        for (int i = 0; i < debrisCount; i++)
        {
            Vector3 horizontal = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            if (horizontal.sqrMagnitude < 0.01f) horizontal = Vector3.forward;

            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "BlastDebris";
            debris.transform.SetParent(transform, false);
            debris.transform.localPosition = Vector3.up * 0.15f;
            float size = Random.Range(0.10f, 0.20f);
            debris.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 1.2f), size);
            DisableCollider(debris);
            SetColour(debris, new Color(0.24f, 0.22f, 0.18f, 1f));
            debris.AddComponent<ExplosionPieceLifetime>().Initialize(Random.Range(0.45f, 0.75f), Vector3.one * 0.65f, horizontal * Random.Range(1.2f, 2.2f) + Vector3.up * Random.Range(0.45f, 0.9f));
        }
    }

    private static void DisableCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
    }

    private static void SetColour(GameObject obj, Color colour)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        Material material = new Material(shader);
        material.color = colour;
        renderer.material = material;
    }
}

public sealed class ExplosionPieceLifetime : MonoBehaviour
{
    private float lifetime;
    private Vector3 targetScaleMultiplier;
    private Vector3 velocity;
    private float age;
    private Vector3 startScale;

    public void Initialize(float duration, Vector3 scaleMultiplier, Vector3 moveVelocity)
    {
        lifetime = Mathf.Max(0.05f, duration);
        targetScaleMultiplier = scaleMultiplier;
        velocity = moveVelocity;
        startScale = transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        transform.position += velocity * Time.deltaTime;
        velocity *= Mathf.Pow(0.15f, Time.deltaTime);
        transform.localScale = Vector3.Lerp(startScale, Vector3.Scale(startScale, targetScaleMultiplier), t);

        if (age >= lifetime)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.material != null) Destroy(renderer.material);
            Destroy(gameObject);
        }
    }
}
