using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recovery-safe runtime match starter. This is a concrete MonoBehaviour (rather than a
/// nested runtime helper) so Unity can reliably execute its coroutine after a battlefield
/// scene is loaded from Bootstrap.
/// </summary>
public sealed class MatchBootstrapper : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMatchAutomatically()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;

        if (activeScene.name != MapSelection.OriginalMapScene &&
            activeScene.name != MapSelection.ChatGPTMapScene)
        {
            return;
        }

        if (FindAnyObjectByType<MatchBootstrapper>() != null) return;

        GameObject runner = new GameObject("MatchBootstrapper_Runtime");
        runner.AddComponent<MatchBootstrapper>();
    }

    private IEnumerator Start()
    {
        // Give the recovered scene one frame to finish Awake/Start initialisation.
        yield return null;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError($"MatchBootstrapper: No GameManager instance was found in '{SceneManager.GetActiveScene().name}'.");
            Destroy(gameObject);
            yield break;
        }

        Debug.Log($"MatchBootstrapper: active in '{SceneManager.GetActiveScene().name}', bypassing recovered front end.");

        HideRecoveredFrontEnd(gameManager);

        if (gameManager.playerGameplayUI != null)
        {
            gameManager.playerGameplayUI.SetActive(true);
        }

        // If a continuing automated batch already owns startup, do not start another match.
        if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
        {
            Destroy(gameObject);
            yield break;
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

        // Find by scene hierarchy, including inactive objects, and disable the known recovered
        // front-end containers at runtime only. Nothing is deleted or saved back into the scene.
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in allObjects)
        {
            if (candidate == null || !candidate.scene.IsValid()) continue;
            if (candidate.scene != SceneManager.GetActiveScene()) continue;

            if (candidate.name == "Canvas_FactionSelect" ||
                candidate.name == "MainMenu_Container" ||
                candidate.name == "Canvas_MainMenu")
            {
                candidate.SetActive(false);
            }
        }
    }
}
