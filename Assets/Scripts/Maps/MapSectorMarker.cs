using UnityEngine;

public class MapSectorMarker : MonoBehaviour
{
    public string sectorName = "Sector";
    public int flagsPerSector = 1;
    public Vector3 size = new Vector3(40f, 16f, 40f);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.62f, 0.15f, 0.95f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
