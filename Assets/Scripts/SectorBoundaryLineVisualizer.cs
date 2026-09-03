using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SectorBoundaryLineVisualizer : MonoBehaviour
{
    [Header("Line Visual Settings")]
    [Tooltip("Height offset above the terrain so the line doesn't clip through the ground.")]
    public float groundOffset = 0.15f;

    [Tooltip("Width of the tactical border line.")]
    public float lineWidth = 0.4f;

    [Tooltip("Color of the tactical boundary line during active combat.")]
    public Color activeBorderColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    [Tooltip("Color of the tactical boundary line during sector transitions.")]
    public Color transitionBorderColor = new Color(0.2f, 0.9f, 0.3f, 0.95f);

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        ConfigureLineRenderer();
    }

    void ConfigureLineRenderer()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 4;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        
        // 🚨 CRITICAL FIX: Align with camera view so it is always visible from top-down angles
        lineRenderer.alignment = LineAlignment.View;

        // Fallback default material if none assigned
        if (lineRenderer.sharedMaterial == null)
        {
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (defaultShader == null) defaultShader = Shader.Find("Sprites/Default");
            lineRenderer.material = new Material(defaultShader);
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        int activeIndex = GameManager.Instance.currentSectorIndex;
        if (activeIndex < 0 || activeIndex >= GameManager.Instance.sectors.Length)
        {
            lineRenderer.enabled = false;
            return;
        }

        Bounds bounds = GameManager.Instance.sectors[activeIndex].sectorBounds;

        if (bounds.size.sqrMagnitude < 0.01f)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        UpdateLinePerimeter(bounds);
    }

    private void UpdateLinePerimeter(Bounds b)
    {
        Vector3 center = b.center;
        Vector3 extents = b.extents;
        float y = center.y + groundOffset;

        Vector3 cornerNW = new Vector3(center.x - extents.x, y, center.z + extents.z);
        Vector3 cornerNE = new Vector3(center.x + extents.x, y, center.z + extents.z);
        Vector3 cornerSE = new Vector3(center.x + extents.x, y, center.z - extents.z);
        Vector3 cornerSW = new Vector3(center.x - extents.x, y, center.z - extents.z);

        lineRenderer.SetPosition(0, cornerNW);
        lineRenderer.SetPosition(1, cornerNE);
        lineRenderer.SetPosition(2, cornerSE);
        lineRenderer.SetPosition(3, cornerSW);

        Color targetColor = GameManager.Instance.isTransitioningSector ? transitionBorderColor : activeBorderColor;
        lineRenderer.startColor = targetColor;
        lineRenderer.endColor = targetColor;
    }
}