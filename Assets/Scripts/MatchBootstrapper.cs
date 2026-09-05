using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        // Bootstrap is only a selector and intentionally has no GameManager.
        if (!activeScene.IsValid() || activeScene.name == MapSelection.BootstrapScene) return;

        // Recovery safety: only start known battlefield scenes. Do not inject match state into
        // utility/editor scenes that happen to be played directly.
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

            // Preserve the known automated-test startup path if a test is already configured.
            if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
            {
                Destroy(gameObject);
                yield break;
            }

            // For the first recovery checkpoint we do not delete or rewrite any menu/UI scene
            // objects. We simply bypass the obsolete faction menu at runtime.
            if (gameManager.factionSelectionUI != null)
            {
                gameManager.factionSelectionUI.SetActive(false);
            }

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
    }
}
