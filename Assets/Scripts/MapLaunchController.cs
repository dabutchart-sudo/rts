using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts the selected gameplay scene without requiring a map-specific front-end menu.
/// PLAY currently starts as Attackers; AUTO TEST uses the existing GameManager test system.
/// This keeps launch behaviour shared by Original Map and ChatGPT Map.
/// </summary>
public static class MapLaunchController
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoaded()
    {
        if (SceneManager.GetActiveScene().name == MapSelection.BootstrapScene) return;

        GameObject runner = new GameObject("Map Launch Controller");
        runner.AddComponent<MapLaunchRunner>();
    }

    private sealed class MapLaunchRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Let scene Awake/Start wiring settle before beginning the match.
            yield return null;

            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError($"MAP LAUNCH: No GameManager found in scene '{SceneManager.GetActiveScene().name}'.");
                Destroy(gameObject);
                yield break;
            }

            // The obsolete Original Map front-end is intentionally bypassed. We leave the
            // recovered scene data intact for now, but it is no longer part of the launch flow.
            if (gameManager.factionSelectionUI != null)
            {
                gameManager.factionSelectionUI.SetActive(false);
            }

            if (MapSelection.LaunchMode == MapLaunchMode.Test)
            {
                Debug.Log($"MAP LAUNCH: Starting AUTO TEST on {MapSelection.GetSelectedDisplayName()}.");
                gameManager.StartAutoTestFromMenu();
            }
            else
            {
                Debug.Log($"MAP LAUNCH: Starting PLAY on {MapSelection.GetSelectedDisplayName()} as Attackers.");
                gameManager.SelectAttackerFaction();
            }

            Destroy(gameObject);
        }
    }
}
