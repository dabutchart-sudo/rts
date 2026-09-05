#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the known-good gameplay systems from the recovered Original Map into the ChatGPT Map
/// without modifying or saving the Original Map. The copied GameManager is then rebound to the
/// ChatGPT Map's own sectors/objectives/bases by the existing greybox wiring pass.
///
/// Recovery rule: the donor scene owns shared presentation/runtime infrastructure. ChatGPT Map
/// owns battlefield content. This installer therefore copies the complete gameplay UI family
/// while explicitly excluding the obsolete BATTLEBLOCKS front end, then removes duplicate
/// cameras/listeners/EventSystems from the target scene.
/// </summary>
public static class ChatGPTMapGameplayInstaller
{
    private const string OriginalScenePath = "Assets/Scenes/RecoveredDevelopmentMap.unity";
    private const string ChatGPTScenePath = "Assets/Scenes/GreyboxBattlefield01.unity";
    private const string SharedRootName = "SHARED GAMEPLAY SYSTEMS";

    [MenuItem("RTS/Maps/Install Gameplay Systems Into ChatGPT Map")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene original = EditorSceneManager.OpenScene(OriginalScenePath, OpenSceneMode.Single);
        if (!original.IsValid())
        {
            Debug.LogError("CHATGPT MAP INSTALL: Could not open RecoveredDevelopmentMap.");
            return;
        }

        GameManager sourceGameManager = FindComponentInScene<GameManager>(original);
        if (sourceGameManager == null)
        {
            Debug.LogError("CHATGPT MAP INSTALL: Original Map has no GameManager. Nothing was changed.");
            return;
        }

        GameObject exportRoot = BuildCopiedSystemsRoot(sourceGameManager, original);
        if (exportRoot == null)
        {
            Debug.LogError("CHATGPT MAP INSTALL: Could not prepare shared gameplay systems. Nothing was changed.");
            return;
        }

        Scene chatGPT = EditorSceneManager.OpenScene(ChatGPTScenePath, OpenSceneMode.Additive);
        if (!chatGPT.IsValid())
        {
            Object.DestroyImmediate(exportRoot);
            Debug.LogError("CHATGPT MAP INSTALL: Could not open GreyboxBattlefield01. Original Map was not saved or modified.");
            return;
        }

        RemoveExistingSharedRoot(chatGPT);
        SceneManager.MoveGameObjectToScene(exportRoot, chatGPT);
        exportRoot.name = SharedRootName;
        EditorSceneManager.SetActiveScene(chatGPT);

        GameManager copiedGameManager = exportRoot.GetComponentInChildren<GameManager>(true);
        if (copiedGameManager == null)
        {
            Debug.LogError("CHATGPT MAP INSTALL: Copied systems contain no GameManager. Aborting before save.");
            EditorSceneManager.CloseScene(chatGPT, true);
            return;
        }

        copiedGameManager.sectors = new Sector[0];
        copiedGameManager.currentSectorIndex = 0;
        copiedGameManager.battlefieldParent = null;
        copiedGameManager.factionSelectionUI = null;
        copiedGameManager.playerFaction = Faction.None;
        copiedGameManager.enableAutoTestMode = false;
        EditorUtility.SetDirty(copiedGameManager);

        // IMPORTANT: close the donor BEFORE any greybox wiring. Several legacy editor helpers use
        // global object searches. Leaving both scenes open lets those helpers bind the donor
        // GameManager or donor CapturePoints by accident, producing references that become null
        // as soon as the donor scene closes.
        EditorSceneManager.CloseScene(original, true);
        EditorSceneManager.SetActiveScene(chatGPT);

        // The copied gameplay root is authoritative. Remove old target-scene core components so
        // the legacy wiring/repair helpers cannot accidentally select stale GameManagers/spawners.
        RemoveDuplicateGameplayCore(chatGPT, exportRoot);
        NormalizePresentationInfrastructure(chatGPT, exportRoot);

        GreyboxBattlefieldGameplayWiring.WireForPlay();
        GreyboxSpawnerReferenceRepair.Repair();

        if (!ValidateMapBindings(copiedGameManager))
        {
            Debug.LogError(
                "CHATGPT MAP INSTALL: Map binding validation FAILED. At least one sector/objective/base reference is missing. " +
                "Do not commit GreyboxBattlefield01.unity. Re-run the installer after pulling the latest code.");
            return;
        }

        GreyboxBattlefieldReadabilityPass.Apply();
        NormalizePresentationInfrastructure(chatGPT, exportRoot);

        if (!ValidateMapBindings(copiedGameManager))
        {
            Debug.LogError(
                "CHATGPT MAP INSTALL: Post-readability validation FAILED. Do not commit GreyboxBattlefield01.unity.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(chatGPT);
        EditorSceneManager.SaveScene(chatGPT);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = exportRoot;

        int canvasCount = exportRoot.GetComponentsInChildren<Canvas>(true).Length;
        int eventSystemCount = CountComponentsInScene<EventSystem>(chatGPT, true);
        int listenerCount = CountEnabledComponentsInScene<AudioListener>(chatGPT);

        Debug.Log(
            "CHATGPT MAP INSTALL: Gameplay systems copied from the known-good Original Map and rebound to ChatGPT Map. " +
            "Donor was closed before map wiring; sector/objective references validated; obsolete BATTLEBLOCKS UI excluded.\n" +
            $"CHATGPT MAP PRESENTATION: donor canvases={canvasCount}, EventSystems={eventSystemCount}, AudioListeners={listenerCount}. " +
            "Test Bootstrap > ChatGPT Map before committing the regenerated scene.");
    }

    private static GameObject BuildCopiedSystemsRoot(GameManager gameManager, Scene sourceScene)
    {
        GameObject exportRoot = new GameObject(SharedRootName);
        HashSet<GameObject> candidates = new HashSet<GameObject>();

        AddCandidate(candidates, gameManager.gameObject);
        AddCandidate(candidates, gameManager.attackerSpawner != null ? gameManager.attackerSpawner.gameObject : null);
        AddCandidate(candidates, gameManager.defenderSpawner != null ? gameManager.defenderSpawner.gameObject : null);
        AddCandidate(candidates, gameManager.playerGameplayUI);
        AddCandidate(candidates, gameManager.enemyDirector != null ? gameManager.enemyDirector.gameObject : null);

        AddAllOfType<UIManager>(candidates, sourceScene);
        AddAllOfType<SelectionManager>(candidates, sourceScene);
        AddAllOfType<AICommander>(candidates, sourceScene);
        AddAllOfType<SquadManager>(candidates, sourceScene);
        AddAllOfType<RTSCamera>(candidates, sourceScene);
        AddAllOfType<EventSystem>(candidates, sourceScene);
        AddAllOfType<TestDashboardOverlay>(candidates, sourceScene);

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas.gameObject.scene != sourceScene) continue;
            if (IsObsoleteFrontEnd(canvas.transform)) continue;
            AddCandidate(candidates, GetTopmostSceneObject(canvas.gameObject, sourceScene));
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == sourceScene)
        {
            AddCandidate(candidates, GetTopmostSceneObject(mainCamera.gameObject, sourceScene));
        }

        RemoveObsoleteFrontEndCandidates(candidates);
        RemoveNestedCandidates(candidates);

        Dictionary<Object, Object> remap = new Dictionary<Object, Object>();

        foreach (GameObject source in candidates)
        {
            if (source == null || source.scene != sourceScene) continue;

            GameObject clone = Object.Instantiate(source, exportRoot.transform);
            clone.name = source.name;
            BuildObjectMap(source, clone, remap);
        }

        RemapCopiedReferences(exportRoot, remap);
        RemoveObsoleteFrontEndFromCopy(exportRoot);
        return exportRoot;
    }

