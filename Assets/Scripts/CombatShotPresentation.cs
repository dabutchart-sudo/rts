using UnityEngine;

/// <summary>
/// Lightweight runtime-only gunfire presentation. No prefab or scene wiring required.
/// </summary>
public static class CombatShotPresentation
{
    public static void PlayMuzzleFlash(Vector3 position, Vector3 forward, bool isRecon)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = isRecon ? "ReconMuzzleFlash" : "MuzzleFlash";
        flash.transform.position = position + forward.normalized * 0.12f;
        flash.transform.localScale = Vector3.one * (isRecon ? 0.30f : 0.20f);

        Collider collider = flash.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        Renderer renderer = flash.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(renderer.sharedMaterial);
            Color color = isRecon ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(1f, 0.72f, 0.22f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.material = material;
        }

        Object.Destroy(flash, isRecon ? 0.09f : 0.06f);
    }

    public static void ConfigureProjectile(GameObject projectile, bool isRecon)
    {
        if (projectile == null) return;

        TrailRenderer trail = projectile.GetComponent<TrailRenderer>();
        if (trail == null) trail = projectile.AddComponent<TrailRenderer>();

        // Recon deliberately leaves a much longer-lived smoke line so that firing reveals
        // the rough direction of the sniper to an observer/player.
        trail.time = isRecon ? 2.75f : 0.16f;
        trail.startWidth = isRecon ? 0.14f : 0.07f;
        trail.endWidth = isRecon ? 0.035f : 0.015f;
        trail.minVertexDistance = 0.05f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            Material material = new Material(shader);
            Color color = isRecon ? new Color(0.72f, 0.72f, 0.72f, 0.82f) : new Color(1f, 0.80f, 0.28f, 0.92f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            trail.material = material;
        }

        if (isRecon)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.78f, 0.78f, 0.78f), 0f),
                    new GradientColorKey(new Color(0.50f, 0.50f, 0.50f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.80f, 0f),
                    new GradientAlphaKey(0.48f, 0.45f),
                    new GradientAlphaKey(0.20f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
        }
        else
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.10f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
        }
    }

    public static void PlayImpact(Vector3 position, bool heavyRicochet = false)
    {
        int count = heavyRicochet ? 5 : 3;
        for (int i = 0; i < count; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.name = heavyRicochet ? "RicochetSpark" : "ImpactSpark";
            spark.transform.position = position + Random.insideUnitSphere * 0.08f;
            spark.transform.localScale = Vector3.one * Random.Range(0.045f, 0.085f);

            Collider collider = spark.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            Renderer renderer = spark.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(renderer.sharedMaterial);
                Color color = heavyRicochet ? new Color(1f, 0.75f, 0.18f, 1f) : new Color(0.85f, 0.82f, 0.72f, 1f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                renderer.material = material;
            }

            Rigidbody body = spark.AddComponent<Rigidbody>();
            body.mass = 0.02f;
            body.useGravity = true;
            Vector3 force = Random.onUnitSphere;
            force.y = Mathf.Abs(force.y) + 0.25f;
            body.AddForce(force.normalized * Random.Range(0.8f, 2.0f), ForceMode.Impulse);
            Object.Destroy(spark, Random.Range(0.18f, 0.35f));
        }
    }
}
