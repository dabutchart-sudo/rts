using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapDistrictKind
{
    Market = 0,
    Farm = 1,
    Industrial = 2,
    Airport = 3
}

[Serializable]
public class MapSectorDefinition
{
    public string sectorName = "Sector A";

    [Tooltip("How many courtyard flags to stamp in this sector. These are markers, not wired capture points.")]
    public int flagsPerSector = 1;

    public Vector3 center = Vector3.zero;
    public Vector3 size = new Vector3(40f, 16f, 40f);
}

[Serializable]
public class MapDistrictDefinition
{
    public string districtName = "Market";
    public MapDistrictKind kind = MapDistrictKind.Market;

    [Tooltip("Which sector in this recipe the district belongs to. The first sector is 0.")]
    public int sectorIndex = 0;

    public Vector3 localPosition = Vector3.zero;
    public float yawDegrees = 0f;

    [Tooltip("Kenney commercial blocks are about one metre across. 8 makes a Market readable from the RTS camera.")]
    public float visualScale = 8f;
}

[CreateAssetMenu(fileName = "MapRecipe", menuName = "RTS/Maps/Map Recipe")]
public class MapRecipe : ScriptableObject
{
    public string mapName = "Untitled Map";

    [TextArea(3, 6)]
    public string notes = "";

    public List<MapSectorDefinition> sectors = new List<MapSectorDefinition>();
    public List<MapDistrictDefinition> districts = new List<MapDistrictDefinition>();
}
