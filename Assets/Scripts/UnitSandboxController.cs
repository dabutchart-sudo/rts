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
    private enum SandboxUnitChoice
    {
        Assault,
        Engineer,
        Recon,
        Support,
        Tank
    }

    private enum DestructibilityChoice
    {
        Destructible,
        Invulnerable
    }

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

    [Header("1v1 Duel Area")]
    public Vector3 duelAttackerSpawn = new Vector3(-5f, 0f, -6f);
    public Vector3 duelDefenderSpawn = new Vector3(5f, 0f, -6f);

    [Header("Sandbox Placement")]
    [Tooltip("Small clearance above the sandbox floor after renderer-based placement.")]
    [SerializeField] private float groundClearance = 0.02f;

    [Header("Sandbox Camera")]
    [SerializeField] private float panSpeed = 12f;
    [SerializeField] private float scrollZoomSpeed = 0.035f;
    [SerializeField] private float minFieldOfView = 28f;
    [SerializeField] private float maxFieldOfView = 70f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly List<GameObject> duelObjects = new List<GameObject>();
    private readonly string[] duelChoiceNames = { "Assault", "Engineer", "Recon", "Support", "Tank" };
    private readonly string[] destructibilityNames = { "Destructible", "Invulnerable" };

    private SandboxUnitChoice duelAttackerChoice = SandboxUnitChoice.Assault;
    private SandboxUnitChoice duelDefenderChoice = SandboxUnitChoice.Assault;
    private DestructibilityChoice duelAttackerDestructibility = DestructibilityChoice.Destructible;
    private DestructibilityChoice duelDefenderDestructibility = DestructibilityChoice.Destructible;

    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle selectedChoiceStyle;
    private GUIStyle unselectedChoiceStyle;
    private Vector2 panelScroll;
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

        const float width = 320f;
        GUILayout.BeginArea(new Rect(14f, 14f, width, Screen.height - 28f), GUI.skin.box);
        panelScroll = GUILayout.BeginScrollView(panelScroll, false, true);

        GUILayout.Label("UNIT SANDBOX", titleStyle);
        GUILayout.Label("Real gameplay units. No XP, tickets or match rules.", labelStyle);
        GUILayout.Space(8f);

        DrawDuelPanel();

        GUILayout.Space(14f);
        GUILayout.Label("FREE SPAWN", sectionStyle);
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
        GUILayout.Label("GROUP TOOLS", sectionStyle);
        if (GUILayout.Button("Spawn 4 Defender Infantry Near Vehicle", buttonStyle)) SpawnDefenderCluster();
        if (GUILayout.Button("Clear Free-Spawn Units", buttonStyle)) ClearFreeSpawned();
        if (GUILayout.Button("Clear Everything", buttonStyle)) ClearEverything();
        if (GUILayout.Button("Reset Camera", buttonStyle)) ResetCamera();

        GUILayout.Space(12f);
        GUILayout.Label("Camera: WASD / arrows to pan. Trackpad two-finger scroll or mouse wheel to zoom.", labelStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawDuelPanel()
    {
        GUILayout.Label("1 v 1 DUEL", sectionStyle);
        GUILayout.Label("Choose one unit per side and explicitly choose whether each can be destroyed.", labelStyle);
        GUILayout.Space(8f);

        GUILayout.Label("ATTACKER UNIT", labelStyle);
        duelAttackerChoice = DrawUnitChoice(duelAttackerChoice);
        GUILayout.Label("ATTACKER DAMAGE STATE", labelStyle);
        duelAttackerDestructibility = DrawDestructibilityChoice(duelAttackerDestructibility);

        GUILayout.Space(10f);
        GUILayout.Label("DEFENDER UNIT", labelStyle);
        duelDefenderChoice = DrawUnitChoice(duelDefenderChoice);
        GUILayout.Label("DEFENDER DAMAGE STATE", labelStyle);
        duelDefenderDestructibility = DrawDestructibilityChoice(duelDefenderDestructibility);

        GUILayout.Space(10f);
        if (GUILayout.Button("START / RESET 1 v 1", buttonStyle)) StartDuel();
        if (GUILayout.Button("Clear 1 v 1", buttonStyle)) ClearDuel();
    }

    private SandboxUnitChoice DrawUnitChoice(SandboxUnitChoice current)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < duelChoiceNames.Length; i++)
        {
            GUIStyle style = i == (int)current ? selectedChoiceStyle : unselectedChoiceStyle;
            if (GUILayout.Button(duelChoiceNames[i], style)) current = (SandboxUnitChoice)i;
        }
        GUILayout.EndHorizontal();
        return current;
    }

    private DestructibilityChoice DrawDestructibilityChoice(DestructibilityChoice current)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < destructibilityNames.Length; i++)
        {
            GUIStyle style = i == (int)current ? selectedChoiceStyle : unselectedChoiceStyle;
            if (GUILayout.Button(destructibilityNames[i], style)) current = (DestructibilityChoice)i;
        }
        GUILayout.EndHorizontal();
        return current;
    }

    private void StartDuel()
    {
        ClearDuel();

        bool attackerDestructible = duelAttackerDestructibility == DestructibilityChoice.Destructible;
        bool defenderDestructible = duelDefenderDestructibility == DestructibilityChoice.Destructible;

        GameObject attacker = SpawnDuelUnit(duelAttackerChoice, "Attacker", duelAttackerSpawn, attackerDestructible);
        GameObject defender = SpawnDuelUnit(duelDefenderChoice, "Defender", duelDefenderSpawn, defenderDestructible);

        if (attacker != null) FaceTowards(attacker, duelDefenderSpawn);
        if (defender != null) FaceTowards(defender, duelAttackerSpawn);
    }

    private GameObject SpawnDuelUnit(SandboxUnitChoice choice, string factionTag, Vector3 position, bool destructible)
    {
        bool attacker = factionTag == "Attacker";
        GameObject unit;

        switch (choice)
        {
            case SandboxUnitChoice.Engineer:
                unit = CreateInfantry(attacker ? attackerEngineerPrefab : defenderEngineerPrefab, factionTag, UnitClass.Engineer, position, false);
                break;

            case SandboxUnitChoice.Recon:
                unit = CreateInfantry(attacker ? attackerAssaultPrefab : defenderAssaultPrefab, factionTag, UnitClass.Recon, position, false);
                if (unit != null) ReconUnitProfile.ApplyIfRecon(unit);
                break;

            case SandboxUnitChoice.Support:
                unit = CreateInfantry(attacker ? attackerAssaultPrefab : defenderAssaultPrefab, factionTag, UnitClass.Support, position, false);
                if (unit != null) SupportUnitProfile.ApplyIfSupport(unit);
                break;

            case SandboxUnitChoice.Tank:
                unit = CreateVehicle(attackerTankPrefab, factionTag, position, !attacker, false);
                break;

            default:
                unit = CreateInfantry(attacker ? attackerAssaultPrefab : defenderAssaultPrefab, factionTag, UnitClass.Assault, position, false);
                break;
        }

        if (unit == null) return null;

        unit.name = $"Sandbox_Duel_{factionTag}_{choice}";
        SetDestructible(unit, destructible);
        duelObjects.Add(unit);
        return unit;
    }

    private static void SetDestructible(GameObject unit, bool destructible)
    {
        Health health = unit != null ? unit.GetComponent<Health>() : null;
        if (health == null) return;

        if (!destructible)
        {
            const float sandboxInvulnerableHealth = 1000000f;
            health.maxHealth = sandboxInvulnerableHealth;
            health.currentHealth = sandboxInvulnerableHealth;
            health.UpdateHealthBar();
        }
    }

    private void SpawnInfantry(GameObject prefab, string factionTag, UnitClass unitClass, Vector3 basePosition)
    {
        CreateInfantry(prefab, factionTag, unitClass, basePosition + RandomSpawnOffset(1.5f), true);
    }

    private GameObject CreateInfantry(GameObject prefab, string factionTag, UnitClass unitClass, Vector3 position, bool trackAsFreeSpawn)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"SANDBOX: No prefab assigned for {factionTag} {unitClass}.");
            return null;
        }

        GameObject unit = InstantiateSandboxPrefab(prefab, position, Quaternion.identity);
        unit.name = $"Sandbox_{factionTag}_{unitClass}";
        unit.tag = factionTag;

        UnitCategoryIdentity.Ensure(unit, UnitCategory.Infantry);
        UnitClassIdentity.Ensure(unit, unitClass);

        if (unitClass == UnitClass.Engineer) EngineerUnitProfile.ApplyIfEngineer(unit);
        if (unitClass == UnitClass.Recon) ReconUnitProfile.ApplyIfRecon(unit);
        if (unitClass == UnitClass.Support) SupportUnitProfile.ApplyIfSupport(unit);

        DisableAutonomousMovement(unit);
        PlaceVisualsOnGround(unit, 0f);

        if (trackAsFreeSpawn) spawnedObjects.Add(unit);
        return unit;
    }

    private void SpawnVehicle(GameObject prefab, string factionTag, Vector3 basePosition, bool recolorForDefender)
    {
        CreateVehicle(prefab, factionTag, basePosition + RandomSpawnOffset(1.2f), recolorForDefender, true);
    }

    private GameObject CreateVehicle(GameObject prefab, string factionTag, Vector3 position, bool recolorForDefender, bool trackAsFreeSpawn)
    {
        if (prefab == null)
        {
            Debug.LogWarning("SANDBOX: Attacker tank prefab is not assigned.");
            return null;
        }

        GameObject vehicle = InstantiateSandboxPrefab(prefab, position, Quaternion.identity);
        vehicle.name = $"Sandbox_{factionTag}_Tank";
        vehicle.tag = factionTag;
        UnitCategoryIdentity.Ensure(vehicle, UnitCategory.Vehicle);

        Combat combat = vehicle.GetComponent<Combat>();
        if (combat != null) combat.enemyTag = factionTag == "Attacker" ? "Defender" : "Attacker";

        DisableAutonomousMovement(vehicle);
        PlaceVisualsOnGround(vehicle, 0f);

        if (recolorForDefender) Recolor(vehicle, FactionVisuals.DefenderColor);
        if (trackAsFreeSpawn) spawnedObjects.Add(vehicle);
        return vehicle;
    }

    private static GameObject InstantiateSandboxPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
    {
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
            new Vector3(2.2f, 0f, -1.6f),
            new Vector3(-2.2f, 0f, 1.6f),
            new Vector3(2.2f, 0f, 1.6f)
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

    private static void FaceTowards(GameObject unit, Vector3 targetPosition)
    {
        if (unit == null) return;

        Vector3 direction = targetPosition - unit.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f) unit.transform.rotation = Quaternion.LookRotation(direction.normalized);
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

    private void ClearDuel()
    {
        for (int i = duelObjects.Count - 1; i >= 0; i--)
        {
            if (duelObjects[i] != null) Destroy(duelObjects[i]);
        }

        duelObjects.Clear();
    }

    private void ClearFreeSpawned()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        }

        spawnedObjects.Clear();
    }

    private void ClearEverything()
    {
        ClearDuel();
        ClearFreeSpawned();
    }

    private void PruneDestroyedObjects()
    {
        PruneList(spawnedObjects);
        PruneList(duelObjects);
    }

    private static void PruneList(List<GameObject> objects)
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] == null) objects.RemoveAt(i);
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
                camera.fieldOfView = Mathf.Clamp(camera.fieldOfView - scroll * scrollZoomSpeed, minFieldOfView, maxFieldOfView);
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

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
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

        unselectedChoiceStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            wordWrap = true,
            fixedHeight = 30f
        };

        selectedChoiceStyle = new GUIStyle(unselectedChoiceStyle);
        selectedChoiceStyle.fontStyle = FontStyle.Bold;
        selectedChoiceStyle.normal.textColor = Color.white;
        selectedChoiceStyle.hover.textColor = Color.white;
        selectedChoiceStyle.active.textColor = Color.white;
        selectedChoiceStyle.normal.background = GUI.skin.button.active.background;
        selectedChoiceStyle.hover.background = GUI.skin.button.active.background;
        selectedChoiceStyle.active.background = GUI.skin.button.active.background;
    }
}
