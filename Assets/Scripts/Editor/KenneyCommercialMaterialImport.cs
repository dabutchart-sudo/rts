using UnityEditor;
using UnityEngine;

public class KenneyCommercialMaterialImport : AssetPostprocessor
{
    const string MaterialPath = "Assets/Art/Kenney/CityKitCommercial/Materials/KenneyCommercialVariationB.mat";

    void OnPreprocessModel()
    {
        string path = assetPath.Replace("\\", "/");
        if (!path.Contains("Assets/Art/Kenney/CityKitCommercial/Models/")) return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.globalScale = 1f;
        importer.useFileScale = true;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) return;

        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "colormap"), material);
    }
}
