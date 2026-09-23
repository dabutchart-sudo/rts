using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayableDecorationPiece
{
    public string pieceId;
    public int sectorIndex;
    public string catalogId;
    public Vector3 position;
    public float yaw;
    public bool kept;
}

[Serializable]
public class PlayableSectorDefinition
{
    public string sectorName = "Sector A";
    public float minZ;
    public float maxZ;
    public List<Vector3> controlPoints = new List<Vector3>();
    public Vector3 attackerSpawn;
    public Vector3 defenderSpawn;
    public MapDistrictKind dressing = MapDistrictKind.Market;
}

[Serializable]
public class PlayableRoadSegment
{
    public string roadId;
    public Vector3 start;
    public Vector3 end;
}

[Serializable]
public class PlayableMapDefinition
{
    public const float MinDepth = 30f;
    public const float MinWidth = 28f;
    public const float SpineRoadWidth = 8f;

    public string mapName = "Untitled Map";
    public float minX = -20f;
    public float maxX = 20f;
    public List<PlayableSectorDefinition> sectors = new List<PlayableSectorDefinition>();
    public List<PlayableDecorationPiece> decoration = new List<PlayableDecorationPiece>();
    public List<Vector3> decorationBlocks = new List<Vector3>();
    public List<PlayableRoadSegment> roads = new List<PlayableRoadSegment>();
    public const int MinDecorationDensity = 1;
    public const int MaxDecorationDensity = 20;

    public int decorationSeed = 1;
    public int decorationDensity = 8;
    public bool decorationGenerated;

    public static string FolderPath => Path.Combine(Application.dataPath, "Data/Maps");

    public static float ContentInset(float span)
    {
        return Mathf.Clamp(span * 0.2f, 3f, 10f);
    }

    public static PlayableMapDefinition CreateBlank(string name, IList<int> pointsPerSector)
    {
        int count = Mathf.Clamp(pointsPerSector == null ? 1 : pointsPerSector.Count, 1, 6);
        float depth = 46f;
        float halfWidth = 20f;
        float startZ = -depth * count * 0.5f;

        var map = new PlayableMapDefinition
        {
            mapName = string.IsNullOrWhiteSpace(name) ? "Untitled Map" : name.Trim(),
            minX = -halfWidth,
            maxX = halfWidth,
            sectors = new List<PlayableSectorDefinition>()
        };

        for (int i = 0; i < count; i++)
        {
            int points = 1;
            if (pointsPerSector != null && i < pointsPerSector.Count)
            {
                points = Mathf.Clamp(pointsPerSector[i], 1, 4);
            }

            float sectorMinZ = startZ + (i * depth);
            float sectorMaxZ = sectorMinZ + depth;
            float midZ = (sectorMinZ + sectorMaxZ) * 0.5f;
            var sector = new PlayableSectorDefinition
            {
                sectorName = "Sector " + (char)('A' + i),
                minZ = sectorMinZ,
                maxZ = sectorMaxZ,
                controlPoints = new List<Vector3>()
            };

            for (int p = 0; p < points; p++)
            {
                float t = (p + 0.5f) / points;
                float x = Mathf.Lerp(map.minX, map.maxX, t);
                sector.controlPoints.Add(new Vector3(x, 0.5f, midZ));
            }

            float inset = ContentInset(depth);
            sector.attackerSpawn = new Vector3(0f, 0.5f, sectorMinZ + inset + 2f);
            sector.defenderSpawn = new Vector3(0f, 0.5f, sectorMaxZ - inset - 2f);
            map.sectors.Add(sector);
        }

        map.ClampAllContents();
        return map;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(mapName)) mapName = "Untitled Map";
        if (sectors == null) sectors = new List<PlayableSectorDefinition>();
        if (decoration == null) decoration = new List<PlayableDecorationPiece>();
        if (decorationBlocks == null) decorationBlocks = new List<Vector3>();
        if (roads == null) roads = new List<PlayableRoadSegment>();
        if (decorationDensity < MinDecorationDensity)
            decorationDensity = decorationGenerated ? 3 : 8;
        decorationDensity = Mathf.Clamp(decorationDensity, MinDecorationDensity, MaxDecorationDensity);
        foreach (PlayableSectorDefinition sector in sectors)
        {
            if (sector == null) continue;
            if (sector.controlPoints == null) sector.controlPoints = new List<Vector3>();
            if (sector.maxZ < sector.minZ)
            {
                float swap = sector.minZ;
                sector.minZ = sector.maxZ;
                sector.maxZ = swap;
            }
        }

