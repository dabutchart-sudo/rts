using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class MapRecipeWindow : EditorWindow
{
    MapRecipe recipe;

    [MenuItem("RTS/Maps/Open Chapter 1 Market Preview", false, 0)]
    public static void OpenChapter1MarketPreview()
    {
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MapRecipeStamper.PreviewScenePath);
        if (scene == null)
        {
            EditorUtility.DisplayDialog(
                "Market preview is not in this project",
                "Chapter1MarketPreview is not on the branch Unity has open. Switch the rts project to dabutchart/dab-148-ch11-recipe-window-and-first-market-district, then try this menu again.",
                "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(MapRecipeStamper.PreviewScenePath, OpenSceneMode.Single);
    }

    [MenuItem("RTS/Maps/Recipe Window", false, 1)]
    public static void Open()
    {
        GetWindow<MapRecipeWindow>("Map Recipe");
    }

    [MenuItem("RTS/Maps/Stamp Chapter 1 Market Preview", false, 2)]
    public static void StampChapter1MarketPreview()
    {
        MapRecipeStamper.StampChapter1Preview();
    }

    void OnEnable()
    {
        if (recipe == null)
        {
            recipe = AssetDatabase.LoadAssetAtPath<MapRecipe>(MapRecipeStamper.Chapter1RecipePath);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Map recipe", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "A recipe lists sectors, how many courtyard flags each sector gets, and which districts to place. Stamping builds a GeneratedMap in the open scene and replaces only that object. SampleScene stays the original map unless you deliberately stamp into it.",
            MessageType.Info);

        recipe = (MapRecipe)EditorGUILayout.ObjectField("Recipe", recipe, typeof(MapRecipe), false);

        if (recipe == null)
        {
            EditorGUILayout.HelpBox("Assign a Map Recipe. The Chapter 1 asset lives at " + MapRecipeStamper.Chapter1RecipePath + ".", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(recipe.mapName, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(recipe.notes)) EditorGUILayout.HelpBox(recipe.notes, MessageType.None);

            EditorGUILayout.LabelField("Sectors", EditorStyles.boldLabel);
            if (recipe.sectors == null || recipe.sectors.Count == 0)
            {
                EditorGUILayout.LabelField("None yet.");
            }
            else
            {
                for (int i = 0; i < recipe.sectors.Count; i++)
                {
                    MapSectorDefinition sector = recipe.sectors[i];
                    if (sector == null) continue;
                    EditorGUILayout.LabelField(
                        (i + 1) + ". " + sector.sectorName,
                        sector.flagsPerSector + " courtyard flags, size " + sector.size);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Districts", EditorStyles.boldLabel);
            if (recipe.districts == null || recipe.districts.Count == 0)
            {
                EditorGUILayout.LabelField("None yet.");
            }
            else
            {
                foreach (MapDistrictDefinition district in recipe.districts)
                {
                    if (district == null) continue;
                    string ready = district.kind == MapDistrictKind.Market ? "ready to stamp" : "saved on the recipe, not built yet";
                    EditorGUILayout.LabelField(district.districtName + " (" + district.kind + ")", ready + ", scale " + district.visualScale);
                }
            }
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(recipe == null))
        {
            if (GUILayout.Button("Stamp into the open scene", GUILayout.Height(32f)))
            {
                MapRecipeStamper.StampIntoActiveScene(recipe, true);
            }
        }

        if (GUILayout.Button("Stamp Chapter 1 Market preview"))
        {
            MapRecipeStamper.StampChapter1Preview();
        }

        EditorGUILayout.HelpBox(
            "Chapter 1.1 builds the Market only: plaza, orange shop blocks, stall canopies, parasols, a small kiosk, and courtyard flags. Farm, Industrial, Airport, roads, and capture wiring come later.",
            MessageType.None);
    }
}
