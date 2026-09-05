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
            "rebound to ChatGPT Map. Original Map was not saved or modified. Test Bootstrap > ChatGPT Map before committing the regenerated scene.");
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

        AddAllOfType<UIManager>(candidates);
        AddAllOfType<SelectionManager>(candidates);
        AddAllOfType<AICommander>(candidates);
        AddAllOfType<SquadManager>(candidates);
        AddAllOfType<RTSCamera>(candidates);
        AddAllOfType<EventSystem>(candidates);
        AddAllOfType<TestDashboardOverlay>(candidates);

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == sourceScene)
        {
            AddCandidate(candidates, mainCamera.gameObject);
        }

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
        return exportRoot;
    }

    private static void AddCandidate(HashSet<GameObject> set, GameObject candidate)
    {
        if (candidate != null) set.Add(candidate);
    }

    private static void AddAllOfType<T>(HashSet<GameObject> set) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T item in items)
        {
            if (item != null) set.Add(item.gameObject);
        }
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
