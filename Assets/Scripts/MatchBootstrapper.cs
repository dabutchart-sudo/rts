using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OpenMapWorkshop()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name != "SampleScene")
        {
            if (Object.FindAnyObjectByType<SceneMenuReturn>() == null)
            {
                GameObject returnObject = new GameObject("Scene Menu Return");
                returnObject.AddComponent<SceneMenuReturn>();
            }

            return;
        }

        MapWorkshop[] workshops = Object.FindObjectsByType<MapWorkshop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < workshops.Length; i++)
        {
            MapWorkshop workshop = workshops[i];
            if (workshop != null && workshop.isActiveAndEnabled && workshop.gameObject.scene == active) return;
        }

        GameObject workshopObject = new GameObject("MapWorkshop");
        workshopObject.AddComponent<MapWorkshop>();
    }
}

public class SceneMenuReturn : MonoBehaviour
{
    void Start()
    {
        GameObject canvasObject = new GameObject("Return Canvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("Main menu");
        buttonObject.transform.SetParent(canvasObject.transform, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ReturnToMainMenu);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(220f, 44f);
        rect.anchoredPosition = new Vector2(0f, 16f);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "Main menu";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 18;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        MapSession.phase = MapSession.Phase.Menu;
        SceneManager.LoadScene("SampleScene");
    }
}
