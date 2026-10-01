using UnityEngine;

public enum MarketPieceKind
{
    Model,
    Plaza,
    Counter,
    Flag
}

public struct MarketPiece
{
    public MarketPieceKind kind;
    public string objectName;
    public string modelFileName;
    public Vector3 localPosition;
    public float yawDegrees;
    public Vector3 size;

    public MarketPiece(
        MarketPieceKind kind,
        string objectName,
        string modelFileName,
        float x,
        float y,
        float z,
        float yawDegrees,
        float sizeX,
        float sizeY,
        float sizeZ)
    {
        this.kind = kind;
        this.objectName = objectName;
        this.modelFileName = modelFileName;
        localPosition = new Vector3(x, y, z);
        this.yawDegrees = yawDegrees;
        size = new Vector3(sizeX, sizeY, sizeZ);
    }
}

// Positions are in Kenney units. The recipe visualScale turns them into metres.
// Variation B of the commercial atlas paints these low blocks and awnings orange.
public static class MarketDistrictLayout
{
    public const string ModelFolder = "Assets/Art/Kenney/CityKitCommercial/Models";

    public static readonly MarketPiece[] Pieces =
    {
        new MarketPiece(MarketPieceKind.Plaza, "Plaza", "", 0f, 0f, 0f, 0f, 4.8f, 1f, 4.8f),

        new MarketPiece(MarketPieceKind.Model, "Shop_NorthWide", "low-detail-building-wide-a", -1.2f, 0f, 1.55f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Shop_NorthCenter", "low-detail-building-c", 0f, 0f, 1.55f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Shop_NorthEast", "low-detail-building-a", 1.2f, 0f, 1.55f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Shop_SouthWest", "low-detail-building-e", -1.2f, 0f, -1.55f, 0f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Shop_SouthEast", "low-detail-building-k", 1.2f, 0f, -1.55f, 0f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Kiosk", "low-detail-building-n", 1.9f, 0f, 0f, -90f, 0f, 0f, 0f),

        new MarketPiece(MarketPieceKind.Model, "Canopy_ShopNorthWide", "detail-awning-wide", -1.2f, 0f, 1.4f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Canopy_ShopNorthCenter", "detail-awning", 0f, 0f, 1.4f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Canopy_ShopNorthEast", "detail-awning", 1.2f, 0f, 1.4f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Canopy_ShopSouthWest", "detail-awning", -1.2f, 0f, -1.4f, 0f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Canopy_ShopSouthEast", "detail-awning", 1.2f, 0f, -1.4f, 0f, 0f, 0f, 0f),

        new MarketPiece(MarketPieceKind.Model, "Stall_NorthWide", "detail-awning-wide", -0.9f, 0f, 0.72f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Counter, "StallCounter_NorthWide", "", -0.9f, 0f, 0.52f, 0f, 0.7f, 0.1f, 0.22f),
        new MarketPiece(MarketPieceKind.Model, "Stall_North", "detail-awning", 0.05f, 0f, 0.72f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Counter, "StallCounter_North", "", 0.05f, 0f, 0.52f, 0f, 0.34f, 0.1f, 0.22f),
        new MarketPiece(MarketPieceKind.Model, "Stall_NorthEast", "detail-awning", 0.85f, 0f, 0.72f, 180f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Counter, "StallCounter_NorthEast", "", 0.85f, 0f, 0.52f, 0f, 0.34f, 0.1f, 0.22f),

        new MarketPiece(MarketPieceKind.Model, "Stall_SouthWide", "detail-awning-wide", -0.4f, 0f, -0.72f, 0f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Counter, "StallCounter_SouthWide", "", -0.4f, 0f, -0.52f, 0f, 0.7f, 0.1f, 0.22f),
        new MarketPiece(MarketPieceKind.Model, "Stall_South", "detail-awning", 0.75f, 0f, -0.72f, 0f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Counter, "StallCounter_South", "", 0.75f, 0f, -0.52f, 0f, 0.34f, 0.1f, 0.22f),

        new MarketPiece(MarketPieceKind.Model, "Parasol_NorthWest", "detail-parasol-b", -1.55f, 0f, 0.85f, 15f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Parasol_NorthEast", "detail-parasol-a", 1.55f, 0f, 0.85f, -20f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Parasol_SouthWest", "detail-parasol-a", -1.55f, 0f, -0.85f, 30f, 0f, 0f, 0f),
        new MarketPiece(MarketPieceKind.Model, "Parasol_SouthEast", "detail-parasol-b", 1.7f, 0f, -0.85f, -10f, 0f, 0f, 0f),

        new MarketPiece(MarketPieceKind.Flag, "CourtyardFlag", "", -0.35f, 0f, 0f, 0f, 0.22f, 0.9f, 0.12f),
        new MarketPiece(MarketPieceKind.Flag, "CourtyardFlag", "", 0.4f, 0f, 0.05f, 25f, 0.22f, 0.9f, 0.12f),
    };
}
