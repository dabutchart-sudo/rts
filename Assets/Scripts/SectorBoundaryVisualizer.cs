using UnityEngine;

public class SectorBoundaryVisualizer : MonoBehaviour
{
    [Header("Out-of-Bounds Visuals")]
    [Tooltip("Material used to shade inactive sectors (Transparent/Unlit).")]
    public Material inactiveSectorMaterial;

    private GameObject[] inactiveOverlays;

    void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return;

        int numSectors = GameManager.Instance.sectors.Length;
        inactiveOverlays = new GameObject[numSectors];

        for (int i = 0; i < numSectors; i++)
        {
            // Create a flat Quad on the ground
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(overlay.GetComponent<Collider>());

            // Rotate 90 degrees on X so it lays flat on the terrain floor
            overlay.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.SetParent(transform);
            overlay.name = $"Inactive_Floor_Sector_{i + 1}";

            MeshRenderer mr = overlay.GetComponent<MeshRenderer>();
            if (inactiveSectorMaterial != null)
            {
                mr.material = inactiveSectorMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            Bounds b = GameManager.Instance.sectors[i].sectorBounds;
            if (b.size.sqrMagnitude > 0.01f)
            {
                // Place slightly above the floor to avoid z-fighting with the ground plane
                overlay.transform.position = new Vector3(b.center.x, b.center.y + 0.05f, b.center.z);
                // Quads use X and Y scale (which correspond to map X and Z once rotated flat)
                overlay.transform.localScale = new Vector3(b.size.x, b.size.z, 1f);
            }
            else
            {
                overlay.SetActive(false);
            }

            inactiveOverlays[i] = overlay;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || inactiveOverlays == null) return;

        int activeIndex = GameManager.Instance.currentSectorIndex;

        for (int i = 0; i < inactiveOverlays.Length; i++)
        {
            if (inactiveOverlays[i] == null) continue;

            bool isClear = (i == activeIndex);

            if (GameManager.Instance.isTransitioningSector && i == activeIndex + 1)
            {
                isClear = true;
            }

            Bounds b = GameManager.Instance.sectors[i].sectorBounds;
            if (b.size.sqrMagnitude < 0.01f) isClear = true;

            inactiveOverlays[i].SetActive(!isClear);
        }
    }
}