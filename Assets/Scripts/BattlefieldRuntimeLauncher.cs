using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single runtime launch path for battlefield scenes.
///
/// Bootstrap/map-selection launches and direct-in-editor battlefield launches both delegate here,
/// so startup behaviour is defined in one place rather than duplicated across recovery helpers.
/// </summary>
public static class BattlefieldRuntimeLauncher
{
    public static bool IsBattlefieldScene(Scene scene)
    {
        if (!scene.IsValid()) return false;

        return scene.name == MapSelection.OriginalMapScene ||
               scene.name == MapSelection.ChatGPTMapScene;
    }

    /// <summary>
    /// Restores runtime-created services that are shared by every battlefield and hides the
    /// obsolete recovered front-end containers if any still exist in a migrated scene.
    /// Safe to call repeatedly.
    /// </summary>
    public static void PrepareBattlefield(Scene scene, GameManager gameManager)
    {
        if (!scene.IsValid() || gameManager == null) return;

        HideLegacyFrontEnd(scene, gameManager);

        if (gameManager.playerGameplayUI != null)
        {
            gameManager.playerGameplayUI.SetActive(true);
        }

        EnsureRuntimeSystems();
    }

    /// <summary>
    /// Starts a normal match if automated testing does not already own startup.
    /// Returns true when this method assigned a player faction and started the match.
    /// </summary>
    public static bool StartNormalMatch(GameManager gameManager, string logPrefix)
    {
        if (gameManager == null) return false;

        if (gameManager.enableAutoTestMode || TestDashboardOverlay.CurrentMatchNumber > 1)
        {
            return false;
        }

        // Startup is idempotent. If another launch path has already assigned a faction, do not
        // start the match a second time.
        if (gameManager.playerFaction != Faction.None)
        {
            return false;
        }

        Faction assignedFaction = Random.value < 0.5f ? Faction.Attacker : Faction.Defender;
        Debug.Log($"{logPrefix}: player faction = {assignedFaction}.");

        if (assignedFaction == Faction.Attacker)
        {
            gameManager.SelectAttackerFaction();
        }
        else
        {
            gameManager.SelectDefenderFaction();
        }

        return true;
    }

    public static void EnsureRuntimeSystems()
    {
        EnsureComponent<ControlModeManager>("ControlModeManager");
        EnsureComponent<ControlModeHUD>("ControlModeHUD");

        SpecialistDeploymentTracker.EnsureInstance();
        EnsureComponent<EngineerPurchasableBootstrap>("EngineerPurchasableBootstrap");
        EnsureComponent<ReconPurchasableBootstrap>("ReconPurchasableBootstrap");
        EnsureComponent<SupportPurchasableBootstrap>("SupportPurchasableBootstrap");
        EnsureComponent<SpecialistAbilityBootstrap>("SpecialistAbilityBootstrap");

        // Diagnostics are intentionally part of the current development/observer experience.
        SquadAIDebugOverlay.EnsureInstance(true);
    }

    private static T EnsureComponent<T>(string objectName) where T : Component
    {
        T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject host = new GameObject(objectName);
        return host.AddComponent<T>();
    }

    private static void HideLegacyFrontEnd(Scene scene, GameManager gameManager)
    {
        if (gameManager.factionSelectionUI != null)
        {
            gameManager.factionSelectionUI.SetActive(false);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item == null) continue;

                if (item.name == "Canvas_FactionSelect" ||
                    item.name == "MainMenu_Container" ||
                    item.name == "Canvas_MainMenu")
                {
                    item.gameObject.SetActive(false);
                }
            }
        }
    }
}
