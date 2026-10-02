using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapRecipeStamper
{
    public const string GeneratedRootName = "GeneratedMap";
    public const string Chapter1RecipePath = "Assets/Data/MapRecipes/Chapter1Market.asset";
    public const string AirportMarketRecipePath = "Assets/Data/MapRecipes/Chapter1AirportMarket.asset";
    public const string PreviewScenePath = "Assets/Scenes/Chapter1MarketPreview.unity";
    public const string AirportMarketPreviewScenePath = "Assets/Scenes/Chapter1AirportMarketPreview.unity";
    public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    const string PlazaMaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/KenneyPlazaPaving.mat";
    const string CounterMaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/KenneyStallCounter.mat";
    const string FlagClothMaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/KenneyFlagCloth.mat";
    const string FlagPoleMaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/KenneyFlagPole.mat";
    const string GroundMaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/PreviewGround.mat";

    public static void StampChapter1Preview()
    {
        OpenAndStamp(Chapter1RecipePath, PreviewScenePath);
    }

    public static void StampAirportMarketPreview()
    {
        OpenAndStamp(AirportMarketRecipePath, AirportMarketPreviewScenePath);
    }

    static void OpenAndStamp(string recipePath, string scenePath)
    {
        MapRecipe recipe = AssetDatabase.LoadAssetAtPath<MapRecipe>(recipePath);
        if (recipe == null)
        {
            Debug.LogError("Could not find the recipe at " + recipePath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene fresh = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(fresh, scenePath);
        }
        else if (Normalize(SceneManager.GetActiveScene().path) != scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        StampIntoActiveScene(recipe, false);
    }

    public static void StampIntoActiveScene(MapRecipe recipe, bool protectSampleScene)
    {
        if (recipe == null)
        {
            Debug.LogError("Choose a Map Recipe before stamping.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (protectSampleScene && Normalize(scene.path) == SampleScenePath)
        {
            bool airportMarket = Normalize(AssetDatabase.GetAssetPath(recipe)) == AirportMarketRecipePath;
            bool openPreview = EditorUtility.DisplayDialog(
                "SampleScene stays the original map",
                airportMarket
                    ? "This stamp would drop a GeneratedMap into SampleScene. The original map should stay as it is. Open the Airport and Market preview and stamp there instead?"
                    : "This stamp would drop a GeneratedMap into SampleScene. The original map should stay as it is. Open the Chapter 1 Market preview and stamp there instead?",
                "Open the preview",
                "Cancel");
            if (openPreview)
            {
                if (airportMarket) StampAirportMarketPreview();
                else StampChapter1Preview();
            }
            return;
        }

        if (colorMaterials != null) colorMaterials.Clear();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Stamp map recipe");

        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject root = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Stamp map recipe");
        CreatePreviewGround(root.transform);

        var sectorObjects = new List<GameObject>();
        if (recipe.sectors != null)
        {
            foreach (MapSectorDefinition sector in recipe.sectors)
            {
                if (sector == null) continue;
                GameObject sectorObject = new GameObject("Sector_" + SafeName(sector.sectorName));
                Undo.RegisterCreatedObjectUndo(sectorObject, "Stamp map recipe");
                sectorObject.transform.SetParent(root.transform, false);
                sectorObject.transform.localPosition = sector.center;
                MapSectorMarker marker = sectorObject.AddComponent<MapSectorMarker>();
                marker.sectorName = sector.sectorName;
                marker.flagsPerSector = Mathf.Max(0, sector.flagsPerSector);
                marker.size = sector.size;
                CreateSectorPad(sectorObject.transform, sector);
                sectorObjects.Add(sectorObject);
            }
        }

        int districtCount = 0;
        if (recipe.districts != null)
        {
            foreach (MapDistrictDefinition district in recipe.districts)
            {
                if (district == null) continue;
                Transform parent = root.transform;
                int flags = 0;
                if (district.sectorIndex >= 0 && district.sectorIndex < sectorObjects.Count)
                {
                    parent = sectorObjects[district.sectorIndex].transform;
                    MapSectorMarker sectorMarker = parent.GetComponent<MapSectorMarker>();
                    if (sectorMarker != null) flags = sectorMarker.flagsPerSector;
                }
                else
                {
                    Debug.LogWarning(district.districtName + " points at a sector that is not in this recipe. It was placed on the generated root.");
                }

                GameObject districtObject = new GameObject("District_" + SafeName(district.districtName));
                Undo.RegisterCreatedObjectUndo(districtObject, "Stamp map recipe");
                districtObject.transform.SetParent(parent, false);
                districtObject.transform.localPosition = district.localPosition;
                districtObject.transform.localRotation = Quaternion.Euler(0f, district.yawDegrees, 0f);

                MapDistrictMarker districtMarker = districtObject.AddComponent<MapDistrictMarker>();
                districtMarker.districtName = district.districtName;
                districtMarker.kind = district.kind;
                districtMarker.visualScale = district.visualScale;

                float visualScale = Mathf.Max(0.01f, district.visualScale);
                if (district.kind == MapDistrictKind.Market)
                {
                    BuildMarket(districtObject.transform, visualScale, flags);
                }
                else if (district.kind == MapDistrictKind.Airport)
                {
                    BuildAirport(districtObject.transform, visualScale, flags);
                }
                else
                {
                    Debug.LogWarning(district.kind + " is not built by the stamp yet. Farm and Industrial stay sector looks in the map editor.");
                }

                districtCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("Stamped '" + recipe.mapName + "' into " + scene.name + ". Sectors: " + sectorObjects.Count + ". Districts: " + districtCount + ". SampleScene was left alone.");
    }

    public static void BuildMarket(Transform parent, float visualScale, int flagCount)
    {
        foreach (MarketPiece piece in MarketDistrictLayout.Pieces)
        {
            if (piece.kind == MarketPieceKind.Flag) continue;
            CreatePiece(parent, piece, visualScale, piece.objectName);
        }

        Transform flagRoot = new GameObject("CourtyardFlags").transform;
        Undo.RegisterCreatedObjectUndo(flagRoot.gameObject, "Stamp map recipe");
        flagRoot.SetParent(parent, false);

        var slots = new List<MarketPiece>();
        foreach (MarketPiece piece in MarketDistrictLayout.Pieces)
        {
            if (piece.kind == MarketPieceKind.Flag) slots.Add(piece);
        }

        for (int i = 0; i < flagCount; i++)
        {
            MarketPiece slot = slots.Count > 0 ? slots[Mathf.Min(i, slots.Count - 1)] : default;
            if (i >= slots.Count && slots.Count > 0)
            {
                slot.localPosition += new Vector3(0.6f * (i - slots.Count + 1), 0f, 0f);
            }

            CreateFlag(flagRoot, slot, visualScale, "CourtyardFlag_" + (i + 1));
        }
    }

    public static void BuildAirport(Transform parent, float visualScale, int flagCount)
    {
        foreach (AirportDistrictLayout.Block block in AirportDistrictLayout.Blocks)
        {
            CreatePrimitive(
                parent,
                block.objectName,
                PrimitiveType.Cube,
                block.localPosition * visualScale,
                block.size * visualScale,
                Quaternion.Euler(0f, block.yawDegrees, 0f),
                ColorMaterial(block.color));
        }

        Transform flagRoot = new GameObject("CourtyardFlags").transform;
        Undo.RegisterCreatedObjectUndo(flagRoot.gameObject, "Stamp map recipe");
        flagRoot.SetParent(parent, false);

        MarketPiece[] slots = AirportDistrictLayout.FlagSlots;
        for (int i = 0; i < flagCount; i++)
        {
            MarketPiece slot = slots.Length > 0 ? slots[Mathf.Min(i, slots.Length - 1)] : default;
            if (i >= slots.Length && slots.Length > 0)
            {
                slot.localPosition += new Vector3(0.6f * (i - slots.Length + 1), 0f, 0f);
            }

            CreateFlag(flagRoot, slot, visualScale, "CourtyardFlag_" + (i + 1));
        }
    }

    static void CreateSectorPad(Transform sectorObject, MapSectorDefinition sector)
    {
        Color tint = new Color(0.55f, 0.54f, 0.48f);
        if (sector.sectorName != null && sector.sectorName.IndexOf("Airport", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            tint = new Color(0.62f, 0.63f, 0.60f);
        }
        else if (sector.sectorName != null && sector.sectorName.IndexOf("Market", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            tint = new Color(0.72f, 0.62f, 0.48f);
        }

        Vector3 size = sector.size;
        CreatePrimitive(
            sectorObject,
            "SectorGround",
            PrimitiveType.Cube,
            new Vector3(0f, 0.02f, 0f),
            new Vector3(Mathf.Max(4f, size.x), 0.04f, Mathf.Max(4f, size.z)),
            Quaternion.identity,
            ColorMaterial(tint));
    }

    static void CreatePiece(Transform parent, MarketPiece piece, float visualScale, string objectName)
    {
        switch (piece.kind)
        {
            case MarketPieceKind.Model:
                CreateModel(parent, piece, visualScale, objectName);
                break;
            case MarketPieceKind.Plaza:
                CreatePlane(parent, "Plaza", piece.localPosition * visualScale, piece.size.x * visualScale, piece.size.z * visualScale, LoadMaterial(PlazaMaterialPath));
                break;
            case MarketPieceKind.Counter:
                CreateBox(
                    parent,
                    objectName,
                    piece.localPosition * visualScale + Vector3.up * (piece.size.y * visualScale * 0.5f),
                    piece.size * visualScale,
                    Quaternion.Euler(0f, piece.yawDegrees, 0f),
                    LoadMaterial(CounterMaterialPath));
                break;
        }
    }

    static void CreateModel(Transform parent, MarketPiece piece, float visualScale, string objectName)
    {
        string path = MarketDistrictLayout.ModelFolder + "/" + piece.modelFileName + ".fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Missing Kenney model at " + path);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Stamp map recipe");
        instance.name = objectName;
        instance.transform.localPosition = piece.localPosition * visualScale;
        instance.transform.localRotation = Quaternion.Euler(0f, piece.yawDegrees, 0f);
        instance.transform.localScale = Vector3.one * visualScale;
    }

    static void CreateFlag(Transform parent, MarketPiece piece, float visualScale, string objectName)
    {
        GameObject flag = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(flag, "Stamp map recipe");
        flag.transform.SetParent(parent, false);
        flag.transform.localPosition = piece.localPosition * visualScale;
        flag.transform.localRotation = Quaternion.Euler(0f, piece.yawDegrees, 0f);

        float poleHeight = piece.size.y * visualScale;
        float poleRadius = 0.015f * visualScale;
        CreatePrimitive(
            flag.transform,
            "Pole",
            PrimitiveType.Cylinder,
            new Vector3(0f, poleHeight * 0.5f, 0f),
            new Vector3(poleRadius * 2f, poleHeight * 0.5f, poleRadius * 2f),
            Quaternion.identity,
            LoadMaterial(FlagPoleMaterialPath));

        Vector3 clothSize = new Vector3(piece.size.x * visualScale, piece.size.z * visualScale, 0.03f * visualScale);
        CreateBox(
            flag.transform,
            "Cloth",
            new Vector3(clothSize.x * 0.5f, poleHeight - clothSize.y * 0.5f, 0f),
            clothSize,
            Quaternion.identity,
            LoadMaterial(FlagClothMaterialPath));
    }

    static void CreatePreviewGround(Transform parent)
    {
        CreatePlane(parent, "PreviewGround", new Vector3(0f, -0.02f, 0f), 160f, 160f, LoadMaterial(GroundMaterialPath));
    }

    static void CreatePlane(Transform parent, string objectName, Vector3 localPosition, float width, float depth, Material material)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Undo.RegisterCreatedObjectUndo(plane, "Stamp map recipe");
        plane.name = objectName;
        plane.transform.SetParent(parent, false);
        plane.transform.localPosition = localPosition;
        plane.transform.localRotation = Quaternion.identity;
        plane.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
        ApplyMaterial(plane, material);
    }

    static void CreateBox(Transform parent, string objectName, Vector3 localPosition, Vector3 size, Quaternion rotation, Material material)
    {
        CreatePrimitive(parent, objectName, PrimitiveType.Cube, localPosition, size, rotation, material);
    }

    static void CreatePrimitive(Transform parent, string objectName, PrimitiveType type, Vector3 localPosition, Vector3 size, Quaternion rotation, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(primitive, "Stamp map recipe");
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = rotation;
        primitive.transform.localScale = size;
        ApplyMaterial(primitive, material);
    }

    static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null) return;
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    static Material ColorMaterial(Color color)
    {
        if (colorMaterials == null) colorMaterials = new Dictionary<Color, Material>();
        Material existing;
        if (colorMaterials.TryGetValue(color, out existing) && existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        colorMaterials[color] = material;
        return material;
    }

    static Dictionary<Color, Material> colorMaterials;

    static Material LoadMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) Debug.LogError("Missing material at " + path);
        return material;
    }

    static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Unnamed";
        return value.Replace(" ", "");
    }

    static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) ? "" : path.Replace("\\", "/");
    }
}
