#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Architecture Pass 1: turns the validated SHARED GAMEPLAY SYSTEMS root into a reusable prefab.
/// The source scene is not rewritten; a temporary clone is sanitised so no battlefield-specific
/// sector/base references are baked into the prefab.
/// </summary>
public static class SharedGameplaySystemsPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/GameplaySystems.prefab";

    [MenuItem("RTS/Architecture/Create Shared Gameplay Systems Prefab")]
    public static void CreatePrefab()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("SHARED SYSTEMS: No valid active scene.");
            return;
        }

        GameObject sourceRoot = FindSharedRoot(activeScene);
        if (sourceRoot == null)
        {
            Debug.LogError(
                "SHARED SYSTEMS: No 'SHARED GAMEPLAY SYSTEMS' root was found in the active scene. " +
                "Use the validated ChatGPT Map scene as the source for this first extraction.");
            return;
        }

        GameObject temp = Object.Instantiate(sourceRoot);
        temp.name = SharedGameplaySystemsRoot.RootObjectName;

        SharedGameplaySystemsRoot marker = temp.GetComponent<SharedGameplaySystemsRoot>();
        if (marker == null) marker = temp.AddComponent<SharedGameplaySystemsRoot>();

        SanitizeMapSpecificState(temp);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath, out bool success);
        Object.DestroyImmediate(temp);

        if (!success || saved == null)
        {
            Debug.LogError("SHARED SYSTEMS: Failed to create GameplaySystems.prefab.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;

        Debug.Log(
            "SHARED SYSTEMS: Created Assets/Prefabs/GameplaySystems.prefab. " +
            "Map-specific GameManager sector/base state was cleared. " +
            "This prefab is now the candidate master gameplay package for all battlefields.");
    }

    private static GameObject FindSharedRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            if (root.name == SharedGameplaySystemsRoot.RootObjectName) return root;
            if (root.GetComponent<SharedGameplaySystemsRoot>() != null) return root;
        }

        return null;
    }

    private static void SanitizeMapSpecificState(GameObject root)
    {
        GameManager gameManager = root.GetComponentInChildren<GameManager>(true);
        if (gameManager != null)
        {
            gameManager.sectors = new Sector[0];
            gameManager.currentSectorIndex = 0;
            gameManager.battlefieldParent = null;
            gameManager.factionSelectionUI = null;
            gameManager.playerFaction = Faction.None;
            gameManager.enableAutoTestMode = false;
            EditorUtility.SetDirty(gameManager);
        }

        // Spawner positions are deliberately not treated as authored prefab data. The map binder
        // moves them to the active sector's bases after installation.
        UnitSpawner[] spawners = root.GetComponentsInChildren<UnitSpawner>(true);
        foreach (UnitSpawner spawner in spawners)
        {
            if (spawner == null) continue;
            spawner.transform.localPosition = Vector3.zero;
            spawner.transform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(spawner);
        }
    }
}
#endif
