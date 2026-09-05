using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main battlefield launcher. Each map can be started as a normal playable match or as an
/// automated AI test. The ChatGPT Map is the current prototyping focus.
/// </summary>
public sealed class MapSelectionMenu : MonoBehaviour
{
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle mapTitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;
    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;

    private void OnDestroy()
    {
        DestroyTexture(panelTexture);
        DestroyTexture(buttonTexture);
        DestroyTexture(buttonHoverTexture);
    }

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 700f), 0.75f, 1.35f);
        float panelWidth = Mathf.Min(Screen.width - 40f, 720f * scale);
        float panelHeight = 500f * scale;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.025f, 0.035f, 0.045f, 1f), 0f, 0f);
        GUI.Box(panel, GUIContent.none, new GUIStyle { normal = { background = panelTexture } });

        GUILayout.BeginArea(new Rect(panel.x + 34f * scale, panel.y + 24f * scale, panel.width - 68f * scale, panel.height - 48f * scale));
        GUILayout.Label("SELECT BATTLEFIELD", titleStyle);
        GUILayout.Space(2f * scale);
        GUILayout.Label("Choose a map, then play it normally or run an automated AI test.", subtitleStyle);
        GUILayout.Space(20f * scale);

        DrawMap("ORIGINAL MAP", "Your recovered original battlefield", MapSelection.OriginalMapScene, scale);
        GUILayout.Space(18f * scale);
        DrawMap("CHATGPT MAP", "Current prototyping map • three sectors • six objectives", MapSelection.ChatGPTMapScene, scale);

        GUILayout.FlexibleSpace();
        GUILayout.Label("Prototype focus: ChatGPT Map", smallStyle);
        GUILayout.EndArea();
    }

    private void DrawMap(string title, string description, string sceneName, float scale)
    {
        GUILayout.Label(title, mapTitleStyle);
        GUILayout.Label(description, subtitleStyle);
        GUILayout.Space(7f * scale);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("PLAY", buttonStyle, GUILayout.Height(48f * scale)))
        {
            Launch(sceneName, MapLaunchMode.Play);
        }

        GUILayout.Space(12f * scale);

        if (GUILayout.Button("AUTO TEST", buttonStyle, GUILayout.Height(48f * scale)))
        {
            Launch(sceneName, MapLaunchMode.Test);
        }
        GUILayout.EndHorizontal();
    }

    private void Launch(string sceneName, MapLaunchMode launchMode)
    {
        MapSelection.ConfigureLaunch(sceneName, launchMode);

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MAP SELECT: Scene '{sceneName}' is not available. Use RTS > Maps > Configure Map Selection Build Settings.");
            return;
        }

        SceneManager.LoadScene(sceneName);
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

        mapTitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
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
