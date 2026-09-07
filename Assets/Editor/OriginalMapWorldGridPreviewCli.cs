using System;
using System.Collections.Generic;
using Unity.Pipeline.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary play-mode-only preview for world-consistent Kenney grid UVs on the Original Map.
///
/// This does not save or modify project assets. It clones each matching mesh at runtime,
/// rewrites the clone UVs using object scale so grid cells remain approximately the same
/// physical size, and forces per-renderer BaseMap tiling to 1x1 via a MaterialPropertyBlock.
/// Stop Play Mode to discard the preview.
/// </summary>
public static class OriginalMapWorldGridPreviewCli
{
    private const string RequiredSceneName = "RecoveredDevelopmentMap";
    private const string MaterialPrefix = "OriginalMap_Kenney_";
    private const float GridCellWorldSize = 2.5f;

    [CliCommand(
        "rts_preview_world_grid",
        "Preview world-consistent Kenney grid UVs on the Original Map. Play Mode only; stop Play Mode to discard.",
        MainThreadRequired = true)]
    public static string PreviewWorldGrid()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.name != RequiredSceneName)
            return "ABORTED: active scene is '" + scene.name + "'. Expected '" + RequiredSceneName + "'.";

        if (!Application.isPlaying)
            return "ABORTED: enter Play Mode first. This preview is intentionally runtime-only so no scene or asset data is saved.";

        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        int changedRenderers = 0;
        int changedMeshes = 0;
        var changedNames = new List<string>();

        foreach (Renderer renderer in renderers)
        {
            if (!UsesOriginalMapKenneyMaterial(renderer))
                continue;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            Mesh source = meshFilter.sharedMesh;
            Mesh clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name + "_WorldGridPreview";

            if (!TryApplyWorldScaleUv(clone, meshFilter.transform))
            {
                UnityEngine.Object.Destroy(clone);
                continue;
            }

            meshFilter.sharedMesh = clone;
            ForceRendererBaseMapTilingOne(renderer);

            changedRenderers++;
            changedMeshes++;
            changedNames.Add(GetHierarchyPath(renderer.transform));
        }

        if (changedRenderers == 0)
            return "No OriginalMap_Kenney_* MeshRenderers were changed.";

        return
            "RTS WORLD GRID PREVIEW\n" +
            "Scene: " + scene.name + "\n" +
            "Grid cell world size: " + GridCellWorldSize.ToString("0.00") + " units\n" +
            "Renderers changed: " + changedRenderers + "\n" +
            "Runtime mesh clones: " + changedMeshes + "\n" +
            "Stop Play Mode to discard this preview.\n" +
            string.Join("\n", changedNames);
    }

    private static bool UsesOriginalMapKenneyMaterial(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null &&
                material.name.StartsWith(MaterialPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyWorldScaleUv(Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        if (vertices == null || vertices.Length == 0)
            return false;

        if (normals == null || normals.Length != vertices.Length)
            return false;

        Vector3 scale = transform.lossyScale;
        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);

        var uv = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i];
            Vector3 n = normals[i];

            float ax = Mathf.Abs(n.x);
            float ay = Mathf.Abs(n.y);
            float az = Mathf.Abs(n.z);

            if (ay >= ax && ay >= az)
            {
                // Top/bottom face: X/Z world-scale projection.
                uv[i] = new Vector2(
                    p.x * scale.x / GridCellWorldSize,
                    p.z * scale.z / GridCellWorldSize);
            }
            else if (ax >= ay && ax >= az)
            {
                // Left/right face: Z/Y world-scale projection.
                uv[i] = new Vector2(
                    p.z * scale.z / GridCellWorldSize,
                    p.y * scale.y / GridCellWorldSize);
            }
            else
            {
                // Front/back face: X/Y world-scale projection.
                uv[i] = new Vector2(
                    p.x * scale.x / GridCellWorldSize,
                    p.y * scale.y / GridCellWorldSize);
            }
        }

        mesh.uv = uv;
        return true;
    }

    private static void ForceRendererBaseMapTilingOne(Renderer renderer)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
        renderer.SetPropertyBlock(block);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
