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

    [Header("Legacy Ring Visual")]
    public float radius = 4f;
    public float lineWidth = 0.3f;
    public int circleSegments = 36;

    [Header("Ground Marker")]
    [SerializeField] private float markerHeight = 0.18f;
    [Tooltip("Designed to render roughly twice the current capture-point text size.")]
    [SerializeField] private float markerScale = 0.04f;
    [SerializeField] private float markerFontSize = 36f;

    private LineRenderer lineRenderer;
    private TextMeshPro groundMarker;

    void Awake()
    {
        if (spawnPoint == null) spawnPoint = transform;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null) lineRenderer.enabled = false;
        CreateGroundMarker();
    }

    private void Update()
    {
        bool isActiveFrontlineBase = IsCurrentSectorBase();
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (groundMarker != null) groundMarker.gameObject.SetActive(isActiveFrontlineBase);

        if (isActiveFrontlineBase)
        {
            UpdateGroundMarkerPosition();
        }
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

    public bool IsCurrentBaseFor(Faction faction)
    {
        if (!IsCurrentSectorBase()) return false;
        return controllingFaction == faction;
    }

    public Transform GetSpawnPoint()
    {
        return spawnPoint != null ? spawnPoint : transform;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = FactionVisuals.GetColor(controllingFaction);
        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, radius);
    }
}
