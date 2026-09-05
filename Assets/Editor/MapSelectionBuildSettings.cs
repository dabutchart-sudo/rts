using UnityEditor;
using UnityEngine;

public static class MapSelectionBuildSettings
{
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string OriginalMapPath = "Assets/Scenes/RecoveredDevelopmentMap.unity";
    private const string ChatGPTMapPath = "Assets/Scenes/GreyboxBattlefield01.unity";

    [MenuItem("RTS/Maps/Configure Recovery Map Selector")]
    public static void ConfigureRecoverySelector()
    {
        string[] required = { BootstrapPath, OriginalMapPath, ChatGPTMapPath };

        foreach (string path in required)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"MAP SELECT: Required scene is missing: {path}");
                return;
            }
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootstrapPath, true),
            new EditorBuildSettingsScene(OriginalMapPath, true),
            new EditorBuildSettingsScene(ChatGPTMapPath, true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("MAP SELECT: Recovery selector configured. Bootstrap is scene 0; Original Map and ChatGPT Map are enabled. No gameplay scene was modified.");
    }
}
