#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Safe wrapper around GreyboxBattlefieldBuilder.
/// The original builder saved SampleScene as a copy before generating geometry, which meant
/// the generated map was accidentally authored into SampleScene. This wrapper first performs
/// a real Save As to GreyboxBattlefield01, then invokes the existing geometry helpers there.
/// </summary>
public static class GreyboxBattlefieldSafeBuilder
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string TargetScene = "Assets/Scenes/GreyboxBattlefield01.unity";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";

    [MenuItem("RTS/Maps/Build Greybox Battlefield 01 (Safe)")]
    public static void BuildSafe()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        if (!source.IsValid())
        {
            Debug.LogError("SAFE MAP BUILD: Could not open SampleScene.");
            return;
        }

        // Important: saveAsCopy=false makes the active scene become the new map scene.
        if (!EditorSceneManager.SaveScene(source, TargetScene, false))
        {
            Debug.LogError("SAFE MAP BUILD: Could not create GreyboxBattlefield01 scene.");
            return;
        }

        Scene target = SceneManager.GetActiveScene();
        if (!target.IsValid() || target.path != TargetScene)
        {
            Debug.LogError($"SAFE MAP BUILD: Expected active scene '{TargetScene}', but got '{target.path}'.");
            return;
        }

        GameObject oldGeometry = GameObject.Find(GeneratedRootName);
        if (oldGeometry != null)
        {
            UnityEngine.Object.DestroyImmediate(oldGeometry);
        }

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
        }
        catch (Exception ex)
        {
            Debug.LogError($"SAFE MAP BUILD: Failed while creating map geometry. {ex.Message}\n{ex.StackTrace}");
            return;
        }

        EditorSceneManager.MarkSceneDirty(target);
        EditorSceneManager.SaveScene(target);
        Selection.activeGameObject = root;

        Debug.Log("SAFE MAP BUILD: Greybox Battlefield 01 created in its own scene and is now active. Next run RTS > Maps > Wire Greybox Battlefield 01 for Play.");
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
