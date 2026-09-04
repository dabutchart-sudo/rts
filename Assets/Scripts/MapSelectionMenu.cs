using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lightweight, map-independent launcher shown once when Play starts.
/// It deliberately lives in code rather than inside a battlefield scene so maps contain
/// world data only. The same launcher works whether Play is pressed from SampleScene or
/// an authored battlefield.
/// </summary>
public sealed class MapSelectionMenu : MonoBehaviour
{
    private static bool selectionMadeThisSession;
    private static MapSelectionMenu instance;

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;
    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnFirstSceneLoad()
    {
        if (selectionMadeThisSession || instance != null) return;

        GameObject root = new GameObject("Map Selection Menu");
        instance = root.AddComponent<MapSelectionMenu>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        DestroyTexture(panelTexture);
        DestroyTexture(buttonTexture);
        DestroyTexture(buttonHoverTexture);
    }

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 700f), 0.75f, 1.35f);
        float panelWidth = Mathf.Min(Screen.width - 40f, 650f * scale);
        float panelHeight = 390f * scale;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.025f, 0.035f, 0.045f, 0.94f), 0f, 0f);
        GUI.Box(panel, GUIContent.none, new GUIStyle { normal = { background = panelTexture } });

        GUILayout.BeginArea(new Rect(panel.x + 34f * scale, panel.y + 28f * scale, panel.width - 68f * scale, panel.height - 56f * scale));
        GUILayout.Label("SELECT BATTLEFIELD", titleStyle);
        GUILayout.Space(4f * scale);
        GUILayout.Label("Choose the map for this match. Gameplay systems and HUD are shared between battlefields.", subtitleStyle);
        GUILayout.Space(28f * scale);

        if (GUILayout.Button("DEVELOPMENT TEST MAP\n<size=70%>Original development battlefield</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.DevelopmentTestScene);
        }

        GUILayout.Space(14f * scale);

        if (GUILayout.Button("GREYBOX BATTLEFIELD 01\n<size=70%>Three sectors • six objectives</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.GreyboxBattlefieldScene);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Current: " + MapSelection.GetSelectedDisplayName(), smallStyle);
        GUILayout.EndArea();
    }

    private void Launch(string sceneName)
    {
        MapSelection.SelectedSceneName = sceneName;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MAP SELECT: Scene '{sceneName}' is not available. Use RTS > Maps > Ensure Gameplay Scenes In Build Settings.");
            return;
        }

        selectionMadeThisSession = true;
        SceneManager.LoadScene(sceneName);
        Destroy(gameObject);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        panelTexture = MakeTexture(new Color(0.075f, 0.09f, 0.105f, 0.98f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.15f, 0.18f, 1f));
        buttonHoverTexture = MakeTexture(new Color(0.18f, 0.23f, 0.27f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            wordWrap = true,
            normal = { textColor = new Color(0.72f, 0.78f, 0.82f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            richText = true,
            wordWrap = true,
            normal = { background = buttonTexture, textColor = Color.white },
            hover = { background = buttonHoverTexture, textColor = Color.white },
            active = { background = buttonHoverTexture, textColor = Color.white }
        };

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            normal = { textColor = new Color(0.55f, 0.62f, 0.67f) }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null) Destroy(texture);
    }
}
