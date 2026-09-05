#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Maintenance utility for deliberately rebuilding the authoritative shared gameplay prefab
/// from an already-valid SHARED GAMEPLAY SYSTEMS root. It does not use Original Map as a donor.
/// Map-specific sector/base references are stripped from the temporary copy before saving.
/// </summary>
public static class SharedGameplaySystemsPrefabBuilder
{
    // Kept as a compatibility alias while Architecture Pass 1 removes older editor helpers.
    public const string PrefabPath = SharedGameplaySystemsPaths.PrefabPath;
    private const string ChatGPTScenePath = "Assets/Scenes/GreyboxBattlefield01.unity";

    [MenuItem("RTS/Architecture/Rebuild Shared Gameplay Systems Prefab From Validated Map")]
    public static void CreatePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("SHARED SYSTEMS: Exit Play mode before rebuilding the gameplay prefab.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene sourceScene = ResolveSourceScene();
        if (!sourceScene.IsValid())
        {
            return;
        }

        GameObject sourceRoot = FindSharedRoot(sourceScene);
        if (sourceRoot == null)
        {
            Debug.LogError(
                "SHARED SYSTEMS: No validated 'SHARED GAMEPLAY SYSTEMS' root was found. " +
                "The authoritative GameplaySystems.prefab was not changed.");
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
            Debug.LogError("SHARED SYSTEMS: Failed to rebuild GameplaySystems.prefab.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;

        Debug.Log(
            "SHARED SYSTEMS: Rebuilt Assets/Prefabs/GameplaySystems.prefab from an already-valid shared gameplay root. " +
            "Map-specific GameManager sector/base state was cleared before saving.");
    }

    private static Scene ResolveSourceScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject activeRoot = activeScene.IsValid() ? FindSharedRoot(activeScene) : null;

        if (activeRoot != null)
        {
            return activeScene;
        }

        Scene chatGPTScene = EditorSceneManager.OpenScene(ChatGPTScenePath, OpenSceneMode.Single);
        if (!chatGPTScene.IsValid())
        {
            Debug.LogError($"SHARED SYSTEMS: Could not open '{ChatGPTScenePath}'.");
            return default;
        }

        Debug.Log("SHARED SYSTEMS: Opened ChatGPT Map automatically as the validated maintenance source.");
        return chatGPTScene;
    }

    private static GameObject FindSharedRoot(Scene scene)
    {
        if (!scene.IsValid()) return null;

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

        // Spawner positions are map-owned. The map binder places these at the active sector bases.
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
