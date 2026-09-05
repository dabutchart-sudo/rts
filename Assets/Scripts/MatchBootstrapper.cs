using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || activeScene.name == MapSelection.BootstrapScene) return;

        if (activeScene.name != MapSelection.OriginalMapScene &&
            activeScene.name != MapSelection.ChatGPTMapScene)
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
            yield return null;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError($"MatchBootstrapper: No GameManager instance was found in '{SceneManager.GetActiveScene().name}'.");
                Destroy(gameObject);
                yield break;
            }

            if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
            {
                Destroy(gameObject);
                yield break;
            }

            HideRecoveredFrontEnd(gameManager);

            if (gameManager.playerGameplayUI != null)
            {
                gameManager.playerGameplayUI.SetActive(true);
            }

            Faction assignedFaction = Random.value < 0.5f ? Faction.Attacker : Faction.Defender;
            Debug.Log($"MatchBootstrapper: recovery launch, player faction = {assignedFaction}.");

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

        private static void HideRecoveredFrontEnd(GameManager gameManager)
        {
            if (gameManager.factionSelectionUI != null)
            {
                gameManager.factionSelectionUI.SetActive(false);
            }

            string[] recoveredFrontEndNames =
            {
                "Canvas_FactionSelect",
                "MainMenu_Container",
                "Canvas_MainMenu"
            };

            foreach (string objectName in recoveredFrontEndNames)
            {
                GameObject candidate = GameObject.Find(objectName);
                if (candidate != null)
                {
                    candidate.SetActive(false);
                }
            }
        }
    }
}
