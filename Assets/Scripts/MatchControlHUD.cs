using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shared battlefield testing controls. Created at runtime for every playable map so testers can
/// restart the current battle or return to the Bootstrap map selector without stopping Play mode.
/// </summary>
public class MatchControlHUD : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.86f);
    private static readonly Color RestartColor = new Color(0.12f, 0.15f, 0.20f, 0.96f);
    private static readonly Color EndColor = new Color(0.58f, 0.10f, 0.10f, 0.96f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateHUD()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!BattlefieldRuntimeLauncher.IsBattlefieldScene(scene)) return;
        EnsureInstance();
    }

    public static MatchControlHUD EnsureInstance()
    {
        MatchControlHUD existing = FindAnyObjectByType<MatchControlHUD>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject host = new GameObject("MatchControlHUD");
        return host.AddComponent<MatchControlHUD>();
    }

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        Canvas gameplayCanvas = FindGameplayCanvas();
        if (gameplayCanvas == null)
        {
            Debug.LogWarning("MatchControlHUD: Canvas_Gameplay was not found.");
            return;
        }

        GraphicRaycaster raycaster = gameplayCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameplayCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        raycaster.enabled = true;

        Transform oldPanel = gameplayCanvas.transform.Find("MatchControlPanel");
        if (oldPanel != null)
        {
            Destroy(oldPanel.gameObject);
        }

        GameObject panel = new GameObject(
            "MatchControlPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup));

        panel.transform.SetParent(gameplayCanvas.transform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(330f, 52f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        CreateButton(panel.transform, "RESTART GAME", RestartColor, RestartGame);
        CreateButton(panel.transform, "END GAME", EndColor, EndGame);
    }

    private static Canvas FindGameplayCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.name == "Canvas_Gameplay")
            {
                return canvas;
            }
        }
        return null;
    }

    private static Button CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            label.Replace(" ", string.Empty) + "Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 16f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;

        TextMeshProUGUI existingText = FindAnyObjectByType<TextMeshProUGUI>();
        if (existingText != null && existingText.font != null)
        {
            text.font = existingText.font;
        }

        return button;
    }

    private static void RestartGame()
    {
        Scene current = SceneManager.GetActiveScene();
        if (!BattlefieldRuntimeLauncher.IsBattlefieldScene(current)) return;

        MapSelection.SelectedSceneName = current.name;
        SceneManager.LoadScene(current.name);
    }

    private static void EndGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(MapSelection.BootstrapScene))
        {
            Debug.LogError("MatchControlHUD: Bootstrap scene is not available in Build Settings.");
            return;
        }

        SceneManager.LoadScene(MapSelection.BootstrapScene);
    }
}
