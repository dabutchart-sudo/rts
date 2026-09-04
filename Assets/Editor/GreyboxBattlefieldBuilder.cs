#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the first authored Breakthrough greybox as a separate Unity scene.
/// It intentionally builds geometry from primitives so the layout remains easy to alter.
/// Gameplay objects are left clearly named for the next wiring pass.
/// </summary>
public static class GreyboxBattlefieldBuilder
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/GreyboxBattlefield01.unity";

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

        GameObject oldGeometry = GameObject.Find("GreyboxBattlefield01_Generated");
        if (oldGeometry != null) Object.DestroyImmediate(oldGeometry);

        GameObject root = new GameObject("GreyboxBattlefield01_Generated");
        CreateMapDefinition(root);
        CreateGround(root.transform);
        CreateSectorOne(root.transform);
        CreateSectorTwo(root.transform);
        CreateSectorThree(root.transform);
        CreateRouteMarkers(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EnsureSceneInBuildSettings(TargetScene);
        Selection.activeGameObject = root;
        Debug.Log("MAP BUILD: Greybox Battlefield 01 created. This is the geometry/layout pass; gameplay-object rewiring follows after visual inspection.");
    }

    private static void CreateMapDefinition(GameObject root)
    {
        MapDefinition definition = root.AddComponent<MapDefinition>();
        definition.mapId = "greybox_battlefield_01";
        definition.displayName = "Greybox Battlefield 01";
        definition.description = "Three-sector Breakthrough battlefield: rural approach, industrial choke, fortified final sector.";
    }

    private static void CreateGround(Transform root)
    {
        CreateBox(root, "Battlefield Ground", new Vector3(0f, -0.6f, 120f), new Vector3(150f, 1f, 300f), new Color(0.30f, 0.42f, 0.24f));
        CreateBox(root, "Main Vehicle Road", new Vector3(0f, 0.02f, 120f), new Vector3(14f, 0.12f, 290f), new Color(0.22f, 0.22f, 0.22f));
        CreateBox(root, "West Service Route", new Vector3(-52f, 0.03f, 120f), new Vector3(7f, 0.14f, 270f), new Color(0.33f, 0.30f, 0.25f));
        CreateBox(root, "East Infantry Track", new Vector3(54f, 0.03f, 120f), new Vector3(5f, 0.14f, 270f), new Color(0.38f, 0.34f, 0.25f));
    }

    private static void CreateSectorOne(Transform root)
    {
        GameObject sector = new GameObject("SECTOR 1 - FARM APPROACH");
        sector.transform.SetParent(root);

        // A1: farm compound on the west. A2: roadside checkpoint on the east.
        CreateObjectivePad(sector.transform, "A1 FARM", new Vector3(-28f, 0.1f, 50f));
        CreateObjectivePad(sector.transform, "A2 CHECKPOINT", new Vector3(27f, 0.1f, 68f));

        CreateBuilding(sector.transform, "Farmhouse", new Vector3(-35f, 3f, 48f), new Vector3(15f, 6f, 11f));
        CreateBuilding(sector.transform, "Barn", new Vector3(-20f, 2.5f, 40f), new Vector3(13f, 5f, 18f));
        CreateWallLine(sector.transform, "Farm Wall", new Vector3(-29f, 0.8f, 61f), new Vector3(35f, 1.6f, 1.3f));

        CreateBox(sector.transform, "Checkpoint Hut", new Vector3(31f, 1.5f, 68f), new Vector3(7f, 3f, 7f), new Color(0.48f, 0.45f, 0.38f));
        CreateWallLine(sector.transform, "Checkpoint Barrier West", new Vector3(10f, 0.7f, 64f), new Vector3(20f, 1.4f, 1f));
        CreateWallLine(sector.transform, "Checkpoint Barrier East", new Vector3(43f, 0.7f, 73f), new Vector3(18f, 1.4f, 1f));

        CreateCoverScatter(sector.transform, new Vector3(-5f, 0.6f, 58f), 7, 13f);
    }

    private static void CreateSectorTwo(Transform root)
    {
        GameObject sector = new GameObject("SECTOR 2 - INDUSTRIAL CROSSING");
        sector.transform.SetParent(root);

        CreateObjectivePad(sector.transform, "B1 WAREHOUSE", new Vector3(-25f, 0.1f, 132f));
        CreateObjectivePad(sector.transform, "B2 DEPOT", new Vector3(28f, 0.1f, 143f));

        CreateBuilding(sector.transform, "Warehouse North", new Vector3(-31f, 4f, 126f), new Vector3(22f, 8f, 17f));
        CreateBuilding(sector.transform, "Warehouse South", new Vector3(-25f, 3f, 148f), new Vector3(16f, 6f, 12f));
        CreateBuilding(sector.transform, "Depot Office", new Vector3(31f, 2.5f, 138f), new Vector3(12f, 5f, 10f));
        CreateBox(sector.transform, "Container 1", new Vector3(17f, 1.3f, 151f), new Vector3(4f, 2.6f, 11f), new Color(0.35f, 0.38f, 0.40f));
        CreateBox(sector.transform, "Container 2", new Vector3(39f, 1.3f, 154f), new Vector3(4f, 2.6f, 11f), new Color(0.40f, 0.34f, 0.30f));
        CreateBox(sector.transform, "Container 3", new Vector3(42f, 1.3f, 129f), new Vector3(11f, 2.6f, 4f), new Color(0.32f, 0.37f, 0.34f));

        // Deliberate central choke with gaps at the road and both outer flanks.
        CreateWallLine(sector.transform, "Industrial Wall West", new Vector3(-32f, 1.3f, 116f), new Vector3(49f, 2.6f, 1.6f));
        CreateWallLine(sector.transform, "Industrial Wall East", new Vector3(33f, 1.3f, 116f), new Vector3(43f, 2.6f, 1.6f));
        CreateCoverScatter(sector.transform, new Vector3(5f, 0.6f, 139f), 10, 16f);
    }

    private static void CreateSectorThree(Transform root)
    {
        GameObject sector = new GameObject("SECTOR 3 - FORTIFIED RIDGE");
        sector.transform.SetParent(root);

        CreateObjectivePad(sector.transform, "C1 COMMAND", new Vector3(-22f, 0.1f, 220f));
        CreateObjectivePad(sector.transform, "C2 BATTERY", new Vector3(25f, 0.1f, 232f));

        CreateBuilding(sector.transform, "Command Block", new Vector3(-24f, 4f, 221f), new Vector3(19f, 8f, 16f));
        CreateBuilding(sector.transform, "Battery Bunker", new Vector3(28f, 2.2f, 231f), new Vector3(18f, 4.4f, 13f));
        CreateWallLine(sector.transform, "Final Defence West", new Vector3(-31f, 1f, 202f), new Vector3(50f, 2f, 1.8f));
        CreateWallLine(sector.transform, "Final Defence East", new Vector3(34f, 1f, 202f), new Vector3(42f, 2f, 1.8f));

        // Inner defensive pockets make the final sector deliberately more claustrophobic.
        CreateWallLine(sector.transform, "Command Courtyard North", new Vector3(-23f, 0.9f, 233f), new Vector3(30f, 1.8f, 1.4f));
        CreateWallLine(sector.transform, "Battery Courtyard South", new Vector3(27f, 0.9f, 219f), new Vector3(27f, 1.8f, 1.4f));
        CreateCoverScatter(sector.transform, new Vector3(0f, 0.6f, 220f), 12, 17f);
    }

    private static void CreateRouteMarkers(Transform root)
    {
        GameObject routes = new GameObject("ROUTE DESIGN NOTES");
        routes.transform.SetParent(root);
        CreateMarker(routes.transform, "ATTACKER START", new Vector3(0f, 0.15f, -10f), new Color(0.2f, 0.55f, 0.9f));
        CreateMarker(routes.transform, "DEFENDER FINAL BASE", new Vector3(0f, 0.15f, 270f), new Color(0.8f, 0.2f, 0.2f));
        CreateMarker(routes.transform, "WEST VEHICLE FLANK", new Vector3(-52f, 0.15f, 130f), new Color(0.9f, 0.75f, 0.2f));
        CreateMarker(routes.transform, "EAST INFANTRY FLANK", new Vector3(54f, 0.15f, 130f), new Color(0.9f, 0.75f, 0.2f));
    }

    private static void CreateObjectivePad(Transform parent, string name, Vector3 position)
    {
        CreateCylinder(parent, name, position, new Vector3(9f, 0.15f, 9f), new Color(0.72f, 0.62f, 0.18f));
    }

    private static void CreateBuilding(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        CreateBox(parent, name, position, scale, new Color(0.46f, 0.44f, 0.40f));
    }

    private static void CreateWallLine(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        CreateBox(parent, name, position, scale, new Color(0.40f, 0.40f, 0.38f));
    }

    private static void CreateCoverScatter(Transform parent, Vector3 centre, int count, float spacing)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * 137.5f * Mathf.Deg2Rad;
            float radius = 5f + (i % 4) * spacing * 0.35f;
            Vector3 pos = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 scale = i % 3 == 0 ? new Vector3(5f, 1.2f, 1.4f) : new Vector3(2.2f, 1.2f, 2.2f);
            CreateBox(parent, "Cover " + (i + 1), pos + Vector3.up * 0.6f, scale, new Color(0.34f, 0.33f, 0.30f));
        }
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
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (material.shader == null) material = new Material(Shader.Find("Standard"));
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
