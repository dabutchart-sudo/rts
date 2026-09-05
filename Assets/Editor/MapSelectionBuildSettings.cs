using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures the dedicated bootstrap scene first, followed by the two authored gameplay maps.
/// SampleScene is deliberately not deleted; it is simply no longer part of the map selector.
/// </summary>
public static class MapSelectionBuildSettings
{
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string DevelopmentScenePath = "Assets/Scenes/RecoveredDevelopmentMap.unity";
    private const string GreyboxScenePath = "Assets/Scenes/GreyboxBattlefield01.unity";

    [MenuItem("RTS/Maps/Configure Map Selection Build Settings")]
    public static void ConfigureMapSelectionBuildSettings()
    {
        string[] requiredPaths =
        {
            BootstrapScenePath,
            DevelopmentScenePath,
            GreyboxScenePath
        };

        List<EditorBuildSettingsScene> configured = new List<EditorBuildSettingsScene>();

        foreach (string path in requiredPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"MAP SELECT: Required scene is missing: '{path}'. Build Settings were not changed.");
                return;
            }

            configured.Add(new EditorBuildSettingsScene(path, true));
        }

        // Preserve any unrelated build scenes after the map-selection scenes, but deliberately
        // leave the obsolete SampleScene out of the configured gameplay path.
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path == "Assets/Scenes/SampleScene.unity") continue;
            if (System.Array.IndexOf(requiredPaths, existing.path) >= 0) continue;
            configured.Add(existing);
        }

        EditorBuildSettings.scenes = configured.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("MAP SELECT: Bootstrap is scene 0; Development Battlefield and Greybox Battlefield 01 are enabled.");
    }
}
