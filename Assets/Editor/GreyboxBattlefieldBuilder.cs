#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Greybox Battlefield 01 as an authored three-sector Breakthrough test map.
/// The geometry is intentionally primitive and disposable: the layout, routes, sightlines
/// and objective relationships matter more than presentation at this stage.
/// </summary>
public static class GreyboxBattlefieldBuilder
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/GreyboxBattlefield01.unity";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";

    private static readonly Color Ground = new Color(0.30f, 0.42f, 0.24f);
    private static readonly Color Road = new Color(0.22f, 0.22f, 0.22f);
    private static readonly Color Track = new Color(0.38f, 0.34f, 0.25f);
    private static readonly Color Structure = new Color(0.46f, 0.44f, 0.40f);
    private static readonly Color Cover = new Color(0.34f, 0.33f, 0.30f);
    private static readonly Color Objective = new Color(0.72f, 0.62f, 0.18f);

    [MenuItem("RTS/Maps/Build Greybox Battlefield 01")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        if (!source.IsValid())
        {
            Debug.LogError("MAP BUILD: Could not open SampleScene.");
            return;
        }

        EditorSceneManager.SaveScene(source, TargetScene, true);
        Scene scene = SceneManager.GetActiveScene();

        GameObject oldGeometry = GameObject.Find(GeneratedRootName);
        if (oldGeometry != null) Object.DestroyImmediate(oldGeometry);

        GameObject root = new GameObject(GeneratedRootName);
        CreateMapDefinition(root);
        CreateTerrain(root.transform);
        CreateRoadNetwork(root.transform);
        CreateSectorOne(root.transform);
        CreateSectorTwo(root.transform);
        CreateSectorThree(root.transform);
        CreateRouteMarkers(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureSceneInBuildSettings(TargetScene);
        Selection.activeGameObject = root;

        Debug.Log("MAP BUILD: Greybox Battlefield 01 v0.2 created - widened battlefield, connected flanks and three distinct combat sectors.");
    }

    private static void CreateMapDefinition(GameObject root)
    {
        MapDefinition definition = root.AddComponent<MapDefinition>();
        definition.mapId = "greybox_battlefield_01";
        definition.displayName = "Greybox Battlefield 01";
        definition.description = "Three-sector Breakthrough battlefield: open rural approach, dense industrial crossing, fortified final position.";
    }

    private static void CreateTerrain(Transform root)
    {
        GameObject terrain = new GameObject("TERRAIN BLOCKOUT");
        terrain.transform.SetParent(root);

        CreateBox(terrain.transform, "Battlefield Ground", new Vector3(0f, -0.65f, 125f), new Vector3(190f, 1f, 310f), Ground);

        // Low broad terraces create sightline changes without yet requiring Unity Terrain.
        CreateBox(terrain.transform, "West Rise S1", new Vector3(-55f, -0.15f, 45f), new Vector3(62f, 0.9f, 62f), new Color(0.32f, 0.44f, 0.25f));
        CreateBox(terrain.transform, "East Rise S2", new Vector3(54f, -0.10f, 140f), new Vector3(54f, 1.0f, 70f), new Color(0.31f, 0.43f, 0.24f));
        CreateBox(terrain.transform, "Final Ridge", new Vector3(0f, 0.00f, 225f), new Vector3(150f, 1.3f, 76f), new Color(0.32f, 0.44f, 0.25f));
    }

    private static void CreateRoadNetwork(Transform root)
    {
        GameObject roads = new GameObject("ROADS AND ROUTES");
        roads.transform.SetParent(root);

        // Main road bends through the battlefield rather than providing a single long firing lane.
        CreateRouteSegment(roads.transform, "Main Road 01", new Vector3(-8f, 0.04f, 8f), new Vector3(4f, 0.04f, 60f), 14f, Road);
        CreateRouteSegment(roads.transform, "Main Road 02", new Vector3(4f, 0.04f, 60f), new Vector3(-10f, 0.04f, 112f), 14f, Road);
        CreateRouteSegment(roads.transform, "Main Road 03", new Vector3(-10f, 0.04f, 112f), new Vector3(12f, 0.04f, 164f), 14f, Road);
        CreateRouteSegment(roads.transform, "Main Road 04", new Vector3(12f, 0.04f, 164f), new Vector3(-6f, 0.04f, 214f), 14f, Road);
        CreateRouteSegment(roads.transform, "Main Road 05", new Vector3(-6f, 0.04f, 214f), new Vector3(5f, 0.04f, 278f), 14f, Road);

        // West route is vehicle-friendly but repeatedly reconnects to the centre.
        CreateRouteSegment(roads.transform, "West Flank 01", new Vector3(-8f, 0.05f, 20f), new Vector3(-67f, 0.05f, 52f), 8f, Track);
        CreateRouteSegment(roads.transform, "West Flank 02", new Vector3(-67f, 0.05f, 52f), new Vector3(-60f, 0.05f, 113f), 8f, Track);
        CreateRouteSegment(roads.transform, "West Reconnect S2", new Vector3(-60f, 0.05f, 113f), new Vector3(-18f, 0.05f, 139f), 8f, Track);
        CreateRouteSegment(roads.transform, "West Flank 03", new Vector3(-60f, 0.05f, 113f), new Vector3(-70f, 0.05f, 190f), 8f, Track);
        CreateRouteSegment(roads.transform, "West Reconnect S3", new Vector3(-70f, 0.05f, 190f), new Vector3(-25f, 0.05f, 222f), 8f, Track);

        // East route is narrower and intended primarily as an infantry alternative.
        CreateRouteSegment(roads.transform, "East Flank 01", new Vector3(6f, 0.06f, 34f), new Vector3(66f, 0.06f, 68f), 5f, Track);
        CreateRouteSegment(roads.transform, "East Flank 02", new Vector3(66f, 0.06f, 68f), new Vector3(58f, 0.06f, 128f), 5f, Track);
        CreateRouteSegment(roads.transform, "East Reconnect S2", new Vector3(58f, 0.06f, 128f), new Vector3(25f, 0.06f, 151f), 5f, Track);
        CreateRouteSegment(roads.transform, "East Flank 03", new Vector3(58f, 0.06f, 128f), new Vector3(72f, 0.06f, 193f), 5f, Track);
        CreateRouteSegment(roads.transform, "East Reconnect S3", new Vector3(72f, 0.06f, 193f), new Vector3(28f, 0.06f, 231f), 5f, Track);
    }

    private static void CreateSectorOne(Transform root)
    {
        GameObject sector = NewGroup(root, "SECTOR 1 - FARM APPROACH");

        CreateObjectivePad(sector.transform, "A1 FARM", new Vector3(-38f, 0.65f, 63f));
        CreateObjectivePad(sector.transform, "A2 CHECKPOINT", new Vector3(32f, 0.15f, 78f));

        CreateBuilding(sector.transform, "Farmhouse", new Vector3(-48f, 3.4f, 61f), new Vector3(16f, 6f, 12f));
        CreateBuilding(sector.transform, "Barn", new Vector3(-29f, 3.0f, 50f), new Vector3(14f, 5f, 20f));
        CreateWall(sector.transform, "Farm Wall North", new Vector3(-42f, 1.3f, 76f), new Vector3(32f, 1.6f, 1.4f));
        CreateWall(sector.transform, "Farm Wall East", new Vector3(-23f, 1.3f, 68f), new Vector3(1.4f, 1.6f, 17f));

        CreateBuilding(sector.transform, "Checkpoint Hut", new Vector3(38f, 1.7f, 78f), new Vector3(8f, 3f, 8f));
        CreateWall(sector.transform, "Checkpoint Barrier", new Vector3(23f, 0.8f, 72f), new Vector3(22f, 1.4f, 1.2f));

        // Tree/rock analogues create broken long-range sightlines while preserving open-field character.
        CreateCoverCluster(sector.transform, new Vector3(-2f, 0.7f, 57f), 6, 12f);
        CreateCoverCluster(sector.transform, new Vector3(54f, 0.7f, 92f), 4, 9f);
        CreateWall(sector.transform, "Sunken Lane Bank", new Vector3(4f, 0.7f, 95f), new Vector3(34f, 1.2f, 2.5f), 12f);
    }

    private static void CreateSectorTwo(Transform root)
    {
        GameObject sector = NewGroup(root, "SECTOR 2 - INDUSTRIAL CROSSING");

        CreateObjectivePad(sector.transform, "B1 WAREHOUSE", new Vector3(-34f, 0.15f, 143f));
        CreateObjectivePad(sector.transform, "B2 DEPOT", new Vector3(37f, 0.65f, 154f));

        CreateBuilding(sector.transform, "Warehouse West", new Vector3(-46f, 4f, 139f), new Vector3(24f, 8f, 18f));
        CreateBuilding(sector.transform, "Workshop", new Vector3(-24f, 3f, 160f), new Vector3(15f, 6f, 12f));
        CreateBuilding(sector.transform, "Depot Office", new Vector3(45f, 3.1f, 149f), new Vector3(13f, 5f, 11f));
        CreateBuilding(sector.transform, "Machine Hall", new Vector3(22f, 3.5f, 174f), new Vector3(19f, 7f, 13f));

        CreateBox(sector.transform, "Container 1", new Vector3(12f, 1.4f, 143f), new Vector3(4f, 2.6f, 11f), new Color(0.35f, 0.38f, 0.40f));
        CreateBox(sector.transform, "Container 2", new Vector3(25f, 1.4f, 151f), new Vector3(11f, 2.6f, 4f), new Color(0.40f, 0.34f, 0.30f));
        CreateBox(sector.transform, "Container 3", new Vector3(55f, 1.9f, 169f), new Vector3(4f, 2.6f, 12f), new Color(0.32f, 0.37f, 0.34f));

        // Broken defensive line: centre, west and east approaches all remain viable.
        CreateWall(sector.transform, "Industrial Wall West", new Vector3(-51f, 1.4f, 121f), new Vector3(47f, 2.6f, 1.6f));
        CreateWall(sector.transform, "Industrial Wall Centre", new Vector3(-4f, 1.4f, 125f), new Vector3(23f, 2.6f, 1.6f), -8f);
        CreateWall(sector.transform, "Industrial Wall East", new Vector3(45f, 1.4f, 124f), new Vector3(38f, 2.6f, 1.6f));

        CreateCoverCluster(sector.transform, new Vector3(1f, 0.7f, 157f), 9, 10f);
        CreateCoverCluster(sector.transform, new Vector3(63f, 1.1f, 145f), 5, 8f);
    }

    private static void CreateSectorThree(Transform root)
    {
        GameObject sector = NewGroup(root, "SECTOR 3 - FORTIFIED RIDGE");

        CreateObjectivePad(sector.transform, "C1 COMMAND", new Vector3(-34f, 0.85f, 231f));
        CreateObjectivePad(sector.transform, "C2 BATTERY", new Vector3(36f, 0.85f, 242f));

        CreateBuilding(sector.transform, "Command Block", new Vector3(-45f, 4.8f, 231f), new Vector3(21f, 8f, 17f));
        CreateBuilding(sector.transform, "Battery Bunker", new Vector3(45f, 3.2f, 242f), new Vector3(19f, 4.5f, 14f));
        CreateBuilding(sector.transform, "Signals Building", new Vector3(-2f, 3.7f, 254f), new Vector3(15f, 6f, 12f));

        // Three deliberate breaches: west vehicle approach, central road and east infantry route.
        CreateWall(sector.transform, "Final Line Far West", new Vector3(-68f, 1.7f, 207f), new Vector3(29f, 2.2f, 1.8f), 7f);
        CreateWall(sector.transform, "Final Line West", new Vector3(-31f, 1.7f, 211f), new Vector3(27f, 2.2f, 1.8f), 7f);
        CreateWall(sector.transform, "Final Line East", new Vector3(29f, 1.7f, 209f), new Vector3(30f, 2.2f, 1.8f), -5f);
        CreateWall(sector.transform, "Final Line Far East", new Vector3(67f, 1.7f, 205f), new Vector3(24f, 2.2f, 1.8f), -5f);

        CreateWall(sector.transform, "Command Courtyard", new Vector3(-35f, 1.6f, 245f), new Vector3(31f, 1.8f, 1.5f));
        CreateWall(sector.transform, "Battery Courtyard", new Vector3(36f, 1.6f, 229f), new Vector3(28f, 1.8f, 1.5f));
        CreateCoverCluster(sector.transform, new Vector3(0f, 1.2f, 228f), 10, 9f);
        CreateCoverCluster(sector.transform, new Vector3(60f, 1.2f, 224f), 4, 8f);
    }

    private static void CreateRouteMarkers(Transform root)
    {
        GameObject notes = NewGroup(root, "ROUTE DESIGN NOTES");
        CreateMarker(notes.transform, "ATTACKER START", new Vector3(-8f, 0.2f, -12f), new Color(0.2f, 0.55f, 0.9f));
        CreateMarker(notes.transform, "DEFENDER FINAL BASE", new Vector3(4f, 0.9f, 282f), new Color(0.8f, 0.2f, 0.2f));
        CreateMarker(notes.transform, "WEST VEHICLE FLANK", new Vector3(-67f, 0.2f, 102f), new Color(0.9f, 0.75f, 0.2f));
        CreateMarker(notes.transform, "EAST INFANTRY FLANK", new Vector3(66f, 0.2f, 102f), new Color(0.9f, 0.75f, 0.2f));
    }

    private static GameObject NewGroup(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        return go;
    }

    private static void CreateObjectivePad(Transform parent, string name, Vector3 position)
    {
        CreateCylinder(parent, name, position, new Vector3(9f, 0.12f, 9f), Objective);
    }

    private static void CreateBuilding(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        CreateBox(parent, name, position, scale, Structure);
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, float yaw = 0f)
    {
        GameObject wall = CreateBox(parent, name, position, scale, new Color(0.40f, 0.40f, 0.38f));
        wall.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static void CreateCoverCluster(Transform parent, Vector3 centre, int count, float spacing)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * 137.5f * Mathf.Deg2Rad;
            float radius = 4f + (i % 4) * spacing * 0.45f;
            Vector3 pos = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 scale = i % 3 == 0 ? new Vector3(5f, 1.25f, 1.5f) : new Vector3(2.4f, 1.3f, 2.4f);
            GameObject cover = CreateBox(parent, "Cover " + (i + 1), pos, scale, Cover);
            cover.transform.position += Vector3.up * (scale.y * 0.5f);
            cover.transform.rotation = Quaternion.Euler(0f, (i * 31f) % 180f, 0f);
        }
    }

    private static void CreateRouteSegment(Transform parent, string name, Vector3 start, Vector3 end, float width, Color color)
    {
        Vector3 delta = end - start;
        Vector3 centre = (start + end) * 0.5f;
        float length = new Vector2(delta.x, delta.z).magnitude;
        float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

        GameObject segment = CreateBox(parent, name, centre, new Vector3(width, 0.12f, length), color);
        segment.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        return go;
    }

    private static GameObject CreateCylinder(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        return go;
    }

    private static void CreateMarker(Transform parent, string name, Vector3 position, Color color)
    {
        GameObject marker = CreateCylinder(parent, name, position, new Vector3(4f, 0.1f, 4f), color);
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        Material material = new Material(shader);
        material.color = color;
        renderer.sharedMaterial = material;
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene entry in existing)
        {
            if (entry.path == scenePath) return;
        }

        EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
        existing.CopyTo(updated, 0);
        updated[updated.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }
}
#endif
