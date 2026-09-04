using TMPro;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BaseZone : MonoBehaviour
{
    [Header("Base Identification")]
    public string baseName = "Forward Operating Base";
    [Tooltip("Legacy/fallback value only. Runtime faction is resolved from the sector's attackerBase/defenderBase assignment.")]
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
    [Tooltip("Large single-letter frontline-base marker for medium/high zoom readability.")]
    [SerializeField] private float markerScale = 0.08f;
    [SerializeField] private float markerFontSize = 48f;

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
            RefreshGroundMarkerIdentity();
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

        groundMarker.alignment = TextAlignmentOptions.Center;
        groundMarker.fontSize = markerFontSize;
        groundMarker.fontStyle = FontStyles.Bold;
        groundMarker.enableAutoSizing = false;
        groundMarker.raycastTarget = false;
        groundMarker.sortingOrder = 5;

        RefreshGroundMarkerIdentity();
        UpdateGroundMarkerPosition();
    }

    private void RefreshGroundMarkerIdentity()
    {
        if (groundMarker == null) return;

        Faction resolvedFaction = GetResolvedFaction();
        groundMarker.text = resolvedFaction == Faction.Defender ? "D" : "A";
        groundMarker.color = FactionVisuals.GetColor(resolvedFaction);
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
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return false;

        int index = GameManager.Instance.currentSectorIndex;
        if (index < 0 || index >= GameManager.Instance.sectors.Length) return false;

        Sector sector = GameManager.Instance.sectors[index];
        if (sector == null) return false;

        if (faction == Faction.Attacker) return sector.attackerBase == this;
        if (faction == Faction.Defender) return sector.defenderBase == this;
        return false;
    }

    public Faction GetResolvedFaction()
    {
        if (GameManager.Instance != null && GameManager.Instance.sectors != null)
        {
            foreach (Sector sector in GameManager.Instance.sectors)
            {
                if (sector == null) continue;
                if (sector.attackerBase == this) return Faction.Attacker;
                if (sector.defenderBase == this) return Faction.Defender;
            }
        }

        return controllingFaction;
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
