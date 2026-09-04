using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent map-selection state. The current project has one gameplay scene, but this
/// deliberately uses scene names so additional authored maps can be added without changing
/// the match systems. A future procedural generator can also register/select a generated map.
/// </summary>
public static class MapSelection
{
    private const string SelectedMapKey = "RTS_SelectedMapScene";
    public const string DevelopmentTestScene = "SampleScene";
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
            : "Development Test Map";
    }
}
