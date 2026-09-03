using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlModeHUD : MonoBehaviour
{
    private Button autoButton;
    private Button assistButton;
    private Button manualButton;
    private TextMeshProUGUI autoText;
    private TextMeshProUGUI assistText;
    private TextMeshProUGUI manualText;

    private static readonly Color SelectedColor = new Color(0.16f, 0.42f, 0.78f, 0.95f);
    private static readonly Color NormalColor = new Color(0.10f, 0.12f, 0.16f, 0.88f);
    private static readonly Color TextColor = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateHUD()
    {
        if (FindFirstObjectByType<ControlModeHUD>(FindObjectsInactive.Include) != null) return;

        GameObject host = new GameObject("ControlModeHUD");
        host.AddComponent<ControlModeHUD>();
    }

    private void Start()
    {
        BuildUI();

        if (ControlModeManager.Instance != null)
        {
            ControlModeManager.Instance.ModeChanged += HandleModeChanged;
            Refresh(ControlModeManager.Instance.CurrentMode);
        }
    }

    private void OnDestroy()
    {
        if (ControlModeManager.Instance != null)
        {
            ControlModeManager.Instance.ModeChanged -= HandleModeChanged;
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = GameObject.Find("Canvas_Gameplay");
        if (canvasObject == null)
        {
            Debug.LogWarning("ControlModeHUD: Canvas_Gameplay was not found.");
            return;
        }

        GameObject panel = new GameObject("ControlModePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(390f, 58f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.07f, 0.82f);

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(7, 7, 7, 7);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        autoButton = CreateButton(panel.transform, "AUTO", out autoText, ControlMode.Auto);
        assistButton = CreateButton(panel.transform, "ASSIST", out assistText, ControlMode.Assist);
        manualButton = CreateButton(panel.transform, "MANUAL", out manualText, ControlMode.Manual);
    }

    private Button CreateButton(Transform parent, string label, out TextMeshProUGUI labelText, ControlMode mode)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = NormalColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SetMode(mode));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        labelText = textObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 18f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = TextColor;
        labelText.raycastTarget = false;

        TextMeshProUGUI existingText = Object.FindAnyObjectByType<TextMeshProUGUI>();
        if (existingText != null && existingText.font != null)
        {
            labelText.font = existingText.font;
        }

        return button;
    }

    private void SetMode(ControlMode mode)
    {
        if (ControlModeManager.Instance == null)
        {
            Debug.LogWarning("ControlModeHUD: ControlModeManager is not available.");
            return;
        }

        ControlModeManager.Instance.SetMode(mode);
        Refresh(mode);
    }

    private void HandleModeChanged(ControlMode mode)
    {
        Refresh(mode);
    }

    private void Refresh(ControlMode mode)
    {
        SetButtonVisual(autoButton, autoText, mode == ControlMode.Auto);
        SetButtonVisual(assistButton, assistText, mode == ControlMode.Assist);
        SetButtonVisual(manualButton, manualText, mode == ControlMode.Manual);
    }

    private void SetButtonVisual(Button button, TextMeshProUGUI text, bool selected)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? SelectedColor : NormalColor;
        }

        if (text != null)
        {
            text.text = selected ? $"> {text.name.Replace("Label", string.Empty)}" : text.text;
        }
    }
}