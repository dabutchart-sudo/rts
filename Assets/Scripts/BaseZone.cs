using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BaseZone : MonoBehaviour
{
    [Header("Base Identification")]
    public string baseName = "Forward Operating Base";
    public Faction controllingFaction = Faction.Attacker;

    [Header("Spawn Reference")]
    [Tooltip("The exact transform where units should spawn.")]
    public Transform spawnPoint;

    [Header("Visual Settings")]
    public float radius = 4f;
    [Tooltip("Color of the Attacker staging ring.")]
    public Color attackerColor = new Color(1f, 0.2f, 0.2f, 0.85f);
    [Tooltip("Color of the Defender staging ring.")]
    public Color defenderColor = new Color(0.2f, 0.6f, 1f, 0.85f);
    public float lineWidth = 0.3f;
    public int circleSegments = 36; // Higher number = smoother circle

    private LineRenderer lineRenderer;

    void Awake()
    {
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        lineRenderer = GetComponent<LineRenderer>();
        SetupVisuals();
    }

    void SetupVisuals()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true; // Connects the end of the line back to the start
        lineRenderer.positionCount = circleSegments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        // Assign a default material if one is missing so it doesn't render as magenta
        if (lineRenderer.sharedMaterial == null)
        {
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (defaultShader == null) defaultShader = Shader.Find("Sprites/Default");
            lineRenderer.material = new Material(defaultShader);
        }

        // Apply team colors
        Color teamColor = (controllingFaction == Faction.Attacker) ? attackerColor : defenderColor;
        lineRenderer.startColor = teamColor;
        lineRenderer.endColor = teamColor;

        DrawCircle();
    }

    void DrawCircle()
    {
        Vector3 center = spawnPoint.position;
        float angle = 0f;

        for (int i = 0; i < circleSegments; i++)
        {
            // Calculate X and Z positions using trigonometry
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

            // Plot the point slightly above the ground (Y + 0.15f) to prevent terrain clipping
            lineRenderer.SetPosition(i, new Vector3(center.x + x, center.y + 0.15f, center.z + z));

            angle += (360f / circleSegments);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = (controllingFaction == Faction.Attacker) ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 0.6f, 1f, 0.6f);
        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, radius);
    }
}