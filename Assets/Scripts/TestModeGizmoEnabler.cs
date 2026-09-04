using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only helper that attempts to switch Gizmos on automatically when automated
/// test mode starts. It uses only reflection for the internal GameView API so player
/// builds remain unaffected if Unity changes the editor implementation.
/// </summary>
public sealed class TestModeGizmoEnabler : MonoBehaviour
{
    private bool attemptedEnable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<TestModeGizmoEnabler>(FindObjectsInactive.Include) != null) return;

        GameObject host = new GameObject("TestModeGizmoEnabler");
        DontDestroyOnLoad(host);
        host.AddComponent<TestModeGizmoEnabler>();
    }

    private void Update()
    {
        if (attemptedEnable) return;
        if (GameManager.Instance == null || !GameManager.Instance.enableAutoTestMode) return;

        attemptedEnable = true;

#if UNITY_EDITOR
        EnableEditorGizmos();
#endif
    }

#if UNITY_EDITOR
    private static void EnableEditorGizmos()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.drawGizmos = true;
            SceneView.lastActiveSceneView.Repaint();
        }

        bool gameViewEnabled = false;
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

        foreach (EditorWindow window in windows)
        {
            if (window == null || window.GetType().Name != "GameView") continue;

            System.Type type = window.GetType();
            gameViewEnabled = TrySetBoolProperty(type, window, "drawGizmos", true) ||
                              TrySetBoolProperty(type, window, "showGizmos", true) ||
                              TrySetBoolProperty(type, window, "gizmos", true) ||
                              TrySetBoolField(type, window, "m_Gizmos", true) ||
                              TrySetBoolField(type, window, "m_ShowGizmos", true);

            window.Repaint();
            if (gameViewEnabled) break;
        }

        Debug.Log(gameViewEnabled
            ? "🧪 TEST MODE: Game-view Gizmos enabled automatically."
            : "🧪 TEST MODE: Scene-view Gizmos enabled. Game-view Gizmos could not be toggled automatically on this Unity version.");
    }

    private static bool TrySetBoolProperty(System.Type type, object target, string propertyName, bool value)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite) return false;

        try
        {
            property.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetBoolField(System.Type type, object target, string fieldName, bool value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(bool)) return false;

        try
        {
            field.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }
#endif
}
