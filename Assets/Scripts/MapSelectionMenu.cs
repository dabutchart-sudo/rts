using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recovery-safe battlefield selector. It exists only in the dedicated Bootstrap scene and
/// deliberately does not alter, clean, initialise or otherwise touch either gameplay scene.
/// For the recovery checkpoint, startup is driven explicitly from this persistent selector
/// rather than relying on RuntimeInitializeOnLoadMethod firing again after SceneManager.LoadScene.
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
    private bool launchInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInBootstrapOnly()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != MapSelection.BootstrapScene) return;
        if (FindAnyObjectByType<MapSelectionMenu>() != null) return;

        new GameObject("Map Selection Menu").AddComponent<MapSelectionMenu>();
    }

    private void OnDestroy()
    {
        DestroyTexture(panelTexture);
        DestroyTexture(buttonTexture);
        DestroyTexture(buttonHoverTexture);
    }

    private void OnGUI()
    {
        if (launchInProgress) return;

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
        GUILayout.Label("Recovery build: choose a battlefield. No gameplay scene assets are modified by this selector.", subtitleStyle);
        GUILayout.Space(28f * scale);

        if (GUILayout.Button("ORIGINAL MAP\n<size=70%>Recovered development battlefield</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.OriginalMapScene);
        }

        GUILayout.Space(14f * scale);

        if (GUILayout.Button("CHATGPT MAP\n<size=70%>Greybox prototype battlefield</size>", buttonStyle, GUILayout.Height(92f * scale)))
        {
            Launch(MapSelection.ChatGPTMapScene);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("First checkpoint: verify Original Map gameplay before changing either scene.", smallStyle);
        GUILayout.EndArea();
    }

    private void Launch(string sceneName)
    {
        if (launchInProgress) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MAP SELECT: Scene '{sceneName}' is not available in Build Settings.");
            return;
        }

        launchInProgress = true;
        MapSelection.SelectedSceneName = sceneName;

        // Keep this known-good Bootstrap object alive just long enough to start the recovered
        // GameManager after the selected scene has loaded. This avoids depending on another
        // runtime-initialize callback during scene switching.
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleBattlefieldLoaded;
        Debug.Log($"MAP SELECT: loading '{sceneName}'.");
        SceneManager.LoadScene(sceneName);
    }

    private void HandleBattlefieldLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MapSelection.SelectedSceneName) return;

        SceneManager.sceneLoaded -= HandleBattlefieldLoaded;
        StartCoroutine(StartRecoveredMatch(scene));
    }

    private IEnumerator StartRecoveredMatch(Scene scene)
    {
        // Let the recovered scene complete Awake/Start first.
        yield return null;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError($"MAP SELECT: '{scene.name}' loaded, but no GameManager instance was found.");
            Destroy(gameObject);
            yield break;
        }

        Debug.Log($"MAP SELECT: '{scene.name}' loaded. Starting recovered gameplay.");

        HideRecoveredFrontEnd(scene, gameManager);

        if (gameManager.playerGameplayUI != null)
        {
            gameManager.playerGameplayUI.SetActive(true);
        }

        // If the scene itself is already configured as an automated test, leave its existing
        // GameManager startup path in control rather than starting a second match.
        if (!gameManager.enableAutoTestMode && TestDashboardOverlay.CurrentMatchNumber <= 1)
        {
            Faction assignedFaction = Random.value < 0.5f ? Faction.Attacker : Faction.Defender;
            Debug.Log($"MAP SELECT: recovered match faction = {assignedFaction}.");

            if (assignedFaction == Faction.Attacker)
            {
                gameManager.SelectAttackerFaction();
            }
            else
            {
                gameManager.SelectDefenderFaction();
            }
        }

        Destroy(gameObject);
    }

    private static void HideRecoveredFrontEnd(Scene scene, GameManager gameManager)
    {
        if (gameManager.factionSelectionUI != null)
        {
            gameManager.factionSelectionUI.SetActive(false);
        }

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
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
            wordWrap = true,
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
