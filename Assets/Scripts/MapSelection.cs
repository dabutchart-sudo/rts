using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapSelection
{
    private const string SelectedMapKey = "RTS_SelectedMapScene";

    public const string BootstrapScene = "Bootstrap";
    public const string OriginalMapScene = "RecoveredDevelopmentMap";
    public const string ChatGPTMapScene = "GreyboxBattlefield01";

    public static string SelectedSceneName
    {
        get => PlayerPrefs.GetString(SelectedMapKey, OriginalMapScene);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            PlayerPrefs.SetString(SelectedMapKey, value);
            PlayerPrefs.Save();
        }
    }

    public static bool TryLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MAP SELECT: Scene '{sceneName}' is not available in Build Settings.");
            return false;
        }

        SelectedSceneName = sceneName;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static string GetDisplayName(string sceneName)
    {
        return sceneName == ChatGPTMapScene ? "ChatGPT Map" : "Original Map";
    }
}
