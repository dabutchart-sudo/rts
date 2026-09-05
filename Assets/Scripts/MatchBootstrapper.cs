using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Direct-battlefield fallback for development/testing.
/// Normal game flow starts in Bootstrap; if a battlefield scene is entered directly in the
/// editor, this runner delegates to BattlefieldRuntimeLauncher so startup behaviour matches
/// the Bootstrap path instead of maintaining a second set of launch rules.
/// </summary>
public sealed class MatchBootstrapper : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!BattlefieldRuntimeLauncher.IsBattlefieldScene(activeScene)) return;
        if (FindAnyObjectByType<MatchBootstrapper>() != null) return;

        GameObject runner = new GameObject("MatchBootstrapper_Runtime");
        runner.AddComponent<MatchBootstrapper>();
    }

    private IEnumerator Start()
    {
        // Give the scene-owned GameplaySystems prefab one frame to finish Awake/Start.
        yield return null;

        Scene activeScene = SceneManager.GetActiveScene();
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError($"MatchBootstrapper: No GameManager instance was found in '{activeScene.name}'.");
            Destroy(gameObject);
            yield break;
        }

        BattlefieldRuntimeLauncher.PrepareBattlefield(activeScene, gameManager);
        Debug.Log($"MatchBootstrapper: direct battlefield launch prepared for '{activeScene.name}'.");

        BattlefieldRuntimeLauncher.StartNormalMatch(gameManager, "MatchBootstrapper");
        Destroy(gameObject);
    }
}
