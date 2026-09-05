using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        // The Bootstrap scene is only the battlefield selector. It intentionally contains no
        // GameManager, so never try to start a match there.
        if (activeScene.IsValid() && activeScene.name == MapSelection.BootstrapScene)
        {
            return;
        }

        GameObject runner = new GameObject("MatchBootstrapper_Runtime");
        runner.AddComponent<MatchBootstrapperRunner>();
    }

    private sealed class MatchBootstrapperRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Allow the selected scene's Awake/Start methods to initialise first.
            yield return null;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError($"MatchBootstrapper: No GameManager instance was found in scene '{SceneManager.GetActiveScene().name}'.");
                Destroy(gameObject);
                yield break;
            }

            HideObsoleteMenu(gameManager);

            if (MapSelection.LaunchMode == MapLaunchMode.Test)
            {
                Debug.Log($"MatchBootstrapper: starting AUTO TEST on {MapSelection.GetSelectedDisplayName()}.");

                // StartAutoTestFromMenu uses the existing, established automated-test path:
                // defender player perspective, both spawners active, accelerated time and
                // multi-match telemetry.
                gameManager.StartAutoTestFromMenu();
                Destroy(gameObject);
                yield break;
            }

            // If a reloaded automated-test match is already underway, its GameManager Awake
            // routine owns startup. Do not start a second match on top of it.
            if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
            {
                Destroy(gameObject);
                yield break;
            }

            if (gameManager.playerGameplayUI != null)
            {
                gameManager.playerGameplayUI.SetActive(true);
            }

            // Normal PLAY mode currently assigns the player's side randomly. Both maps use the
            // same startup path, so this can later be replaced by AUTO / ASSIST / MANUAL without
            // changing map scenes.
            Faction assignedFaction = Random.value < 0.5f ? Faction.Attacker : Faction.Defender;
            Debug.Log($"MatchBootstrapper: starting PLAY on {MapSelection.GetSelectedDisplayName()}, player faction = {assignedFaction}.");

            if (assignedFaction == Faction.Attacker)
            {
                gameManager.SelectAttackerFaction();
            }
            else
            {
                gameManager.SelectDefenderFaction();
            }

            Destroy(gameObject);
        }

        private static void HideObsoleteMenu(GameManager gameManager)
        {
            if (gameManager.factionSelectionUI != null)
            {
                gameManager.factionSelectionUI.SetActive(false);
            }

            // Recovery scenes have carried several names for the old prototype menu over time.
            // Disable any that exist, but do not delete them from the preserved scene yet.
            string[] obsoleteMenuNames =
            {
                "Canvas_FactionSelect",
                "MainMenu_Container",
                "Canvas_MainMenu"
            };

            foreach (string menuName in obsoleteMenuNames)
            {
                GameObject oldMenu = GameObject.Find(menuName);
                if (oldMenu != null)
                {
                    oldMenu.SetActive(false);
                }
            }
        }
    }
}
