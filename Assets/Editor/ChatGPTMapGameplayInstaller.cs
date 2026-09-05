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

        GameManager sourceGameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
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

        // These belong to the battlefield, not to shared gameplay infrastructure.
        copiedGameManager.sectors = new Sector[0];
        copiedGameManager.currentSectorIndex = 0;
        copiedGameManager.battlefieldParent = null;
        copiedGameManager.factionSelectionUI = null;
        copiedGameManager.playerFaction = Faction.None;
        copiedGameManager.enableAutoTestMode = false;

        EditorUtility.SetDirty(copiedGameManager);

        // The donor root is now authoritative for runtime presentation/input. Remove only
        // duplicate target-scene infrastructure; map geometry and map-owned world objects remain.
        DeduplicatePresentationInfrastructure(chatGPT, exportRoot);

        // Reuse the already-tested greybox binder to provide map-specific objectives, bases,
        // sectors, spawner positions and navigation.
        GreyboxBattlefieldGameplayWiring.WireForPlay();
        GreyboxSpawnerReferenceRepair.Repair();
        GreyboxBattlefieldReadabilityPass.Apply();

        EditorSceneManager.MarkSceneDirty(chatGPT);
        EditorSceneManager.SaveScene(chatGPT);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // The Original Map was opened only as a donor and is deliberately closed without save.
        EditorSceneManager.CloseScene(original, true);
        EditorSceneManager.SetActiveScene(chatGPT);

        Selection.activeGameObject = exportRoot;

        Debug.Log(
            "CHATGPT MAP INSTALL: Gameplay systems copied from the known-good Original Map and " +
            "rebound to ChatGPT Map. Complete gameplay canvases copied; obsolete BATTLEBLOCKS UI excluded; " +
            "duplicate EventSystems/cameras/audio listeners removed. Original Map was not saved or modified. " +
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

        // Copy every gameplay canvas family from the known-good donor rather than only the one
        // referenced directly by GameManager. This restores sidebars/store/control presentation
        // that lives in sibling canvases, while filtering the obsolete recovered front end.
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
            if (candidate != null && IsObsoleteFrontEnd(candidate.transform))
            {
                remove.Add(candidate);
            }
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

    private static void DeduplicatePresentationInfrastructure(Scene targetScene, GameObject authoritativeRoot)
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null || eventSystem.gameObject.scene != targetScene) continue;
            if (eventSystem.transform == authoritativeRoot.transform || eventSystem.transform.IsChildOf(authoritativeRoot.transform)) continue;

            Object.DestroyImmediate(eventSystem.gameObject);
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            if (listener == null || listener.gameObject.scene != targetScene) continue;
            if (listener.transform == authoritativeRoot.transform || listener.transform.IsChildOf(authoritativeRoot.transform)) continue;

            Object.DestroyImmediate(listener);
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != targetScene) continue;
            if (camera.transform == authoritativeRoot.transform || camera.transform.IsChildOf(authoritativeRoot.transform)) continue;

            camera.enabled = false;
            EditorUtility.SetDirty(camera);
        }
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
