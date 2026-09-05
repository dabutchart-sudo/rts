using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Map selector owned by the dedicated Bootstrap scene. No gameplay scene creates this menu,
/// so a battle cannot start behind it.
/// </summary>
public sealed class MapSelectionMenu : MonoBehaviour
{
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
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
        float panelWidth = Mathf.Min(Screen.width - 40f, 650f * scale);
        float panelHeight = 390f * scale;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.025f, 0.035f, 0.045f, 1f), 0f, 0f);
        GUI.Box(panel, GUIContent.none, new GUIStyle { normal = { background = panelTexture } });

        GUILayout.BeginArea(new Rect(panel.x + 34f * scale, panel.y + 28f * scale, panel.width - 68f * scale, panel.height - 56f * scale));
        GUILayout.Label("SELECT BATTLEFIELD", titleStyle);
        GUILayout.Space(4f * scale);
        GUILayout.Label("Choose the battlefield for this match.", subtitleStyle);
        GUILayout.Space(28f * scale);

        if (GUILayout.Button("DEVELOPMENT BATTLEFIELD\n<size=70%>Recovered original development map</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.DevelopmentTestScene);
        }

        GUILayout.Space(14f * scale);

        if (GUILayout.Button("GREYBOX BATTLEFIELD 01\n<size=70%>Three sectors • six objectives</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.GreyboxBattlefieldScene);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Last selected: " + MapSelection.GetSelectedDisplayName(), smallStyle);
        GUILayout.EndArea();
    }

    private void Launch(string sceneName)
    {
        MapSelection.SelectedSceneName = sceneName;

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
