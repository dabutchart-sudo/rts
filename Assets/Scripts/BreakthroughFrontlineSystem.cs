using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which sectors each side is allowed to stand in.
/// Attackers may use every sector they have already taken, plus the one being fought.
/// During the pause after a capture they stay in the sector they just took.
/// Defenders stay in the active sector, and may cross back into the one they lost while retreating.
/// </summary>
public sealed class BreakthroughFrontlineSystem : MonoBehaviour
{
    public static BreakthroughFrontlineSystem Instance { get; private set; }

    [Header("Debug Visualisation")]
    [SerializeField] private bool showRuntimeBoundaries = true;
    [SerializeField] private float lineWidth = 0.22f;
    [SerializeField] private float groundOffset = 0.22f;

    private readonly List<LineRenderer> sectorLines = new List<LineRenderer>();
    private Material lineMaterial;
    private int lastSectorCount = -1;
    private int lastActiveSector = -1;
    private bool lastTransitionState;

    public static BreakthroughFrontlineSystem EnsureInstance()
    {
        if (Instance != null) return Instance;

        BreakthroughFrontlineSystem existing = FindAnyObjectByType<BreakthroughFrontlineSystem>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("BreakthroughFrontlineSystem");
        return host.AddComponent<BreakthroughFrontlineSystem>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        DisableLegacyBoundaryLine();
        RefreshVisuals(true);
    }

    private void Update()
    {
        RefreshVisuals(false);
    }

    public static bool IsPositionAllowed(Faction faction, Vector3 position)
    {
        if (!TryGetAllowedSectorIndices(faction, out List<int> allowedIndices)) return true;

        Sector[] sectors = GameManager.Instance.sectors;
        bool foundUsableBounds = false;

        foreach (int sectorIndex in allowedIndices)
        {
            if (sectorIndex < 0 || sectorIndex >= sectors.Length) continue;
            Sector sector = sectors[sectorIndex];
            if (sector == null || sector.sectorBounds.size.sqrMagnitude <= 0.01f) continue;

            foundUsableBounds = true;
            if (sector.sectorBounds.Contains(position)) return true;
        }

        return !foundUsableBounds;
    }

    public static bool TryGetClosestAllowedPoint(Faction faction, Vector3 position, out Vector3 closestPoint)
    {
        closestPoint = position;
        if (!TryGetAllowedSectorIndices(faction, out List<int> allowedIndices)) return false;

        Sector[] sectors = GameManager.Instance.sectors;
        bool found = false;
        float bestDistanceSqr = float.MaxValue;

        foreach (int sectorIndex in allowedIndices)
        {
            if (sectorIndex < 0 || sectorIndex >= sectors.Length) continue;
            Sector sector = sectors[sectorIndex];
            if (sector == null || sector.sectorBounds.size.sqrMagnitude <= 0.01f) continue;

            Vector3 candidate = sector.sectorBounds.ClosestPoint(position);
            float distanceSqr = (candidate - position).sqrMagnitude;

            if (!found || distanceSqr < bestDistanceSqr)
            {
                closestPoint = candidate;
                bestDistanceSqr = distanceSqr;
                found = true;
            }
        }

        return found;
    }

    public static bool TryGetAllowedSectorIndices(Faction faction, out List<int> allowedIndices)
    {
        allowedIndices = new List<int>();

        if (GameManager.Instance == null || GameManager.Instance.sectors == null || GameManager.Instance.sectors.Length == 0)
        {
            return false;
        }

        int activeIndex = Mathf.Clamp(GameManager.Instance.currentSectorIndex, 0, GameManager.Instance.sectors.Length - 1);

        if (GameManager.Instance.isTransitioningSector)
        {
            if (faction == Faction.Attacker)
            {
                allowedIndices.Add(activeIndex);
            }
            else if (faction == Faction.Defender)
            {
                allowedIndices.Add(activeIndex);
                int nextIndex = activeIndex + 1;
                if (nextIndex < GameManager.Instance.sectors.Length) allowedIndices.Add(nextIndex);
            }

            return allowedIndices.Count > 0;
        }

        if (faction == Faction.Attacker)
        {
            for (int i = 0; i <= activeIndex; i++) allowedIndices.Add(i);
        }
        else if (faction == Faction.Defender)
        {
            allowedIndices.Add(activeIndex);
        }

        return allowedIndices.Count > 0;
    }

