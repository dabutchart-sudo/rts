using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UnitSandboxBuilder
{
    private const string SandboxScenePath = "Assets/Scenes/UnitSandbox.unity";

    private const string AttackerAssaultPath = "Assets/Prefabs/Units/Assault_Attacker.prefab";
    private const string DefenderAssaultPath = "Assets/Prefabs/Units/Assault_Defender.prefab";
    private const string AttackerEngineerPath = "Assets/Prefabs/Units/Engineer_Attacker.prefab";
    private const string DefenderEngineerPath = "Assets/Prefabs/Units/Engineer_Defender.prefab";
    private const string AttackerTankPath = "Assets/Prefabs/Tank_Attacker.prefab";

    [MenuItem("RTS/Testing/Build or Refresh Unit Sandbox")]
    public static void BuildSandbox()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject attackerAssault = LoadRequiredPrefab(AttackerAssaultPath);
        GameObject defenderAssault = LoadRequiredPrefab(DefenderAssaultPath);
        GameObject attackerEngineer = LoadRequiredPrefab(AttackerEngineerPath);
        GameObject defenderEngineer = LoadRequiredPrefab(DefenderEngineerPath);
        GameObject attackerTank = LoadRequiredPrefab(AttackerTankPath);

        if (attackerAssault == null || defenderAssault == null || attackerEngineer == null || defenderEngineer == null || attackerTank == null)
        {
            Debug.LogError("SANDBOX BUILD: One or more required gameplay prefabs could not be loaded. No scene was created.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateGround();
        CreateSpawnPads();
        CreateCamera();
        CreateLighting();

        GameObject controllerObject = new GameObject("Unit Sandbox Controller");
        UnitSandboxController controller = controllerObject.AddComponent<UnitSandboxController>();
        controller.attackerAssaultPrefab = attackerAssault;
        controller.defenderAssaultPrefab = defenderAssault;
        controller.attackerEngineerPrefab = attackerEngineer;
        controller.defenderEngineerPrefab = defenderEngineer;
        controller.attackerTankPrefab = attackerTank;

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, SandboxScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!saved)
        {
            Debug.LogError($"SANDBOX BUILD: Unity could not save '{SandboxScenePath}'.");
            return;
        }

        Selection.activeGameObject = controllerObject;
        Debug.Log("SANDBOX BUILD: UnitSandbox.unity created/refreshed. Press Play and use the left-side control panel to spawn test units.");
    }

    [MenuItem("RTS/Testing/Open Unit Sandbox")]
    public static void OpenSandbox()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScenePath);
        if (sceneAsset == null)
        {
            bool build = EditorUtility.DisplayDialog(
                "Unit Sandbox",
                "UnitSandbox.unity does not exist yet. Build it now?",
                "Build Sandbox",
                "Cancel");

            if (build) BuildSandbox();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
    }

    private static GameObject LoadRequiredPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogError($"SANDBOX BUILD: Required prefab not found at '{path}'.");
        return prefab;
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Sandbox Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(34f, 1f, 24f);

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                Material material = new Material(shader) { name = "Sandbox Ground Material" };
                SetMaterialColor(material, new Color(0.22f, 0.30f, 0.18f, 1f));
                renderer.sharedMaterial = material;
            }
        }
    }

    private static void CreateSpawnPads()
    {
        CreatePad("Attacker Spawn Pad", new Vector3(-9f, 0.03f, 0f), FactionVisuals.AttackerColor);
        CreatePad("Defender Spawn Pad", new Vector3(9f, 0.03f, 0f), FactionVisuals.DefenderColor);
        CreatePad("Attacker Vehicle Pad", new Vector3(-4f, 0.035f, 3f), new Color(0.55f, 0.12f, 0.12f, 1f));
        CreatePad("Defender Vehicle Pad", new Vector3(4f, 0.035f, 3f), new Color(0.10f, 0.30f, 0.58f, 1f));
    }

    private static void CreatePad(string name, Vector3 position, Color color)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = name;
        pad.transform.position = position;
        pad.transform.localScale = new Vector3(2.4f, 0.025f, 2.4f);

        Collider collider = pad.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        Renderer renderer = pad.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        Material material = new Material(shader) { name = name + " Material" };
        SetMaterialColor(material, color);
        renderer.sharedMaterial = material;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 19f, -19f);
        camera.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
        camera.fieldOfView = 45f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f, 1f);
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f, 1f);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }
}
