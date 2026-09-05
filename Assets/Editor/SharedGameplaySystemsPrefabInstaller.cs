#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Architecture Pass 1 proof: installs the reusable GameplaySystems prefab into ChatGPT Map
/// without opening or borrowing anything from Original Map.
/// </summary>
public static class SharedGameplaySystemsPrefabInstaller
{
    private const string ChatGPTScenePath = "Assets/Scenes/GreyboxBattlefield01.unity";

    [MenuItem("RTS/Architecture/Install Shared Gameplay Prefab Into ChatGPT Map")]
    public static void InstallIntoChatGPTMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedGameplaySystemsPrefabBuilder.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                "SHARED SYSTEMS: GameplaySystems.prefab does not exist yet. " +
                "Open the validated ChatGPT Map and run RTS > Architecture > Create Shared Gameplay Systems Prefab first.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ChatGPTScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("SHARED SYSTEMS: Could not open GreyboxBattlefield01.");
            return;
        }

        RemoveExistingSharedRoot(scene);
        RemoveLegacyGameplayCore(scene);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("SHARED SYSTEMS: Could not instantiate GameplaySystems.prefab.");
            return;
        }

        instance.name = SharedGameplaySystemsRoot.RootObjectName;

        GameManager gameManager = instance.GetComponentInChildren<GameManager>(true);
        if (gameManager == null)
        {
            Debug.LogError("SHARED SYSTEMS: GameplaySystems.prefab contains no GameManager.");
            Object.DestroyImmediate(instance);
            return;
        }

        gameManager.sectors = new Sector[0];
        gameManager.currentSectorIndex = 0;
        gameManager.battlefieldParent = null;
        gameManager.factionSelectionUI = null;
        gameManager.playerFaction = Faction.None;
        gameManager.enableAutoTestMode = false;
        EditorUtility.SetDirty(gameManager);

        EditorSceneManager.SetActiveScene(scene);

        // Existing tested map binder provides the map-specific half of the contract.
        GreyboxBattlefieldGameplayWiring.WireForPlay();
        GreyboxSpawnerReferenceRepair.Repair();
        GreyboxBattlefieldReadabilityPass.Apply();

        if (!ValidateBindings(gameManager, scene))
        {
            Debug.LogError(
                "SHARED SYSTEMS: PREFAB INSTALL VALIDATION FAILED. Do not commit GreyboxBattlefield01.unity.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = instance;

        Debug.Log(
            "SHARED SYSTEMS: PREFAB INSTALL PASS. ChatGPT Map is now using Assets/Prefabs/GameplaySystems.prefab " +
            "and was wired without opening Original Map. Test Bootstrap > ChatGPT Map before committing.");
    }

    private static void RemoveExistingSharedRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            if (root.name == SharedGameplaySystemsRoot.RootObjectName ||
                root.GetComponent<SharedGameplaySystemsRoot>() != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void RemoveLegacyGameplayCore(Scene scene)
    {
        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameManager manager in managers)
        {
            if (manager != null && manager.gameObject.scene == scene)
            {
                Object.DestroyImmediate(manager);
            }
        }

        UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitSpawner spawner in spawners)
        {
            if (spawner != null && spawner.gameObject.scene == scene)
            {
                Object.DestroyImmediate(spawner.gameObject);
            }
        }
    }

    private static bool ValidateBindings(GameManager gameManager, Scene scene)
    {
        if (gameManager == null || gameManager.gameObject.scene != scene) return false;
        if (gameManager.attackerSpawner == null || gameManager.defenderSpawner == null) return false;
        if (gameManager.attackerSpawner.gameObject.scene != scene || gameManager.defenderSpawner.gameObject.scene != scene) return false;
        if (gameManager.sectors == null || gameManager.sectors.Length == 0) return false;

        for (int i = 0; i < gameManager.sectors.Length; i++)
        {
            Sector sector = gameManager.sectors[i];
            if (sector == null || sector.capturePoints == null || sector.capturePoints.Length == 0) return false;
            if (sector.attackerBase == null || sector.defenderBase == null) return false;
            if (sector.attackerBase.gameObject.scene != scene || sector.defenderBase.gameObject.scene != scene) return false;

            foreach (CapturePoint point in sector.capturePoints)
            {
                if (point == null || point.gameObject.scene != scene) return false;
            }
        }

        return true;
    }
}
#endif
