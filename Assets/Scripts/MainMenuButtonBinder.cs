using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuButtonBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindMainMenuButtons()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("MainMenuButtonBinder: GameManager was not available after scene load.");
            return;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int boundCount = 0;

        foreach (Button button in buttons)
        {
            if (button == null) continue;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string text = label.text.Trim().ToUpperInvariant();

            if (text.Contains("PLAY AS ATTACKERS"))
            {
                button.onClick.RemoveListener(GameManager.Instance.SelectAttackerFaction);
                button.onClick.AddListener(GameManager.Instance.SelectAttackerFaction);
                boundCount++;
            }
            else if (text.Contains("PLAY AS DEFENDERS"))
            {
                button.onClick.RemoveListener(GameManager.Instance.SelectDefenderFaction);
                button.onClick.AddListener(GameManager.Instance.SelectDefenderFaction);
                boundCount++;
            }
            else if (text.Contains("START AUTO TEST"))
            {
                button.onClick.RemoveListener(GameManager.Instance.StartAutoTestFromMenu);
                button.onClick.AddListener(GameManager.Instance.StartAutoTestFromMenu);
                boundCount++;
            }
        }

        Debug.Log($"MainMenuButtonBinder: wired {boundCount} main-menu button action(s).");
    }
}