        if (maxX < minX)
        {
            float swap = minX;
            minX = maxX;
            maxX = swap;
        }
    }

    public Bounds WorldBounds()
    {
        Normalize();
        if (sectors.Count == 0) return new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f));

        float minZ = sectors[0].minZ;
        float maxZ = sectors[sectors.Count - 1].maxZ;
        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        Vector3 size = new Vector3(Mathf.Max(1f, maxX - minX), 1f, Mathf.Max(1f, maxZ - minZ));
        return new Bounds(center, size);
    }

    public Bounds SectorBounds(int index)
    {
        Normalize();
        PlayableSectorDefinition sector = sectors[index];
        Vector3 center = new Vector3((minX + maxX) * 0.5f, 10f, (sector.minZ + sector.maxZ) * 0.5f);
        Vector3 size = new Vector3(Mathf.Max(1f, maxX - minX), 40f, Mathf.Max(1f, sector.maxZ - sector.minZ));
        return new Bounds(center, size);
    }

    public void ClampAllContents()
    {
        Normalize();
        if (maxX - minX < MinWidth) maxX = minX + MinWidth;

        for (int i = 0; i < sectors.Count; i++)
        {
            PlayableSectorDefinition sector = sectors[i];
            if (sector.maxZ - sector.minZ < MinDepth) sector.maxZ = sector.minZ + MinDepth;
            if (i > 0) sector.minZ = sectors[i - 1].maxZ;

            for (int p = 0; p < sector.controlPoints.Count; p++)
            {
                sector.controlPoints[p] = ClampInside(i, sector.controlPoints[p]);
            }

            sector.attackerSpawn = ClampInside(i, sector.attackerSpawn);
            sector.defenderSpawn = ClampInside(i, sector.defenderSpawn);
        }
    }

    public void CentreContents()
    {
        Normalize();
        float centerX = (minX + maxX) * 0.5f;
        for (int i = 0; i < sectors.Count; i++)
        {
            PlayableSectorDefinition sector = sectors[i];
            float depth = Mathf.Max(1f, sector.maxZ - sector.minZ);
            float inset = ContentInset(depth);
            float midZ = (sector.minZ + sector.maxZ) * 0.5f;
            sector.attackerSpawn = new Vector3(centerX, 0.5f, sector.minZ + inset + 2f);
            sector.defenderSpawn = new Vector3(centerX, 0.5f, sector.maxZ - inset - 2f);

            int points = sector.controlPoints.Count;
            for (int p = 0; p < points; p++)
            {
                float t = (p + 0.5f) / points;
                float x = Mathf.Lerp(minX, maxX, t);
                sector.controlPoints[p] = new Vector3(x, 0.5f, midZ);
            }
        }

        ClampAllContents();
    }

    public Vector3 ClampInside(int sectorIndex, Vector3 point)
    {
        PlayableSectorDefinition sector = sectors[sectorIndex];
        float insetX = ContentInset(maxX - minX);
        float insetZ = ContentInset(sector.maxZ - sector.minZ);
        point.x = Mathf.Clamp(point.x, minX + insetX, maxX - insetX);
        point.z = Mathf.Clamp(point.z, sector.minZ + insetZ, sector.maxZ - insetZ);
        point.y = 0.5f;
        return point;
    }

    public void MoveDepthEdge(int edgeIndex, float worldZ)
    {
        Normalize();
        if (sectors.Count == 0) return;

        if (edgeIndex <= 0)
        {
            sectors[0].minZ = Mathf.Min(worldZ, sectors[0].maxZ - MinDepth);
        }
        else if (edgeIndex >= sectors.Count)
        {
            int last = sectors.Count - 1;
            sectors[last].maxZ = Mathf.Max(worldZ, sectors[last].minZ + MinDepth);
        }
        else
        {
            PlayableSectorDefinition previous = sectors[edgeIndex - 1];
            PlayableSectorDefinition next = sectors[edgeIndex];
            float z = Mathf.Clamp(worldZ, previous.minZ + MinDepth, next.maxZ - MinDepth);
            previous.maxZ = z;
            next.minZ = z;
        }

        ClampAllContents();
    }

    public void MoveWidthEdge(bool minEdge, float worldX)
    {
        Normalize();
        if (minEdge) minX = Mathf.Min(worldX, maxX - MinWidth);
        else maxX = Mathf.Max(worldX, minX + MinWidth);
        ClampAllContents();
    }

    public static string FileNameFor(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName)) mapName = "Untitled Map";
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            mapName = mapName.Replace(invalid, '_');
        }

        mapName = mapName.Trim();
        if (string.IsNullOrEmpty(mapName)) mapName = "Untitled Map";
        return mapName + ".json";
    }

    public string Save()
    {
        Normalize();
        Directory.CreateDirectory(FolderPath);
        string fileName = FileNameFor(mapName);
        string path = Path.Combine(FolderPath, fileName);
        File.WriteAllText(path, JsonUtility.ToJson(this, true));
        return fileName;
    }

    public static PlayableMapDefinition Load(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string path = Path.Combine(FolderPath, fileName);
        if (!File.Exists(path)) return null;

        PlayableMapDefinition map = JsonUtility.FromJson<PlayableMapDefinition>(File.ReadAllText(path));
        if (map == null) return null;
        map.Normalize();
        return map;
    }

    public static List<string> ListSavedFileNames()
    {
        var names = new List<string>();
        if (!Directory.Exists(FolderPath)) return names;

        string[] files = Directory.GetFiles(FolderPath, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            names.Add(Path.GetFileName(file));
        }

        return names;
    }

    public void EnsureDecoration()
    {
        Normalize();
        if (decorationGenerated) return;
        AddGeneratedDecoration();
        decorationGenerated = true;
    }

    public void DressAgain()
    {
        Normalize();
        decoration.RemoveAll(piece => piece == null || !piece.kept);
        decorationSeed = decorationSeed >= int.MaxValue - 1 ? 1 : decorationSeed + 1;
        AddGeneratedDecoration();
        decorationGenerated = true;
    }

    public void SetDecorationDensity(int density)
    {
        Normalize();
        decorationDensity = Mathf.Clamp(density, MinDecorationDensity, MaxDecorationDensity);
        decoration.RemoveAll(piece => piece == null || !piece.kept);
        AddGeneratedDecoration();
        decorationGenerated = true;
    }

    public PlayableDecorationPiece FindDecoration(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId) || decoration == null) return null;
        for (int i = 0; i < decoration.Count; i++)
        {
            if (decoration[i] != null && decoration[i].pieceId == pieceId) return decoration[i];
        }

        return null;
    }

    public void KeepDecoration(string pieceId, Vector3 world)
    {
        PlayableDecorationPiece piece = FindDecoration(pieceId);
        if (piece == null) return;
        int sectorIndex = SectorIndexAt(world.z);
        if (sectorIndex < 0) sectorIndex = piece.sectorIndex;
        piece.sectorIndex = sectorIndex;
        piece.position = ClampInside(sectorIndex, world);
        piece.kept = true;
    }

    public void RemoveDecoration(string pieceId)
    {
        PlayableDecorationPiece piece = FindDecoration(pieceId);
        if (piece == null) return;
        decorationBlocks.Add(piece.position);
        decoration.Remove(piece);
    }

    public bool TryAddRoad(Vector3 start, Vector3 end)
    {
        Normalize();
        if (sectors.Count == 0) return false;

        Vector3 from = ClampToMap(start);
        Vector3 to = ClampToMap(end);
        if (Mathf.Abs(to.x - from.x) >= Mathf.Abs(to.z - from.z)) to.z = from.z;
        else to.x = from.x;
        if (Vector3.Distance(from, to) < 8f) return false;

        roads.Add(new PlayableRoadSegment
        {
            roadId = NextRoadId(),
            start = from,
            end = to
        });
        return true;
    }

    public void RemoveRoad(string roadId)
    {
        if (string.IsNullOrEmpty(roadId) || roads == null) return;
        for (int i = roads.Count - 1; i >= 0; i--)
        {
            if (roads[i] != null && roads[i].roadId == roadId) roads.RemoveAt(i);
        }
    }

    Vector3 ClampToMap(Vector3 world)
    {
        float minZ = sectors[0].minZ;
        float maxZ = sectors[sectors.Count - 1].maxZ;
        world.x = Mathf.Clamp(world.x, minX, maxX);
        world.y = 0f;
        world.z = Mathf.Clamp(world.z, minZ, maxZ);
        return world;
    }

    string NextRoadId()
    {
        int index = roads.Count;
        string id = "road-" + index;
        while (FindRoad(id) != null)
        {
            index++;
            id = "road-" + index;
        }

        return id;
    }

    PlayableRoadSegment FindRoad(string roadId)
    {
        if (roads == null) return null;
        for (int i = 0; i < roads.Count; i++)
        {
            if (roads[i] != null && roads[i].roadId == roadId) return roads[i];
        }

        return null;
    }

    public void SetSectorDressing(int index, MapDistrictKind kind)
    {
        Normalize();
        if (index < 0 || index >= sectors.Count || sectors[index] == null) return;
        if (sectors[index].dressing == kind) return;
        sectors[index].dressing = kind;
        decoration.RemoveAll(piece => piece != null && piece.sectorIndex == index && !piece.kept);
        AddGeneratedDecorationForSector(index);
        decorationGenerated = true;
    }

    public bool TryPlaceKeptDecoration(string catalogId, Vector3 world)
    {
        Normalize();
        if (string.IsNullOrEmpty(DecorationCatalog.Find(catalogId).id)) return false;
        if (sectors.Count == 0) return false;

        float minZ = sectors[0].minZ;
        float maxZ = sectors[sectors.Count - 1].maxZ;
        if (world.x < minX || world.x > maxX || world.z < minZ || world.z > maxZ) return false;

        int sectorIndex = SectorIndexAt(world.z);
        if (sectorIndex < 0) return false;

        decoration.Add(new PlayableDecorationPiece
        {
            pieceId = NextDecorationId(sectorIndex, decoration.Count),
            sectorIndex = sectorIndex,
            catalogId = catalogId,
            position = ClampInside(sectorIndex, world),
            yaw = 0f,
            kept = true
        });
        decorationGenerated = true;
        return true;
    }

    void AddGeneratedDecoration()
    {
        for (int sectorIndex = 0; sectorIndex < sectors.Count; sectorIndex++)
        {
            AddGeneratedDecorationForSector(sectorIndex);
        }
    }

    void AddGeneratedDecorationForSector(int sectorIndex)
    {
        if (sectorIndex < 0 || sectorIndex >= sectors.Count) return;
        PlayableSectorDefinition sector = sectors[sectorIndex];
        if (sector == null) return;

        DecorationCatalog.Entry[] catalog = DecorationCatalog.AutoDressEntries(DecorationCatalog.GroupName(sector.dressing));
        if (catalog.Length == 0) return;

            int already = 0;
            for (int i = 0; i < decoration.Count; i++)
            {
                if (decoration[i] != null && decoration[i].sectorIndex == sectorIndex) already++;
            }

            int room = decorationDensity - already;
            if (room <= 0) return;

            float inset = 4.5f;
            float step = 4.8f;
            var spots = new List<Vector3>();
            for (float z = sector.minZ + inset; z <= sector.maxZ - inset; z += step)
            {
                for (float x = minX + inset; x <= maxX - inset; x += step)
                {
                    spots.Add(new Vector3(x, 0f, z));
                }
            }

            var random = new System.Random(decorationSeed + (sectorIndex * 31));
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                Vector3 held = spots[i];
                spots[i] = spots[swap];
                spots[swap] = held;
            }

            int placed = 0;
            bool placeBarn = sector.dressing == MapDistrictKind.Farm && !SectorHasCatalog(sectorIndex, "barn");
            for (int i = 0; i < spots.Count && placed < room; i++)
            {
                if (!DecorationSpotIsClear(sectorIndex, spots[i])) continue;
                string catalogId = placeBarn ? "barn" : catalog[random.Next(catalog.Length)].id;
                placeBarn = false;
                decoration.Add(new PlayableDecorationPiece
                {
                    pieceId = NextDecorationId(sectorIndex, placed),
                    sectorIndex = sectorIndex,
                    catalogId = catalogId,
                    position = spots[i],
                    yaw = random.Next(0, 8) * 45f,
                    kept = false
                });
                placed++;
            }
    }

    public bool EnsureFarmLandmarks()
    {
        Normalize();
        bool added = false;
        for (int sectorIndex = 0; sectorIndex < sectors.Count; sectorIndex++)
        {
            PlayableSectorDefinition sector = sectors[sectorIndex];
            if (sector == null || sector.dressing != MapDistrictKind.Farm) continue;
            if (SectorHasCatalog(sectorIndex, "barn")) continue;
            if (TryPlaceCatalogInSector(sectorIndex, "barn")) added = true;
        }

        return added;
    }

    bool SectorHasCatalog(int sectorIndex, string catalogId)
    {
        if (decoration == null) return false;
        for (int i = 0; i < decoration.Count; i++)
        {
            PlayableDecorationPiece piece = decoration[i];
            if (piece != null && piece.sectorIndex == sectorIndex && piece.catalogId == catalogId) return true;
        }

        return false;
    }

    bool TryPlaceCatalogInSector(int sectorIndex, string catalogId)
    {
        PlayableSectorDefinition sector = sectors[sectorIndex];
        float inset = 4.5f;
        float step = 4.8f;
        for (float z = sector.minZ + inset; z <= sector.maxZ - inset; z += step)
        {
            for (float x = minX + inset; x <= maxX - inset; x += step)
            {
                Vector3 spot = new Vector3(x, 0f, z);
                if (!DecorationSpotIsClear(sectorIndex, spot)) continue;
                decoration.Add(new PlayableDecorationPiece
                {
                    pieceId = NextDecorationId(sectorIndex, decoration.Count),
                    sectorIndex = sectorIndex,
                    catalogId = catalogId,
                    position = spot,
                    yaw = 0f,
                    kept = false
                });
                return true;
            }
        }

        return false;
    }

    string NextDecorationId(int sectorIndex, int placed)
    {
        string id = "dec-" + decorationSeed + "-" + sectorIndex + "-" + placed;
        int extra = 0;
        while (FindDecoration(id) != null)
        {
            extra++;
            id = "dec-" + decorationSeed + "-" + sectorIndex + "-" + placed + "-" + extra;
        }

        return id;
    }

    bool DecorationSpotIsClear(int sectorIndex, Vector3 spot)
    {
        PlayableSectorDefinition sector = sectors[sectorIndex];
        if (Vector3.Distance(Flat(spot), Flat(sector.attackerSpawn)) < 9f) return false;
        if (Vector3.Distance(Flat(spot), Flat(sector.defenderSpawn)) < 9f) return false;
        if (sector.controlPoints != null)
        {
            for (int i = 0; i < sector.controlPoints.Count; i++)
            {
                if (Vector3.Distance(Flat(spot), Flat(sector.controlPoints[i])) < 8f) return false;
            }
        }

        float roadCenterX = (minX + maxX) * 0.5f;
        if (Mathf.Abs(spot.x - roadCenterX) < (SpineRoadWidth * 0.5f) + 1.5f) return false;
        if (roads != null)
        {
            for (int i = 0; i < roads.Count; i++)
            {
                PlayableRoadSegment road = roads[i];
                if (road == null) continue;
                bool alongX = Mathf.Abs(road.end.x - road.start.x) >= Mathf.Abs(road.end.z - road.start.z);
                float halfWidth = alongX ? 3f : SpineRoadWidth * 0.5f;
                if (DistanceToSegment(spot, road.start, road.end) < halfWidth + 1.5f) return false;
            }
        }

        for (int i = 0; i < decoration.Count; i++)
        {
            if (decoration[i] != null && Vector3.Distance(Flat(spot), Flat(decoration[i].position)) < 4.6f) return false;
        }

        for (int i = 0; i < decorationBlocks.Count; i++)
        {
            if (Vector3.Distance(Flat(spot), Flat(decorationBlocks[i])) < 4.6f) return false;
        }

        return true;
    }

    int SectorIndexAt(float worldZ)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            if (worldZ >= sectors[i].minZ && worldZ <= sectors[i].maxZ) return i;
        }

        return sectors.Count == 0 ? -1 : sectors.Count - 1;
    }

    static Vector3 Flat(Vector3 point)
    {
        point.y = 0f;
        return point;
    }

    static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 from = Flat(start);
        Vector3 to = Flat(end);
        Vector3 offset = to - from;
        float lengthSq = offset.sqrMagnitude;
        if (lengthSq < 0.01f) return Vector3.Distance(Flat(point), from);
        float t = Mathf.Clamp01(Vector3.Dot(Flat(point) - from, offset) / lengthSq);
        return Vector3.Distance(Flat(point), from + (offset * t));
    }
}
