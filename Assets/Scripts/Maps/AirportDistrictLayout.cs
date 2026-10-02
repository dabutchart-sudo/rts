using UnityEngine;

// Positions are in the same units as the Market layout. The recipe visualScale turns them into metres.
// The open apron is the courtyard. Flags stand there, not on the hangar or the tower.
public static class AirportDistrictLayout
{
    public struct Block
    {
        public readonly string objectName;
        public readonly Vector3 localPosition;
        public readonly Vector3 size;
        public readonly float yawDegrees;
        public readonly Color color;

        public Block(string objectName, float x, float y, float z, float yawDegrees, float sizeX, float sizeY, float sizeZ, Color color)
        {
            this.objectName = objectName;
            localPosition = new Vector3(x, y, z);
            this.yawDegrees = yawDegrees;
            size = new Vector3(sizeX, sizeY, sizeZ);
            this.color = color;
        }
    }

    static readonly Color Concrete = new Color(0.62f, 0.63f, 0.60f);
    static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.25f);
    static readonly Color Paint = new Color(0.93f, 0.93f, 0.90f);
    static readonly Color Yellow = new Color(0.86f, 0.70f, 0.16f);
    static readonly Color Hangar = new Color(0.62f, 0.68f, 0.74f);
    static readonly Color Roof = new Color(0.32f, 0.35f, 0.38f);
    static readonly Color Door = new Color(0.18f, 0.19f, 0.20f);
    static readonly Color Plane = new Color(0.90f, 0.91f, 0.88f);
    static readonly Color Wing = new Color(0.35f, 0.38f, 0.42f);
    static readonly Color Tail = new Color(0.20f, 0.35f, 0.62f);
    static readonly Color Crate = new Color(0.55f, 0.42f, 0.22f);
    static readonly Color Tower = new Color(0.78f, 0.80f, 0.82f);
    static readonly Color Glass = new Color(0.45f, 0.62f, 0.78f);

    public static readonly Block[] Blocks =
    {
        new Block("Apron", 0.05f, 0.02f, 0.15f, 0f, 2.6f, 0.04f, 2.2f, Concrete),
        new Block("BayMark_North", 0.05f, 0.045f, 0.95f, 0f, 1.4f, 0.012f, 0.06f, Yellow),
        new Block("BayMark_South", 0.05f, 0.045f, -0.65f, 0f, 1.4f, 0.012f, 0.06f, Yellow),

        new Block("Runway", 1.35f, 0.03f, -0.15f, 0f, 0.48f, 0.03f, 2.8f, Asphalt),
        new Block("RunwayDash_1", 1.35f, 0.05f, -0.9f, 0f, 0.08f, 0.012f, 0.28f, Paint),
        new Block("RunwayDash_2", 1.35f, 0.05f, -0.15f, 0f, 0.08f, 0.012f, 0.28f, Paint),
        new Block("RunwayDash_3", 1.35f, 0.05f, 0.6f, 0f, 0.08f, 0.012f, 0.28f, Paint),

        new Block("Hangar", -1.75f, 0.36f, 0.1f, 0f, 1.15f, 0.7f, 1.45f, Hangar),
        new Block("HangarRoof", -1.75f, 0.76f, 0.1f, 0f, 1.28f, 0.08f, 1.58f, Roof),
        new Block("HangarDoor", -1.16f, 0.28f, 0.1f, 0f, 0.04f, 0.5f, 0.72f, Door),

        new Block("TowerBase", 1.55f, 0.12f, 1.25f, 0f, 0.42f, 0.24f, 0.42f, Concrete),
        new Block("TowerShaft", 1.55f, 0.55f, 1.25f, 0f, 0.22f, 0.62f, 0.22f, Tower),
        new Block("TowerCab", 1.55f, 0.98f, 1.25f, 0f, 0.4f, 0.26f, 0.4f, Glass),
        new Block("TowerRoof", 1.55f, 1.16f, 1.25f, 0f, 0.5f, 0.06f, 0.5f, Roof),

        new Block("PlaneA_Body", 0.35f, 0.16f, 0.95f, 0f, 0.85f, 0.14f, 0.16f, Plane),
        new Block("PlaneA_Wing", 0.28f, 0.14f, 0.95f, 0f, 0.22f, 0.03f, 0.85f, Wing),
        new Block("PlaneA_Tail", -0.05f, 0.26f, 0.95f, 0f, 0.12f, 0.16f, 0.04f, Tail),

        new Block("PlaneB_Body", -0.35f, 0.16f, -0.85f, 0f, 0.85f, 0.14f, 0.16f, Plane),
        new Block("PlaneB_Wing", -0.42f, 0.14f, -0.85f, 0f, 0.22f, 0.03f, 0.85f, Wing),
        new Block("PlaneB_Tail", -0.75f, 0.26f, -0.85f, 0f, 0.12f, 0.16f, 0.04f, Tail),

        new Block("Crate", -0.95f, 0.12f, -0.7f, 0f, 0.28f, 0.22f, 0.24f, Crate)
    };

    public static readonly MarketPiece[] FlagSlots =
    {
        new MarketPiece(MarketPieceKind.Flag, "CourtyardFlag", "", 0.15f, 0.04f, 0.2f, 10f, 0.22f, 0.9f, 0.12f),
        new MarketPiece(MarketPieceKind.Flag, "CourtyardFlag", "", -0.25f, 0.04f, -0.15f, -15f, 0.22f, 0.9f, 0.12f)
    };
}
