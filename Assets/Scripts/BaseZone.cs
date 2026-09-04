using TMPro;
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
    public float lineWidth = 0.3f;
    public int circleSegments = 36;

    [Header("Ground Marker")]
    [SerializeField] private float markerHeight = 0.18f;
    [SerializeField] private float markerScale = 0.12f;
    [SerializeField] private float markerFontSize = 12f;

    private LineRenderer lineRenderer;
    private TextMeshPro groundMarker;

    void Awake()
    {
        if (spawnPoint == null) spawnPoint = transform;

        lineRenderer = GetComponent<LineRenderer>();
        SetupVisuals();
        CreateGroundMarker();
    }

    private void Update()
    {
        bool isActiveFrontlineBase = IsCurrentSectorBase();
        if (lineRenderer != null) lineRenderer.enabled = isActiveFrontlineBase;
        if (groundMarker != null) groundMarker.gameObject.SetActive(isActiveFrontlineBase);

        if (isActiveFrontlineBase)
        {
            DrawCircle();
            UpdateGroundMarkerPosition();
        }
    }

    void SetupVisuals()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = circleSegments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (defaultShader == null) defaultShader = Shader.Find("Sprites/Default");
            lineRenderer.material = new Material(defaultShader);
        }

        Color teamColor = FactionVisuals.GetColor(controllingFaction);
        lineRenderer.startColor = teamColor;
        lineRenderer.endColor = teamColor;
        DrawCircle();
    }

    private void CreateGroundMarker()
    {
        Transform existing = transform.Find("BaseGroundMarker");
        if (existing != null) groundMarker = existing.GetComponent<TextMeshPro>();

        if (groundMarker == null)
        {
            GameObject markerObject = new GameObject("BaseGroundMarker");
            markerObject.transform.SetParent(transform, false);
            groundMarker = markerObject.AddComponent<TextMeshPro>();
        }

        groundMarker.text = controllingFaction == Faction.Attacker ? "A" : "D";
        groundMarker.alignment = TextAlignmentOptions.Center;
        groundMarker.fontSize = markerFontSize;
        groundMarker.fontStyle = FontStyles.Bold;
        groundMarker.color = FactionVisuals.GetColor(controllingFaction);
        groundMarker.enableAutoSizing = false;
        groundMarker.raycastTarget = false;
        groundMarker.sortingOrder = 5;
        UpdateGroundMarkerPosition();
    }

    private void UpdateGroundMarkerPosition()
    {
        if (groundMarker == null || spawnPoint == null) return;

        groundMarker.transform.position = spawnPoint.position + Vector3.up * markerHeight;
        groundMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        groundMarker.transform.localScale = Vector3.one * markerScale;
    }

    private bool IsCurrentSectorBase()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return false;
        int index = GameManager.Instance.currentSectorIndex;
        if (index < 0 || index >= GameManager.Instance.sectors.Length) return false;

        Sector sector = GameManager.Instance.sectors[index];
        if (sector == null) return false;

        return sector.attackerBase == this || sector.defenderBase == this;
    }

    void DrawCircle()
    {
        if (lineRenderer == null || spawnPoint == null) return;

        Vector3 center = spawnPoint.position;
        float angle = 0f;

        for (int i = 0; i < circleSegments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(center.x + x, center.y + 0.15f, center.z + z));
            angle += 360f / circleSegments;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = FactionVisuals.GetColor(controllingFaction);
        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, radius);
    }
}