#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Architecture Pass 1: migrates the recovered Original Map onto the reusable
/// Assets/Prefabs/GameplaySystems.prefab package.
///
/// The Original Map contributes only map data (sectors/objectives/bases and battlefield root).
/// Shared runtime systems, HUD, camera, input and AI infrastructure come from the prefab.
/// </summary>
public static class OriginalMapSharedGameplayInstaller
{
    private const string OriginalScenePath = "Assets/Scenes/RecoveredDevelopmentMap.unity";

    [MenuItem("RTS/Architecture/Install Shared Gameplay Prefab Into Original Map")]
    public static void InstallIntoOriginalMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedGameplaySystemsPrefabBuilder.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                "ORIGINAL MAP MIGRATION: GameplaySystems.prefab does not exist. " +
                "Create the shared gameplay prefab first.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(OriginalScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("ORIGINAL MAP MIGRATION: Could not open RecoveredDevelopmentMap.");
            return;
        }

        GameManager oldGameManager = FindLegacyGameManager(scene);
        if (oldGameManager == null)
        {
            Debug.LogError(
                "ORIGINAL MAP MIGRATION: No existing GameManager was found to provide the map bindings. " +
                "Nothing was changed.");
            return;
        }

        // Preserve only battlefield-owned references. These objects remain in the map scene after
        // the old gameplay infrastructure is removed.
        Sector[] mapSectors = oldGameManager.sectors != null
            ? (Sector[])oldGameManager.sectors.Clone()
            : new Sector[0];
        Transform battlefieldParent = oldGameManager.battlefieldParent;

        if (!ValidateMapData(mapSectors, scene))
        {
            Debug.LogError(
                "ORIGINAL MAP MIGRATION: Existing sector/objective/base references are incomplete. " +
                "The Original Map was left untouched.");
            return;
        }

        RemoveExistingSharedRoot(scene);
        RemoveLegacyInfrastructure(scene);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("ORIGINAL MAP MIGRATION: Could not instantiate GameplaySystems.prefab.");
            return;
        }

        instance.name = SharedGameplaySystemsRoot.RootObjectName;

        GameManager gameManager = instance.GetComponentInChildren<GameManager>(true);
        if (gameManager == null)
        {
            Debug.LogError("ORIGINAL MAP MIGRATION: GameplaySystems.prefab contains no GameManager.");
            Object.DestroyImmediate(instance);
            return;
        }

        gameManager.sectors = mapSectors;
        gameManager.currentSectorIndex = 0;
        gameManager.battlefieldParent = battlefieldParent;
        gameManager.factionSelectionUI = null;
        gameManager.playerFaction = Faction.None;
        gameManager.enableAutoTestMode = false;
        EditorUtility.SetDirty(gameManager);

        PositionSharedSpawnersAtFirstSector(gameManager);
        NormalizePresentation(scene, instance);

        if (!ValidateInstalledState(gameManager, scene, instance))
        {
            Debug.LogError(
                "ORIGINAL MAP MIGRATION: VALIDATION FAILED. Do not commit RecoveredDevelopmentMap.unity. " +
                "Restore/revert the scene before retrying.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = instance;

        Debug.Log(
            "ORIGINAL MAP MIGRATION: PASS. RecoveredDevelopmentMap now uses Assets/Prefabs/GameplaySystems.prefab. " +
            "Original map sectors/objectives/bases were retained; legacy shared gameplay infrastructure was removed. " +
            "Test Bootstrap > Original Map before committing the regenerated scene.");
    }

    private static GameManager FindLegacyGameManager(Scene scene)
    {
        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);
        foreach (GameManager manager in managers)
        {
            if (manager != null && manager.gameObject.scene == scene)
            {
                return manager;
            }
        }
        return null;
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

    private static void RemoveLegacyInfrastructure(Scene scene)
    {
        DestroyComponentGameObjects<GameManager>(scene);
        DestroyComponentGameObjects<UnitSpawner>(scene);
        DestroyComponentGameObjects<UIManager>(scene);
        DestroyComponentGameObjects<SelectionManager>(scene);
        DestroyComponentGameObjects<AICommander>(scene);
        DestroyComponentGameObjects<SquadManager>(scene);
        DestroyComponentGameObjects<RTSCamera>(scene);
        DestroyComponentGameObjects<TestDashboardOverlay>(scene);

        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem != null && eventSystem.gameObject.scene == scene)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        // Remove the old front-end explicitly. It is not part of the new Bootstrap flow.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            RemoveNamedDescendants(root.transform, new HashSet<string>
            {
                "Canvas_FactionSelect",
                "MainMenu_Container",
                "Canvas_MainMenu"
            });
        }
    }

    private static void DestroyComponentGameObjects<T>(Scene scene) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        foreach (T item in items)
        {
            if (item == null || item.gameObject.scene != scene) continue;
            Object.DestroyImmediate(item.gameObject);
        }
    }

    private static void RemoveNamedDescendants(Transform root, HashSet<string> names)
    {
        if (root == null) return;

        List<GameObject> remove = new List<GameObject>();
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform item in transforms)
        {
            if (item == null || item == root) continue;
            if (names.Contains(item.name)) remove.Add(item.gameObject);
        }

        foreach (GameObject item in remove)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
    }

    private static void PositionSharedSpawnersAtFirstSector(GameManager gameManager)
    {
        if (gameManager == null || gameManager.sectors == null || gameManager.sectors.Length == 0) return;

        Sector first = gameManager.sectors[0];
        if (first == null) return;

        if (gameManager.attackerSpawner != null && first.attackerBase != null)
        {
            gameManager.attackerSpawner.transform.position = first.attackerBase.transform.position;
            gameManager.attackerSpawner.transform.rotation = first.attackerBase.transform.rotation;
            EditorUtility.SetDirty(gameManager.attackerSpawner);
        }

        if (gameManager.defenderSpawner != null && first.defenderBase != null)
        {
            gameManager.defenderSpawner.transform.position = first.defenderBase.transform.position;
            gameManager.defenderSpawner.transform.rotation = first.defenderBase.transform.rotation;
            EditorUtility.SetDirty(gameManager.defenderSpawner);
        }
    }

    private static void NormalizePresentation(Scene scene, GameObject authoritativeRoot)
    {
        // Remove legacy canvases only when the shared prefab contains a canvas with the same name.
        // This keeps any genuinely map-specific world-space UI intact.
        HashSet<string> sharedCanvasNames = new HashSet<string>();
        Canvas[] sharedCanvases = authoritativeRoot.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in sharedCanvases)
        {
            if (canvas != null) sharedCanvasNames.Add(canvas.gameObject.name);
        }

        Canvas[] sceneCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas canvas in sceneCanvases)
        {
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            if (IsInside(canvas.transform, authoritativeRoot.transform)) continue;
            if (!sharedCanvasNames.Contains(canvas.gameObject.name)) continue;
            Object.DestroyImmediate(canvas.gameObject);
        }

        // The prefab is authoritative for EventSystem/camera/audio presentation.
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        bool keptEventSystem = false;
        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null || eventSystem.gameObject.scene != scene) continue;
            if (!keptEventSystem && IsInside(eventSystem.transform, authoritativeRoot.transform))
            {
                keptEventSystem = true;
                continue;
            }
            Object.DestroyImmediate(eventSystem.gameObject);
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        Camera keeper = null;
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != scene) continue;
            if (IsInside(camera.transform, authoritativeRoot.transform) && camera.GetComponent<RTSCamera>() != null)
            {
                keeper = camera;
                break;
            }
        }

        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != scene) continue;
            if (camera == keeper)
            {
                camera.enabled = true;
                continue;
            }
            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null) Object.DestroyImmediate(listener);
        }

        if (keeper != null && keeper.GetComponent<AudioListener>() == null)
        {
            keeper.gameObject.AddComponent<AudioListener>();
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        AudioListener keeperListener = keeper != null ? keeper.GetComponent<AudioListener>() : null;
        foreach (AudioListener listener in listeners)
        {
            if (listener == null || listener.gameObject.scene != scene) continue;
            if (listener == keeperListener) continue;
            Object.DestroyImmediate(listener);
        }
    }

    private static bool ValidateMapData(Sector[] sectors, Scene scene)
    {
        if (sectors == null || sectors.Length == 0) return false;

        foreach (Sector sector in sectors)
        {
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

    private static bool ValidateInstalledState(GameManager gameManager, Scene scene, GameObject authoritativeRoot)
    {
        if (gameManager == null || gameManager.gameObject.scene != scene) return false;
        if (!IsInside(gameManager.transform, authoritativeRoot.transform)) return false;
        if (!ValidateMapData(gameManager.sectors, scene)) return false;
        if (gameManager.attackerSpawner == null || gameManager.defenderSpawner == null) return false;

        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);
        int managerCount = 0;
        foreach (GameManager manager in managers)
        {
            if (manager != null && manager.gameObject.scene == scene) managerCount++;
        }
        if (managerCount != 1) return false;

        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        int eventSystemCount = 0;
        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem != null && eventSystem.gameObject.scene == scene) eventSystemCount++;
        }
        if (eventSystemCount != 1) return false;

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        int enabledListenerCount = 0;
        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener.gameObject.scene == scene && listener.enabled) enabledListenerCount++;
        }
        if (enabledListenerCount != 1) return false;

        return true;
    }

    private static bool IsInside(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;
        Transform current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }
}
#endif
