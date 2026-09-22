using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OpenMapWorkshop()
    {
        Scene active = SceneManager.GetActiveScene();
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
