using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small shared guard for runtime systems that should exist only in actual battlefield scenes.
/// Bootstrap is a launcher, not a gameplay scene.
/// </summary>
public static class SceneRuntimeGate
{
    public static bool IsBattlefieldScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return false;

        return scene.name == MapSelection.OriginalMapScene ||
               scene.name == MapSelection.ChatGPTMapScene;
    }
}
