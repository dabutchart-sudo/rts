using UnityEngine;

public class MapEditHandle : MonoBehaviour
{
    public enum Kind
    {
        WidthMin,
        WidthMax,
        DepthEdge,
        ControlPoint,
        AttackerSpawn,
        DefenderSpawn,
        Decoration,
        DecorationRemove
    }

    public Kind kind;
    public int sectorIndex;
    public int pointIndex;
    public int depthEdgeIndex;
    public string decorationId;
}
