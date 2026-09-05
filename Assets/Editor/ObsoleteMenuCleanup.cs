#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Permanently removes the recovered prototype menu from battlefield scenes.
/// The Bootstrap scene is now the only battlefield-selection/start menu.
/// </summary>
public static class ObsoleteMenuCleanup
{
    private static readonly string[] ObsoleteObjectNames =
    {
        "Canvas_FactionSelect",
        "MainMenu_Container",
        "Canvas_MainMenu"
    };

    [MenuItem("RTS/Maps/Remove Old BATTLEBLOCKS Menu From Battlefield Scenes")]
    public static void RemoveFromBothBattlefields()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int removedOriginal = CleanScene("Assets/Scenes/RecoveredDevelopmentMap.unity");
        int removedChatGPT = CleanScene("Assets/Scenes/GreyboxBattlefield01.unity");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"OLD MENU CLEANUP: Complete. Original Map removed {removedOriginal} obsolete menu object(s); " +
            $"ChatGPT Map removed {removedChatGPT}. The Bootstrap scene is now the only start menu.");
    }

    public static int RemoveFromActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return 0;

        int removed = RemoveNamedObjects(scene);
        ClearGameManagerMenuReference(scene);

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return removed;
    }

    private static int CleanScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"OLD MENU CLEANUP: Could not open '{scenePath}'.");
            return 0;
        }

        int removed = RemoveNamedObjects(scene);
        ClearGameManagerMenuReference(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return removed;
    }

    private static int RemoveNamedObjects(Scene scene)
    {
        List<GameObject> toRemove = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectMatches(root.transform, toRemove);
        }

        // Remove deepest children first so a parent removal does not invalidate traversal state.
        toRemove.Sort((a, b) => GetDepth(b.transform).CompareTo(GetDepth(a.transform)));

        int removed = 0;
        foreach (GameObject candidate in toRemove)
        {
            if (candidate == null) continue;
            Object.DestroyImmediate(candidate);
            removed++;
        }

        return removed;
    }

    private static void CollectMatches(Transform node, List<GameObject> matches)
    {
        if (node == null) return;

        if (IsObsoleteName(node.name))
        {
            matches.Add(node.gameObject);
            return;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            CollectMatches(node.GetChild(i), matches);
        }
    }

    private static bool IsObsoleteName(string objectName)
    {
        foreach (string obsoleteName in ObsoleteObjectNames)
        {
            if (objectName == obsoleteName) return true;
        }
        return false;
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        while (transform != null)
        {
            depth++;
            transform = transform.parent;
        }
        return depth;
    }

    private static void ClearGameManagerMenuReference(Scene scene)
    {
        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameManager manager in managers)
        {
            if (manager == null || manager.gameObject.scene != scene) continue;
            manager.factionSelectionUI = null;
            EditorUtility.SetDirty(manager);
        }
    }
}
#endif
