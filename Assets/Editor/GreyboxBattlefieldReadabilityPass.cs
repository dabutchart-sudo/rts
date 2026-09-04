#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// v0.3 readability pass for Greybox Battlefield 01.
/// Keeps the authored route layout, but removes design-only objective discs, opens clear
/// capture courtyards and pushes the visual language toward clean modular/right-angle spaces.
/// Safe to run repeatedly after the gameplay wiring command.
/// </summary>
public static class GreyboxBattlefieldReadabilityPass
{
    private const string SceneName = "GreyboxBattlefield01";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";

    [MenuItem("RTS/Maps/Apply Greybox Battlefield 01 v0.3 Readability Pass")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            EditorUtility.DisplayDialog(
                "Greybox Battlefield 01",
                "Open GreyboxBattlefield01 first, then run this command again.",
                "OK");
            return;
        }

        GameObject rootObject = GameObject.Find(GeneratedRootName);
        if (rootObject == null)
        {
            Debug.LogError("MAP READABILITY: Generated battlefield root was not found.");
            return;
        }

        Transform root = rootObject.transform;

        // These were useful while designing the layout, but duplicate the real CapturePoints.
        SetDesignMarkerActive(root, "A1 FARM", false);
        SetDesignMarkerActive(root, "A2 CHECKPOINT", false);
        SetDesignMarkerActive(root, "B1 WAREHOUSE", false);
        SetDesignMarkerActive(root, "B2 DEPOT", false);
        SetDesignMarkerActive(root, "C1 COMMAND", false);
        SetDesignMarkerActive(root, "C2 BATTERY", false);

        // Give each real objective a deliberately readable capture courtyard.
        // Buildings remain close enough to matter as cover, but no longer sit over the zone.
        Move(root, "Farmhouse", new Vector3(-52f, 3f, 55f), 0f);
        Move(root, "Barn", new Vector3(-27f, 2.5f, 47f), 0f);
        Move(root, "Farm Wall North", new Vector3(-42f, 0.8f, 78f), 0f);
        Move(root, "Farm Wall East", new Vector3(-21f, 0.8f, 67f), 0f);

        Move(root, "Checkpoint Hut", new Vector3(44f, 1.5f, 78f), 0f);
        Move(root, "Checkpoint Barrier", new Vector3(24f, 0.7f, 68f), 0f);

        Move(root, "Warehouse West", new Vector3(-50f, 4f, 136f), 0f);
        Move(root, "Workshop", new Vector3(-20f, 3f, 163f), 0f);
        Move(root, "Depot Office", new Vector3(50f, 2.5f, 151f), 0f);
        Move(root, "Machine Hall", new Vector3(23f, 3.5f, 176f), 0f);

        Move(root, "Command Block", new Vector3(-51f, 4f, 228f), 0f);
        Move(root, "Battery Bunker", new Vector3(51f, 2.2f, 244f), 0f);
        Move(root, "Signals Building", new Vector3(-3f, 3f, 259f), 0f);
        Move(root, "Command Courtyard", new Vector3(-34f, 0.9f, 247f), 0f);
        Move(root, "Battery Courtyard", new Vector3(36f, 0.9f, 225f), 0f);

        // Smaller world-space labels read better at the full tactical zoom.
        CapturePoint[] points = Object.FindObjectsByType<CapturePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CapturePoint point in points)
        {
            if (point == null || !point.transform.IsChildOf(root)) continue;
            point.captureLabelScale = 0.012f;
            point.groundOffset = 0.2f;
            EditorUtility.SetDirty(point);
        }

        RebuildNavigation(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = rootObject;

        Debug.Log(
            "MAP READABILITY: Greybox Battlefield 01 v0.3 applied - design discs hidden, " +
            "objective courtyards cleared, architecture squared, capture labels reduced, NavMesh rebuilt.");
    }

    private static void SetDesignMarkerActive(Transform root, string name, bool active)
    {
        Transform item = FindDeepChild(root, name);
        if (item != null) item.gameObject.SetActive(active);
    }

    private static void Move(Transform root, string name, Vector3 position, float yaw)
    {
        Transform item = FindDeepChild(root, name);
        if (item == null)
        {
            Debug.LogWarning($"MAP READABILITY: Could not find '{name}'.");
            return;
        }

        item.position = position;
        item.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private static void RebuildNavigation(Transform root)
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface == null || !surface.enabled) continue;
            if (surface.transform == root || surface.transform.IsChildOf(root))
            {
                surface.RemoveData();
                surface.BuildNavMesh();
            }
        }
    }
}
#endif
