using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared map-selection state. The bootstrap scene chooses a battlefield, then the selected
/// gameplay scene is loaded normally. Gameplay scenes do not create or own the map menu.
/// </summary>
public static class MapSelection
{
    private const string SelectedMapKey = "RTS_SelectedMapScene";

    public const string BootstrapScene = "Bootstrap";
    public const string DevelopmentTestScene = "RecoveredDevelopmentMap";
    public const string GreyboxBattlefieldScene = "GreyboxBattlefield01";

    public static string SelectedSceneName
    {
        get => PlayerPrefs.GetString(SelectedMapKey, DevelopmentTestScene);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            PlayerPrefs.SetString(SelectedMapKey, value);
            PlayerPrefs.Save();
        }
    }

    public static void SelectDevelopmentTestMap()
    {
        SelectedSceneName = DevelopmentTestScene;
    }

    public static void SelectGreyboxBattlefield()
    {
        SelectedSceneName = GreyboxBattlefieldScene;
    }

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
        return SelectedSceneName == GreyboxBattlefieldScene
            ? "Greybox Battlefield 01"
            : "Development Battlefield";
    }
}
