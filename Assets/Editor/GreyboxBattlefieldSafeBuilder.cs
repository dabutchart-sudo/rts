#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reproducible build pipeline for the ChatGPT Map (GreyboxBattlefield01).
///
/// IMPORTANT: the recovered Original Map is now the gameplay-system template. The ChatGPT Map
/// is created as a COPY of that known-rich scene, then only its battlefield geometry/objectives/
/// bases/navigation are replaced. This prevents the greybox from inheriting the ancient,
/// incomplete SampleScene gameplay wiring.
///
/// The Original Map itself is never modified by this builder.
/// </summary>
public static class GreyboxBattlefieldSafeBuilder
{
    private const string SourceScene = "Assets/Scenes/RecoveredDevelopmentMap.unity";
    private const string TargetScene = "Assets/Scenes/GreyboxBattlefield01.unity";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";

    [MenuItem("RTS/Maps/Rebuild ChatGPT Map (Complete)")]
    public static void RebuildComplete()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        if (!source.IsValid())
        {
            Debug.LogError("CHATGPT MAP BUILD: Could not open the recovered Original Map.");
            return;
        }

        // saveAsCopy=true is deliberate. RecoveredDevelopmentMap is our preserved source and
        // must not become or be modified as the ChatGPT Map.
        if (!EditorSceneManager.SaveScene(source, TargetScene, true))
        {
            Debug.LogError("CHATGPT MAP BUILD: Could not create GreyboxBattlefield01 from the Original Map template.");
            return;
        }

        Scene target = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        if (!target.IsValid() || target.path != TargetScene)
        {
            Debug.LogError($"CHATGPT MAP BUILD: Expected active scene '{TargetScene}', but got '{target.path}'.");
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
            Debug.LogError($"CHATGPT MAP BUILD: Failed while creating the playable map. {ex.Message}\n{ex.StackTrace}");
            return;
        }

        Selection.activeGameObject = GameObject.Find(GeneratedRootName);

        Debug.Log(
            "CHATGPT MAP BUILD: GreyboxBattlefield01 rebuilt from the recovered gameplay template. " +
            "The Original Map was left untouched. Geometry, 3 sectors, 6 objectives, dynamic bases, " +
            "spawn references, readability and NavMesh have been applied. Test the map before committing the generated scene.");
    }

    [MenuItem("RTS/Maps/Rebuild Greybox Battlefield 01 (Complete)")]
    private static void RebuildLegacyMenuName()
    {
        RebuildComplete();
    }

    [MenuItem("RTS/Maps/Build Greybox Battlefield 01 (Safe)")]
    public static void BuildSafe()
    {
        RebuildComplete();
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
