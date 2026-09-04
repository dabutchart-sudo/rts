using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps authored gameplay scenes available to the runtime map selector.
/// This is intentionally an explicit editor command rather than silently rewriting project
/// settings on every domain reload.
/// </summary>
public static class MapSelectionBuildSettings
{
    private const string DevelopmentScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GreyboxScenePath = "Assets/Scenes/GreyboxBattlefield01.unity";

    [MenuItem("RTS/Maps/Ensure Gameplay Scenes In Build Settings")]
    public static void EnsureGameplayScenes()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = EnsureScene(scenes, DevelopmentScenePath);
        changed |= EnsureScene(scenes, GreyboxScenePath);

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("MAP SELECT: Development Test Map and Greybox Battlefield 01 are enabled in Build Settings.");
        }
        else
        {
            Debug.Log("MAP SELECT: Gameplay scenes are already enabled in Build Settings.");
        }
    }

    private static bool EnsureScene(List<EditorBuildSettingsScene> scenes, string path)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != path) continue;

            if (scenes[i].enabled) return false;
            scenes[i] = new EditorBuildSettingsScene(path, true);
            return true;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogWarning($"MAP SELECT: Cannot add missing scene '{path}'.");
            return false;
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        return true;
    }
}