    private static void AddCandidate(HashSet<GameObject> set, GameObject candidate)
    {
        if (candidate != null) set.Add(candidate);
    }

    private static void AddAllOfType<T>(HashSet<GameObject> set, Scene sourceScene) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T item in items)
        {
            if (item == null || item.gameObject.scene != sourceScene) continue;
            if (IsObsoleteFrontEnd(item.transform)) continue;
            set.Add(GetTopmostSceneObject(item.gameObject, sourceScene));
        }
    }

    private static GameObject GetTopmostSceneObject(GameObject gameObject, Scene scene)
    {
        if (gameObject == null) return null;

        Transform current = gameObject.transform;
        while (current.parent != null && current.parent.gameObject.scene == scene)
        {
            current = current.parent;
        }

        return current.gameObject;
    }

    private static bool IsObsoleteFrontEnd(Transform item)
    {
        Transform current = item;
        while (current != null)
        {
            if (current.name == "Canvas_FactionSelect" ||
                current.name == "MainMenu_Container" ||
                current.name == "Canvas_MainMenu")
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private static void RemoveObsoleteFrontEndCandidates(HashSet<GameObject> candidates)
    {
        List<GameObject> remove = new List<GameObject>();
        foreach (GameObject candidate in candidates)
        {
            if (candidate != null && IsObsoleteFrontEnd(candidate.transform)) remove.Add(candidate);
        }
        foreach (GameObject item in remove) candidates.Remove(item);
    }

    private static void RemoveNestedCandidates(HashSet<GameObject> set)
    {
        List<GameObject> remove = new List<GameObject>();
        foreach (GameObject candidate in set)
        {
            if (candidate == null) continue;

            Transform parent = candidate.transform.parent;
            while (parent != null)
            {
                if (set.Contains(parent.gameObject))
                {
                    remove.Add(candidate);
                    break;
                }
                parent = parent.parent;
            }
        }
        foreach (GameObject item in remove) set.Remove(item);
    }

    private static void BuildObjectMap(GameObject source, GameObject clone, Dictionary<Object, Object> map)
    {
        map[source] = clone;
        map[source.transform] = clone.transform;

        Component[] sourceComponents = source.GetComponents<Component>();
        Component[] cloneComponents = clone.GetComponents<Component>();
        int componentCount = Mathf.Min(sourceComponents.Length, cloneComponents.Length);
        for (int i = 0; i < componentCount; i++)
        {
            if (sourceComponents[i] != null && cloneComponents[i] != null)
            {
                map[sourceComponents[i]] = cloneComponents[i];
            }
        }

        int childCount = Mathf.Min(source.transform.childCount, clone.transform.childCount);
        for (int i = 0; i < childCount; i++)
        {
            BuildObjectMap(source.transform.GetChild(i).gameObject, clone.transform.GetChild(i).gameObject, map);
        }
    }

    private static void RemapCopiedReferences(GameObject exportRoot, Dictionary<Object, Object> map)
    {
        Component[] components = exportRoot.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null) continue;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                Object referenced = property.objectReferenceValue;
                if (referenced != null && map.TryGetValue(referenced, out Object replacement))
                {
                    property.objectReferenceValue = replacement;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void RemoveObsoleteFrontEndFromCopy(GameObject exportRoot)
    {
        Transform[] transforms = exportRoot.GetComponentsInChildren<Transform>(true);
        List<GameObject> remove = new List<GameObject>();

        foreach (Transform item in transforms)
        {
            if (item == null || item == exportRoot.transform) continue;
            if (item.name == "Canvas_FactionSelect" ||
                item.name == "MainMenu_Container" ||
                item.name == "Canvas_MainMenu")
            {
                remove.Add(item.gameObject);
            }
        }

        foreach (GameObject item in remove)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
    }

    private static void RemoveDuplicateGameplayCore(Scene targetScene, GameObject authoritativeRoot)
    {
        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameManager manager in managers)
        {
            if (manager == null || manager.gameObject.scene != targetScene) continue;
            if (IsInside(manager.transform, authoritativeRoot.transform)) continue;
            Object.DestroyImmediate(manager);
        }

        UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitSpawner spawner in spawners)
        {
            if (spawner == null || spawner.gameObject.scene != targetScene) continue;
            if (IsInside(spawner.transform, authoritativeRoot.transform)) continue;
            Object.DestroyImmediate(spawner.gameObject);
        }
    }

    private static void NormalizePresentationInfrastructure(Scene targetScene, GameObject authoritativeRoot)
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem keeperEventSystem = null;
        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null || eventSystem.gameObject.scene != targetScene) continue;
            if (keeperEventSystem == null && IsInside(eventSystem.transform, authoritativeRoot.transform))
            {
                keeperEventSystem = eventSystem;
                continue;
            }
            Object.DestroyImmediate(eventSystem.gameObject);
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera keeperCamera = null;
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != targetScene) continue;

            bool inside = IsInside(camera.transform, authoritativeRoot.transform);
            bool isPreferred = inside && camera.GetComponent<RTSCamera>() != null;

            if (keeperCamera == null && isPreferred)
            {
                keeperCamera = camera;
                camera.enabled = true;
                continue;
            }
        }

        if (keeperCamera == null)
        {
            foreach (Camera camera in cameras)
            {
                if (camera == null || camera.gameObject.scene != targetScene) continue;
                if (!IsInside(camera.transform, authoritativeRoot.transform)) continue;
                keeperCamera = camera;
                camera.enabled = true;
                break;
            }
        }

        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != targetScene) continue;
            if (camera == keeperCamera) continue;
            camera.enabled = false;
            AudioListener extraListener = camera.GetComponent<AudioListener>();
            if (extraListener != null) Object.DestroyImmediate(extraListener);
            EditorUtility.SetDirty(camera);
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AudioListener keeperListener = keeperCamera != null ? keeperCamera.GetComponent<AudioListener>() : null;
        if (keeperCamera != null && keeperListener == null)
        {
            keeperListener = keeperCamera.gameObject.AddComponent<AudioListener>();
        }

        foreach (AudioListener listener in listeners)
        {
            if (listener == null || listener.gameObject.scene != targetScene) continue;
            if (listener == keeperListener) continue;
            Object.DestroyImmediate(listener);
        }

        // If the target still has a canvas with the same name as a donor canvas, remove the old
        // target copy. Otherwise runtime HUD helpers can bind the wrong Canvas_Gameplay.
        HashSet<string> authoritativeCanvasNames = new HashSet<string>();
        foreach (Canvas canvas in authoritativeRoot.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas != null) authoritativeCanvasNames.Add(canvas.gameObject.name);
        }

        Canvas[] targetCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in targetCanvases)
        {
            if (canvas == null || canvas.gameObject.scene != targetScene) continue;
            if (IsInside(canvas.transform, authoritativeRoot.transform)) continue;
            if (!authoritativeCanvasNames.Contains(canvas.gameObject.name)) continue;
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static bool ValidateMapBindings(GameManager gameManager)
    {
        if (gameManager == null || gameManager.sectors == null || gameManager.sectors.Length == 0)
        {
            Debug.LogError("CHATGPT MAP VALIDATION: GameManager has no sectors.");
            return false;
        }

        bool valid = true;
        for (int sectorIndex = 0; sectorIndex < gameManager.sectors.Length; sectorIndex++)
        {
            Sector sector = gameManager.sectors[sectorIndex];
            if (sector == null)
            {
                Debug.LogError($"CHATGPT MAP VALIDATION: Sector {sectorIndex} is null.");
                valid = false;
                continue;
            }

            if (sector.attackerBase == null || sector.defenderBase == null)
            {
                Debug.LogError($"CHATGPT MAP VALIDATION: '{sector.sectorName}' is missing an attacker or defender base.");
                valid = false;
            }

            if (sector.capturePoints == null || sector.capturePoints.Length == 0)
            {
                Debug.LogError($"CHATGPT MAP VALIDATION: '{sector.sectorName}' has no capture points.");
                valid = false;
                continue;
            }

            for (int pointIndex = 0; pointIndex < sector.capturePoints.Length; pointIndex++)
            {
                CapturePoint point = sector.capturePoints[pointIndex];
                if (point == null)
                {
                    Debug.LogError($"CHATGPT MAP VALIDATION: '{sector.sectorName}' CapturePoint[{pointIndex}] is null.");
                    valid = false;
                    continue;
                }

                if (point.gameObject.scene != gameManager.gameObject.scene)
                {
                    Debug.LogError(
                        $"CHATGPT MAP VALIDATION: '{sector.sectorName}' CapturePoint[{pointIndex}] belongs to scene " +
                        $"'{point.gameObject.scene.name}', not '{gameManager.gameObject.scene.name}'.");
                    valid = false;
                }
            }
        }

        if (gameManager.attackerSpawner == null || gameManager.defenderSpawner == null)
        {
            Debug.LogError("CHATGPT MAP VALIDATION: one or both UnitSpawner references are missing.");
            valid = false;
        }

        if (valid)
        {
            Debug.Log($"CHATGPT MAP VALIDATION: PASS - {gameManager.sectors.Length} sectors fully bound to '{gameManager.gameObject.scene.name}'.");
        }

        return valid;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T item in items)
        {
            if (item != null && item.gameObject.scene == scene) return item;
        }
        return null;
    }

    private static int CountComponentsInScene<T>(Scene scene, bool includeInactive) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int count = 0;
        foreach (T item in items)
        {
            if (item != null && item.gameObject.scene == scene) count++;
        }
        return count;
    }

    private static int CountEnabledComponentsInScene<T>(Scene scene) where T : Behaviour
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (T item in items)
        {
            if (item != null && item.gameObject.scene == scene && item.enabled) count++;
        }
        return count;
    }

    private static bool IsInside(Transform item, Transform root)
    {
        return item == root || item.IsChildOf(root);
    }

    private static void RemoveExistingSharedRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == SharedRootName)
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
