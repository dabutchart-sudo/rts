using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only project-specific Unity CLI diagnostics for the RTS project.
///
/// These commands intentionally expose compact, stable summaries instead of
/// requiring callers to evaluate ad-hoc C# or inspect scene YAML.
/// </summary>
public static class RTSCliCommands
{
    [CliCommand(
        "rts_status",
        "Return a compact read-only status summary for the active RTS scene.",
        MainThreadRequired = true)]
    public static string Status()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        Collider[] colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        int capturePoints = CountComponentsNamed(behaviours, "CapturePoint");
        int gameManagers = CountComponentsNamed(behaviours, "GameManager");
        int aiCommanders = CountComponentsNamed(behaviours, "AICommander");

        var sb = new StringBuilder();
        sb.AppendLine("RTS STATUS");
        sb.AppendLine("Scene: " + scene.name);
        sb.AppendLine("Scene loaded: " + scene.isLoaded);
        sb.AppendLine("Playing: " + Application.isPlaying);
        sb.AppendLine("Root GameObjects: " + roots.Length);
        sb.AppendLine("Renderers: " + renderers.Length);
        sb.AppendLine("Colliders: " + colliders.Length);
        sb.AppendLine("CapturePoint components: " + capturePoints);
        sb.AppendLine("GameManager components: " + gameManagers);
        sb.AppendLine("AICommander components: " + aiCommanders);

        return sb.ToString();
    }

    [CliCommand(
        "rts_scene_summary",
        "Return a read-only summary of root objects and their immediate children in the active scene.",
        MainThreadRequired = true)]
    public static string SceneSummary()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects()
            .OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("RTS SCENE SUMMARY");
        sb.AppendLine("Scene: " + scene.name);
        sb.AppendLine("Root GameObjects: " + roots.Length);
        sb.AppendLine();

        foreach (GameObject root in roots)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

            sb.Append(root.name);
            sb.Append(" | Descendants: ");
            sb.Append(Math.Max(0, transforms.Length - 1));
            sb.Append(" | Immediate children: ");
            sb.Append(root.transform.childCount);
            sb.Append(" | Renderers: ");
            sb.Append(renderers.Length);
            sb.Append(" | Colliders: ");
            sb.Append(colliders.Length);
            sb.Append(" | Active: ");
            sb.Append(root.activeSelf);
            sb.AppendLine();

            for (int i = 0; i < root.transform.childCount; i++)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                Component[] components = child.GetComponents<Component>();
                string componentNames = string.Join(", ", components
                    .Where(component => component != null)
                    .Select(component => component.GetType().Name));

                sb.Append("  - ");
                sb.Append(child.name);
                sb.Append(" | Active: ");
                sb.Append(child.activeSelf);
                sb.Append(" | Components: ");
                sb.Append(componentNames);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    [CliCommand(
        "rts_materials",
        "Return a read-only list of material assignments used by Renderers in the active scene.",
        MainThreadRequired = true)]
    public static string Materials()
    {
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        var rows = new List<string>();

        foreach (Renderer renderer in renderers.OrderBy(r => r.gameObject.name, StringComparer.OrdinalIgnoreCase))
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                string colour = GetMaterialColour(material);
                Vector2 tiling = material.HasProperty("_BaseMap")
                    ? material.GetTextureScale("_BaseMap")
                    : Vector2.one;

                rows.Add(
                    renderer.gameObject.name +
                    " | Slot: " + i +
                    " | Material: " + material.name +
                    " | Shader: " + (material.shader != null ? material.shader.name : "(none)") +
                    " | Colour: " + colour +
                    " | Tiling: " + tiling);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("RTS MATERIALS");
        sb.AppendLine("Assignments: " + rows.Count);

        foreach (string row in rows)
            sb.AppendLine(row);

        return sb.ToString();
    }

    [CliCommand(
        "rts_objectives",
        "Return a read-only list of CapturePoint objects in the active scene.",
        MainThreadRequired = true)]
    public static string Objectives()
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        MonoBehaviour[] capturePoints = behaviours
            .Where(behaviour => behaviour != null && behaviour.GetType().Name == "CapturePoint")
            .OrderBy(behaviour => behaviour.gameObject.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("RTS OBJECTIVES");
        sb.AppendLine("CapturePoints: " + capturePoints.Length);

        foreach (MonoBehaviour capturePoint in capturePoints)
        {
            Transform transform = capturePoint.transform;
            Collider collider = capturePoint.GetComponent<Collider>();

            sb.Append(capturePoint.gameObject.name);
            sb.Append(" | Active: ");
            sb.Append(capturePoint.gameObject.activeSelf);
            sb.Append(" | Enabled: ");
            sb.Append(capturePoint.enabled);
            sb.Append(" | Position: ");
            sb.Append(transform.position);
            sb.Append(" | Collider: ");
            sb.Append(collider != null ? collider.GetType().Name : "(none)");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int CountComponentsNamed(IEnumerable<MonoBehaviour> behaviours, string typeName)
    {
        return behaviours.Count(behaviour =>
            behaviour != null &&
            behaviour.GetType().Name == typeName);
    }

    private static string GetMaterialColour(Material material)
    {
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor").ToString();

        if (material.HasProperty("_Color"))
            return material.GetColor("_Color").ToString();

        return "(no colour property)";
    }
}
