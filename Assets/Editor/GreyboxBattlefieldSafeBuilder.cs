#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reproducible build pipeline for Greybox Battlefield 01.
/// The finished map scene is generated from the clean SampleScene baseline, then gameplay
/// wiring, readability cleanup and serialized-reference repair are applied before saving.
/// This keeps the authored map reproducible while still allowing the generated .unity scene
/// itself to be committed to source control as a normal project asset.
/// </summary>
public static class GreyboxBattlefieldSafeBuilder
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/GreyboxBattlefield01.unity";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";

    [MenuItem("RTS/Maps/Rebuild Greybox Battlefield 01 (Complete)")]
    public static void RebuildComplete()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        if (!source.IsValid())
        {
            Debug.LogError("COMPLETE MAP BUILD: Could not open SampleScene.");
            return;
        }

        RemoveAccidentalGeneratedMapFromSource(source);
        EditorSceneManager.SaveScene(source);

        // saveAsCopy=false is deliberate: the active scene becomes GreyboxBattlefield01.
        if (!EditorSceneManager.SaveScene(source, TargetScene, false))
        {
            Debug.LogError("COMPLETE MAP BUILD: Could not create GreyboxBattlefield01 scene.");
            return;
        }

        Scene target = SceneManager.GetActiveScene();
        if (!target.IsValid() || target.path != TargetScene)
        {
            Debug.LogError($"COMPLETE MAP BUILD: Expected active scene '{TargetScene}', but got '{target.path}'.");
            return;
        }

        GameObject oldGeometry = GameObject.Find(GeneratedRootName);
        if (oldGeometry != null) UnityEngine.Object.DestroyImmediate(oldGeometry);

        GameObject root = new GameObject(GeneratedRootName);

        try
        {
            InvokeBuilder("CreateMapDefinition", root);
            InvokeBuilder("CreateTerrain", root.transform);
            InvokeBuilder("CreateRoadNetwork", root.transform);
            InvokeBuilder("CreateSectorOne", root.transform);
            InvokeBuilder("CreateSectorTwo", root.transform);
            InvokeBuilder("CreateSectorThree", root.transform);
            InvokeBuilder("CreateRouteMarkers", root.transform);
            InvokeBuilder("EnsureSceneInBuildSettings", TargetScene);

            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);

            // These are intentionally called through their public editor entry points so the
            // same operations can still be run independently while debugging.
            GreyboxBattlefieldGameplayWiring.WireForPlay();
            GreyboxBattlefieldReadabilityPass.Apply();
            GreyboxSpawnerReferenceRepair.Repair();

            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogError($"COMPLETE MAP BUILD: Failed while creating the playable map. {ex.Message}\n{ex.StackTrace}");
            return;
        }

        Selection.activeGameObject = GameObject.Find(GeneratedRootName);

        Debug.Log(
            "COMPLETE MAP BUILD: Greybox Battlefield 01 is fully rebuilt and saved. " +
            "Geometry, 3 sectors, 6 objectives, bases, spawn references, readability pass and NavMesh are applied. " +
            "IMPORTANT: commit Assets/Scenes/GreyboxBattlefield01.unity and its .meta file in GitHub Desktop so this tested map exists in source control.");
    }

    // Kept as a compatibility menu item because earlier instructions referred to it.
    [MenuItem("RTS/Maps/Build Greybox Battlefield 01 (Safe)")]
    public static void BuildSafe()
    {
        RebuildComplete();
    }

    [MenuItem("RTS/Maps/Clean Greybox Artifacts From SampleScene")]
    public static void CleanSampleScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        if (!source.IsValid())
        {
            Debug.LogError("MAP CLEANUP: Could not open SampleScene.");
            return;
        }

        bool removed = RemoveAccidentalGeneratedMapFromSource(source);
        if (removed)
        {
            EditorSceneManager.MarkSceneDirty(source);
            EditorSceneManager.SaveScene(source);
            Debug.Log("MAP CLEANUP: Removed GreyboxBattlefield01_Generated from SampleScene and saved the clean source scene.");
        }
        else
        {
            Debug.Log("MAP CLEANUP: SampleScene already contains no generated Greybox Battlefield root.");
        }
    }

    private static bool RemoveAccidentalGeneratedMapFromSource(Scene source)
    {
        if (!source.IsValid()) return false;

        bool removedAny = false;
        foreach (GameObject rootObject in source.GetRootGameObjects())
        {
            if (rootObject != null && rootObject.name == GeneratedRootName)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                removedAny = true;
            }
        }

        return removedAny;
    }

    private static void InvokeBuilder(string methodName, params object[] args)
    {
        MethodInfo method = typeof(GreyboxBattlefieldBuilder).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new MissingMethodException(nameof(GreyboxBattlefieldBuilder), methodName);
        }

        method.Invoke(null, args);
    }
}
#endif