    private void RefreshVisuals(bool force)
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            SetAllLinesVisible(false);
            return;
        }

        int sectorCount = GameManager.Instance.sectors.Length;
        int activeSector = GameManager.Instance.currentSectorIndex;
        bool transitioning = GameManager.Instance.isTransitioningSector;

        if (!force && sectorCount == lastSectorCount && activeSector == lastActiveSector && transitioning == lastTransitionState)
        {
            return;
        }

        lastSectorCount = sectorCount;
        lastActiveSector = activeSector;
        lastTransitionState = transitioning;

        EnsureLineCount(sectorCount);

        for (int i = 0; i < sectorLines.Count; i++)
        {
            LineRenderer line = sectorLines[i];
            if (line == null) continue;

            if (!showRuntimeBoundaries || i >= sectorCount)
            {
                line.enabled = false;
                continue;
            }

            Sector sector = GameManager.Instance.sectors[i];
            if (sector == null || sector.sectorBounds.size.sqrMagnitude <= 0.01f)
            {
                line.enabled = false;
                continue;
            }

            Color color = GetSectorDebugColor(i, activeSector, transitioning);
            if (color.a <= 0.01f)
            {
                line.enabled = false;
                continue;
            }

            line.enabled = true;
            line.startColor = color;
            line.endColor = color;
            UpdateLinePerimeter(line, sector.sectorBounds);
        }
    }

    private Color GetSectorDebugColor(int sectorIndex, int activeSector, bool transitioning)
    {
        Color attacker = new Color(0.85f, 0.25f, 0.22f, 0.82f);
        Color defender = new Color(0.25f, 0.45f, 0.9f, 0.82f);

        if (transitioning)
        {
            if (sectorIndex == activeSector) return attacker;
            if (sectorIndex == activeSector + 1) return defender;
            if (sectorIndex < activeSector)
            {
                Color captured = attacker;
                captured.a = 0.30f;
                return captured;
            }
            return Color.clear;
        }

        if (sectorIndex == activeSector)
        {
            return new Color(1f, 0.82f, 0.2f, 0.92f);
        }

        if (sectorIndex < activeSector)
        {
            Color captured = attacker;
            captured.a = 0.30f;
            return captured;
        }

        return Color.clear;
    }

    private void EnsureLineCount(int count)
    {
        while (sectorLines.Count < count)
        {
            int index = sectorLines.Count;
            GameObject lineObject = new GameObject($"FrontlineBoundary_Sector_{index + 1}");
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetLineMaterial();

            sectorLines.Add(line);
        }
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null) return lineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            lineMaterial = new Material(shader)
            {
                name = "FrontlineBoundary_RuntimeMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return lineMaterial;
    }

    private void UpdateLinePerimeter(LineRenderer line, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float y = center.y + groundOffset;

        line.SetPosition(0, new Vector3(center.x - extents.x, y, center.z + extents.z));
        line.SetPosition(1, new Vector3(center.x + extents.x, y, center.z + extents.z));
        line.SetPosition(2, new Vector3(center.x + extents.x, y, center.z - extents.z));
        line.SetPosition(3, new Vector3(center.x - extents.x, y, center.z - extents.z));
    }

    private void SetAllLinesVisible(bool visible)
    {
        foreach (LineRenderer line in sectorLines)
        {
            if (line != null) line.enabled = visible;
        }
    }

    private void DisableLegacyBoundaryLine()
    {
        SectorBoundaryLineVisualizer legacy = FindAnyObjectByType<SectorBoundaryLineVisualizer>(FindObjectsInactive.Include);
        if (legacy != null)
        {
            legacy.enabled = false;
            LineRenderer legacyLine = legacy.GetComponent<LineRenderer>();
            if (legacyLine != null) legacyLine.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
