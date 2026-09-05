using UnityEngine;
using UnityEngine.SceneManagement;

public enum MapLaunchMode
{
    Play,
    Test
}

/// <summary>
/// Shared map-selection state. The bootstrap scene chooses a battlefield and whether it should
/// launch as a normal playable match or an automated test.
/// </summary>
public static class MapSelection
{
    private const string SelectedMapKey = "RTS_SelectedMapScene";
    private const string LaunchModeKey = "RTS_MapLaunchMode";

    public const string BootstrapScene = "Bootstrap";
    public const string OriginalMapScene = "RecoveredDevelopmentMap";
    public const string ChatGPTMapScene = "GreyboxBattlefield01";

    // Compatibility aliases for older code while the map-selection system is being cleaned up.
    public const string DevelopmentTestScene = OriginalMapScene;
    public const string GreyboxBattlefieldScene = ChatGPTMapScene;

    public static string SelectedSceneName
    {
        get => PlayerPrefs.GetString(SelectedMapKey, ChatGPTMapScene);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            PlayerPrefs.SetString(SelectedMapKey, value);
            PlayerPrefs.Save();
        }
    }

    public static MapLaunchMode LaunchMode
    {
        get => (MapLaunchMode)PlayerPrefs.GetInt(LaunchModeKey, (int)MapLaunchMode.Play);
        set
        {
            PlayerPrefs.SetInt(LaunchModeKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static void ConfigureLaunch(string sceneName, MapLaunchMode launchMode)
    {
        SelectedSceneName = sceneName;
        LaunchMode = launchMode;
    }

    public static void SelectDevelopmentTestMap() => SelectedSceneName = OriginalMapScene;
    public static void SelectGreyboxBattlefield() => SelectedSceneName = ChatGPTMapScene;

    public static bool TryLoadSelectedMap()
    {
        string sceneName = SelectedSceneName;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"MAP SELECT: Scene '{sceneName}' is not in Build Settings. Staying in the current scene.");
            return false;
        }

        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static string GetSelectedDisplayName()
    {
        return SelectedSceneName == OriginalMapScene ? "Original Map" : "ChatGPT Map";
    }
}
