#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prevents Unity's rather accusatory "No cameras rendering" message while the Bootstrap
/// selector scene is open in Edit mode. The camera is editor-only, unsaved and destroyed
/// before Play mode, so it cannot affect the actual game.
/// </summary>
[InitializeOnLoad]
public static class BootstrapGameViewPreview
{
    private const string PreviewObjectName = "Bootstrap GameView Preview Camera";
    private static GameObject previewObject;

    static BootstrapGameViewPreview()
    {
        EditorApplication.update += UpdatePreview;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            DestroyPreview();
        }
    }

    private static void UpdatePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DestroyPreview();
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        bool shouldExist = scene.IsValid() && scene.name == MapSelection.BootstrapScene;

        if (!shouldExist)
        {
            DestroyPreview();
            return;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject == previewObject) continue;
            if (camera.gameObject.scene == scene && camera.enabled)
            {
                DestroyPreview();
                return;
            }
        }

        if (previewObject != null) return;

        previewObject = new GameObject(PreviewObjectName);
        previewObject.hideFlags = HideFlags.HideAndDontSave;

        Camera previewCamera = previewObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.035f, 0.04f, 0.05f, 1f);
        previewCamera.cullingMask = 0;
        previewCamera.depth = -100f;
        previewCamera.enabled = true;
    }

    private static void DestroyPreview()
    {
        if (previewObject == null) return;
        Object.DestroyImmediate(previewObject);
        previewObject = null;
    }
}
#endif
