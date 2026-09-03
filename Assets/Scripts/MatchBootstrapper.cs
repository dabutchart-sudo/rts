using System.Collections;
using UnityEngine;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        GameObject runner = new GameObject("MatchBootstrapper_Runtime");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<MatchBootstrapperRunner>();
    }

    private sealed class MatchBootstrapperRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Allow the scene's Awake/Start methods to initialise first.
            yield return null;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("MatchBootstrapper: No GameManager instance was found.");
                Destroy(gameObject);
                yield break;
            }

            // Automated test runs already have their own startup path.
            if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
            {
                Destroy(gameObject);
                yield break;
            }

            // Hide the old faction-selection menu completely. It is no longer part of the normal flow.
            if (gameManager.factionSelectionUI != null)
            {
                gameManager.factionSelectionUI.SetActive(false);
            }
            else
            {
                GameObject oldMenu = GameObject.Find("Canvas_FactionSelect");
                if (oldMenu != null) oldMenu.SetActive(false);
            }

            if (gameManager.playerGameplayUI != null)
            {
                gameManager.playerGameplayUI.SetActive(true);
            }

            // The design calls for the player's side to be assigned randomly each round.
            Faction assignedFaction = Random.value < 0.5f ? Faction.Attacker : Faction.Defender;

            Debug.Log($"MatchBootstrapper: randomly assigned player faction = {assignedFaction}.");

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
    }
}