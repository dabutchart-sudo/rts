using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime control panel for the developer-only unit sandbox.
/// Spawns the real gameplay prefabs without XP, tickets, sector rules or match setup.
/// The sandbox deliberately disables autonomous movement so weapon/class behaviour can be
/// observed repeatedly in a controlled space.
/// </summary>
public sealed class UnitSandboxController : MonoBehaviour
{
    [Header("Real Gameplay Prefabs")]
    public GameObject attackerAssaultPrefab;
    public GameObject defenderAssaultPrefab;
    public GameObject attackerEngineerPrefab;
    public GameObject defenderEngineerPrefab;
    public GameObject attackerTankPrefab;

    [Header("Spawn Layout")]
    public Vector3 attackerSpawn = new Vector3(-9f, 0f, 0f);
    public Vector3 defenderSpawn = new Vector3(9f, 0f, 0f);
    public Vector3 attackerVehicleSpawn = new Vector3(-4f, 0f, 3f);
    public Vector3 defenderVehicleSpawn = new Vector3(4f, 0f, 3f);

    [Header("Sandbox Placement")]
    [Tooltip("Small clearance above the sandbox floor after renderer-based placement.")]
    [SerializeField] private float groundClearance = 0.02f;

    [Header("Sandbox Camera")]
    [SerializeField] private float panSpeed = 12f;
    [SerializeField] private float scrollZoomSpeed = 0.035f;
    [SerializeField] private float minFieldOfView = 28f;
    [SerializeField] private float maxFieldOfView = 70f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
    private float cameraStartFieldOfView;

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraStartPosition = Camera.main.transform.position;
            cameraStartRotation = Camera.main.transform.rotation;
            cameraStartFieldOfView = Camera.main.fieldOfView;
        }
    }

    private void Update()
    {
        HandleCameraControls();
        PruneDestroyedObjects();
    }

    private void OnGUI()
    {
        EnsureStyles();

        const float width = 285f;
        GUILayout.BeginArea(new Rect(14f, 14f, width, Screen.height - 28f), GUI.skin.box);
        GUILayout.Label("UNIT SANDBOX", titleStyle);
        GUILayout.Label("Real gameplay units. No XP, tickets or match rules.", labelStyle);
        GUILayout.Space(8f);

        GUILayout.Label("ATTACKERS", labelStyle);
        if (GUILayout.Button("Spawn Assault", buttonStyle)) SpawnInfantry(attackerAssaultPrefab, "Attacker", UnitClass.Assault, attackerSpawn);
        if (GUILayout.Button("Spawn Engineer", buttonStyle)) SpawnInfantry(attackerEngineerPrefab, "Attacker", UnitClass.Engineer, attackerSpawn);
        if (GUILayout.Button("Spawn Tank", buttonStyle)) SpawnVehicle(attackerTankPrefab, "Attacker", attackerVehicleSpawn, false);
        if (GUILayout.Button("Spawn Unkillable Vehicle Target", buttonStyle)) SpawnVehicleTarget("Attacker", attackerVehicleSpawn);

        GUILayout.Space(10f);
        GUILayout.Label("DEFENDERS", labelStyle);
        if (GUILayout.Button("Spawn Assault", buttonStyle)) SpawnInfantry(defenderAssaultPrefab, "Defender", UnitClass.Assault, defenderSpawn);
        if (GUILayout.Button("Spawn Engineer", buttonStyle)) SpawnInfantry(defenderEngineerPrefab, "Defender", UnitClass.Engineer, defenderSpawn);
        if (GUILayout.Button("Spawn Tank (Sandbox Clone)", buttonStyle)) SpawnVehicle(attackerTankPrefab, "Defender", defenderVehicleSpawn, true);
        if (GUILayout.Button("Spawn Unkillable Vehicle Target", buttonStyle)) SpawnVehicleTarget("Defender", defenderVehicleSpawn);

        GUILayout.Space(10f);
        GUILayout.Label("TOOLS", labelStyle);
        if (GUILayout.Button("Spawn 4 Defender Infantry Near Vehicle", buttonStyle)) SpawnDefenderCluster();
        if (GUILayout.Button("Clear Spawned Units", buttonStyle)) ClearSpawned();
        if (GUILayout.Button("Reset Camera", buttonStyle)) ResetCamera();

        GUILayout.FlexibleSpace();
        GUILayout.Label("Camera: WASD / arrows to pan. Trackpad two-finger scroll or mouse wheel to zoom.", labelStyle);
        GUILayout.EndArea();
    }

    private void SpawnInfantry(GameObject prefab, string factionTag, UnitClass unitClass, Vector3 basePosition)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"SANDBOX: No prefab assigned for {factionTag} {unitClass}.");
            return;
        }

        Vector3 position = basePosition + RandomSpawnOffset(1.5f);
        GameObject unit = InstantiateSandboxPrefab(prefab, position, Quaternion.identity);
        unit.name = $"Sandbox_{factionTag}_{unitClass}";
        unit.tag = factionTag;

        UnitCategoryIdentity.Ensure(unit, UnitCategory.Infantry);
        UnitClassIdentity.Ensure(unit, unitClass);

        if (unitClass == UnitClass.Engineer)
        {
            EngineerUnitProfile.ApplyIfEngineer(unit);
        }

        DisableAutonomousMovement(unit);
        PlaceVisualsOnGround(unit, 0f);
        spawnedObjects.Add(unit);
    }

    private void SpawnVehicle(GameObject prefab, string factionTag, Vector3 basePosition, bool recolorForDefender)
    {
        if (prefab == null)
        {
            Debug.LogWarning("SANDBOX: Attacker tank prefab is not assigned.");
            return;
        }

        GameObject vehicle = InstantiateSandboxPrefab(prefab, basePosition + RandomSpawnOffset(1.2f), Quaternion.identity);
        vehicle.name = $"Sandbox_{factionTag}_Tank";
        vehicle.tag = factionTag;
        UnitCategoryIdentity.Ensure(vehicle, UnitCategory.Vehicle);

        DisableAutonomousMovement(vehicle);
        PlaceVisualsOnGround(vehicle, 0f);

        if (recolorForDefender)
        {
            Recolor(vehicle, FactionVisuals.DefenderColor);
        }

        spawnedObjects.Add(vehicle);
    }

    private static GameObject InstantiateSandboxPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // The gameplay prefabs contain enabled NavMeshAgents. Instantiating them enabled in a
        // scene with deliberately no NavMesh makes Unity emit an error before the sandbox can
        // disable the agent. Temporarily disable the prefab's agent for the clone operation.
        NavMeshAgent prefabAgent = prefab.GetComponent<NavMeshAgent>();
        bool restoreAgent = prefabAgent != null && prefabAgent.enabled;

        if (restoreAgent) prefabAgent.enabled = false;
        GameObject instance = Object.Instantiate(prefab, position, rotation);
        if (restoreAgent) prefabAgent.enabled = true;

        NavMeshAgent instanceAgent = instance.GetComponent<NavMeshAgent>();
        if (instanceAgent != null) instanceAgent.enabled = false;

        return instance;
    }

    private void SpawnVehicleTarget(string factionTag, Vector3 position)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = $"Sandbox_{factionTag}_UnkillableVehicleTarget";
        target.tag = factionTag;
        target.transform.position = position + RandomSpawnOffset(1f) + Vector3.up * 0.75f;
        target.transform.localScale = new Vector3(1.8f, 1.5f, 2.5f);

        UnitCategoryIdentity.Ensure(target, UnitCategory.Vehicle);
        Health health = target.AddComponent<Health>();
        health.maxHealth = 100000f;
        health.currentHealth = health.maxHealth;

        Recolor(target, factionTag == "Attacker" ? FactionVisuals.AttackerColor : FactionVisuals.DefenderColor);
        spawnedObjects.Add(target);
    }

    private void SpawnDefenderCluster()
    {
        if (defenderAssaultPrefab == null)
        {
            Debug.LogWarning("SANDBOX: Defender Assault prefab is not assigned.");
            return;
        }

        Vector3 centre = defenderVehicleSpawn;
        Vector3[] offsets =
        {
            new Vector3(-2.2f, 0f, -1.6f),
            new Vector3( 2.2f, 0f, -1.6f),
            new Vector3(-2.2f, 0f,  1.6f),
            new Vector3( 2.2f, 0f,  1.6f)
        };

        foreach (Vector3 offset in offsets)
        {
            SpawnInfantry(defenderAssaultPrefab, "Defender", UnitClass.Assault, centre + offset);
        }
    }

    private static Vector3 RandomSpawnOffset(float radius)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        return new Vector3(offset.x, 0f, offset.y);
    }

    private static void DisableAutonomousMovement(GameObject unit)
    {
        AutonomousUnit autonomous = unit.GetComponent<AutonomousUnit>();
        if (autonomous != null) autonomous.enabled = false;

        NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled) agent.enabled = false;
    }

    private void PlaceVisualsOnGround(GameObject root, float groundY)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool foundBounds = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            // Ignore world-space UI such as health bars/class labels. They should not affect
            // the physical ground placement of the unit model.
            if (renderer.GetComponentInParent<Canvas>() != null) continue;
            if (renderer is TrailRenderer || renderer is LineRenderer) continue;

            if (!foundBounds)
            {
                bounds = renderer.bounds;
                foundBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!foundBounds) return;

        float lift = (groundY + groundClearance) - bounds.min.y;
        root.transform.position += Vector3.up * lift;
    }

    private static void Recolor(GameObject root, Color color)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterial == null) continue;
            Material material = renderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }

    private void ClearSpawned()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        }

        spawnedObjects.Clear();
    }

    private void PruneDestroyedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] == null) spawnedObjects.RemoveAt(i);
        }
    }

    private void HandleCameraControls()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        Vector2 movement = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
        }

        if (movement.sqrMagnitude > 1f) movement.Normalize();

        Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        camera.transform.position += (right * movement.x + forward * movement.y) * (panSpeed * Time.unscaledDeltaTime);

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                camera.fieldOfView = Mathf.Clamp(
                    camera.fieldOfView - scroll * scrollZoomSpeed,
                    minFieldOfView,
                    maxFieldOfView);
            }
        }
    }

    private void ResetCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        camera.transform.position = cameraStartPosition;
        camera.transform.rotation = cameraStartRotation;
        camera.fieldOfView = cameraStartFieldOfView;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = new Color(0.85f, 0.88f, 0.9f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            wordWrap = true,
            fixedHeight = 32f
        };
    }
}
