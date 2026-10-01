using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ZoomSliderHUD : MonoBehaviour
{
    static ZoomSliderHUD instance;
    static Sprite whiteSprite;

    Slider slider;
    GameObject panel;
    bool dragging;
    bool suppress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateHUD()
    {
        if (FindAnyObjectByType<ZoomSliderHUD>(FindObjectsInactive.Include) != null) return;
        EnsureInstance();
    }

    public static void SetVisible(bool visible)
    {
        EnsureInstance();
        if (instance == null) return;
        instance.ApplyVisible(visible);
    }

    static void EnsureInstance()
    {
        if (instance != null) return;
        ZoomSliderHUD existing = FindAnyObjectByType<ZoomSliderHUD>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("ZoomSliderHUD");
        host.AddComponent<ZoomSliderHUD>();
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void ApplyVisible(bool visible)
    {
        if (visible && panel == null) BuildUI();
        if (panel == null) return;
        panel.SetActive(visible);
        if (visible) SyncFromCamera();
    }

    void Update()
    {
        if (dragging && (Mouse.current == null || !Mouse.current.leftButton.isPressed)) dragging = false;
        if (panel == null || !panel.activeSelf || dragging) return;
        SyncFromCamera();
    }

    void SyncFromCamera()
    {
        if (slider == null) return;
        RTSCamera camera = ActiveCamera();
        if (camera == null) return;
        suppress = true;
        slider.SetValueWithoutNotify(camera.NormalizedZoom);
        suppress = false;
    }

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("ZoomCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("ZoomPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(280f, 64f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = White();
        panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.94f);
        panelImage.raycastTarget = true;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(panel.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(14f, 0f);
        labelRect.sizeDelta = new Vector2(64f, 0f);
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "Zoom";
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;

        GameObject sliderObject = new GameObject("Zoom", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.offsetMin = new Vector2(84f, -14f);
        sliderRect.offsetMax = new Vector2(-16f, 14f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.28f);
        backgroundRect.anchorMax = new Vector2(1f, 0.72f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = White();
        backgroundImage.color = new Color(0.22f, 0.25f, 0.3f, 1f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.28f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.72f);
        fillAreaRect.offsetMin = new Vector2(4f, 0f);
        fillAreaRect.offsetMax = new Vector2(-4f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = White();
        fillImage.color = new Color(0.25f, 0.55f, 0.9f, 1f);
        fillImage.raycastTarget = false;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 28f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = White();
        handleImage.color = new Color(0.95f, 0.86f, 0.45f, 1f);

        slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.onValueChanged.AddListener(OnSliderChanged);

        EventTrigger trigger = sliderObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, () => dragging = true);
        AddTrigger(trigger, EventTriggerType.PointerUp, () => dragging = false);
        SyncFromCamera();
    }

    void OnSliderChanged(float value)
    {
        if (suppress) return;
        RTSCamera camera = ActiveCamera();
        if (camera == null) return;
        camera.SetNormalizedZoom(value);
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    static RTSCamera ActiveCamera()
    {
        Camera camera = Camera.main;
        return camera != null ? camera.GetComponent<RTSCamera>() : null;
    }

    static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }
}
