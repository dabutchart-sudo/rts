using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class PlayableMapBuilder
{
    public const string RootName = "GeneratedMap";

    static readonly string[] OriginalBattlefieldNames =
    {
        "Ground",
        "CaptureZone_A",
        "CaptureZone_B",
        "ObjectiveTarget",
        "TankSpawner"
    };

    static readonly Color[] SectorColors =
    {
        new Color(0.45f, 0.55f, 0.38f),
        new Color(0.40f, 0.50f, 0.58f),
        new Color(0.58f, 0.48f, 0.36f),
        new Color(0.42f, 0.54f, 0.50f),
        new Color(0.54f, 0.42f, 0.50f),
        new Color(0.50f, 0.50f, 0.40f)
    };

    static readonly List<GameObject> HiddenOriginal = new List<GameObject>();

    readonly List<BuiltSector> built = new List<BuiltSector>();
    readonly List<Transform> depthHandles = new List<Transform>();

    PlayableMapDefinition map;
    GameObject root;
    Transform ground;
    Transform handlesRoot;
    Transform widthMin;
    Transform widthMax;
    Transform decorationRoot;
    Sector[] wired;

    class BuiltSector
    {
        public Transform tint;
        public Transform label;
        public readonly List<Transform> points = new List<Transform>();
        public Transform attacker;
        public Transform defender;
        public CapturePoint[] capturePoints;
        public BaseZone attackerBase;
        public BaseZone defenderBase;
    }

    public static void ForgetHiddenOriginal()
    {
        HiddenOriginal.Clear();
    }

    public static void HideOriginalBattlefield()
    {
        RestoreOriginalBattlefield();
        foreach (string objectName in OriginalBattlefieldNames)
        {
            GameObject battlefieldObject = GameObject.Find(objectName);
            if (battlefieldObject == null) continue;

            NavMeshSurface surface = battlefieldObject.GetComponent<NavMeshSurface>();
            if (surface != null) surface.RemoveData();

            battlefieldObject.SetActive(false);
            HiddenOriginal.Add(battlefieldObject);
        }
    }

    public static void RestoreOriginalBattlefield()
    {
        for (int i = 0; i < HiddenOriginal.Count; i++)
        {
            if (HiddenOriginal[i] != null) HiddenOriginal[i].SetActive(true);
        }

        HiddenOriginal.Clear();
    }

    public void ClearGenerated()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Object.Destroy(existing);
        root = null;
        ground = null;
        handlesRoot = null;
        widthMin = null;
        widthMax = null;
        decorationRoot = null;
        wired = null;
        built.Clear();
        depthHandles.Clear();
    }

    public void Build(PlayableMapDefinition definition, bool editing)
    {
        ClearGenerated();
        map = definition;
        if (map == null) return;
        map.Normalize();

        root = new GameObject(RootName);
        ground = CreateCube("Ground", root.transform, new Color(0.24f, 0.27f, 0.22f)).transform;

        for (int i = 0; i < map.sectors.Count; i++)
        {
            PlayableSectorDefinition sector = map.sectors[i];
            var visual = new BuiltSector();
            Color tintColor = SectorColors[i % SectorColors.Length];
            visual.tint = CreateCube("Sector " + (char)('A' + i), root.transform, tintColor).transform;
            RemoveCollider(visual.tint.gameObject);
            visual.label = AddLabel(root.transform, sector.sectorName);

            visual.capturePoints = new CapturePoint[sector.controlPoints.Count];
            for (int p = 0; p < sector.controlPoints.Count; p++)
            {
                Transform point = CreateControlPoint(root.transform, sector.sectorName, i, p);
                visual.points.Add(point);
                visual.capturePoints[p] = point.GetComponent<CapturePoint>();
            }

            visual.attacker = CreateSpawn(root.transform, Faction.Attacker, i);
            visual.defender = CreateSpawn(root.transform, Faction.Defender, i);
            visual.attackerBase = visual.attacker.GetComponent<BaseZone>();
            visual.defenderBase = visual.defender.GetComponent<BaseZone>();
            built.Add(visual);
        }

        handlesRoot = new GameObject("Borders").transform;
        handlesRoot.SetParent(root.transform, false);
        for (int edge = 0; edge <= map.sectors.Count; edge++)
        {
            Transform handle = CreateBorderHandle(handlesRoot, MapEditHandle.Kind.DepthEdge, -1, edge);
            depthHandles.Add(handle);
        }

        widthMin = CreateBorderHandle(handlesRoot, MapEditHandle.Kind.WidthMin, -1, -1);
        widthMax = CreateBorderHandle(handlesRoot, MapEditHandle.Kind.WidthMax, -1, -1);

        ApplyToGameManager();
        Sync();
        SetEditing(editing);
    }

    public void Sync()
    {
        if (map == null || root == null || built.Count != map.sectors.Count) return;

        map.Normalize();
        float minZ = map.sectors[0].minZ;
        float maxZ = map.sectors[map.sectors.Count - 1].maxZ;
        float width = Mathf.Max(1f, map.maxX - map.minX);
        float depth = Mathf.Max(1f, maxZ - minZ);
        float centerX = (map.minX + map.maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        ground.position = new Vector3(centerX, -0.25f, centerZ);
        ground.localScale = new Vector3(width, 0.5f, depth);

        for (int i = 0; i < built.Count; i++)
        {
            PlayableSectorDefinition sector = map.sectors[i];
            BuiltSector visual = built[i];
            float sectorDepth = Mathf.Max(1f, sector.maxZ - sector.minZ);
            visual.tint.position = new Vector3(centerX, 0.15f, (sector.minZ + sector.maxZ) * 0.5f);
            visual.tint.localScale = new Vector3(Mathf.Max(1f, width - 1.4f), 0.08f, Mathf.Max(1f, sectorDepth - 1.4f));
            if (visual.label != null)
            {
                visual.label.position = new Vector3(centerX, 0.45f, (sector.minZ + sector.maxZ) * 0.5f);
                visual.label.rotation = Quaternion.Euler(90f, 0f, 0f);
                visual.label.localScale = Vector3.one;
            }

            for (int p = 0; p < visual.points.Count && p < sector.controlPoints.Count; p++)
            {
                visual.points[p].position = sector.controlPoints[p];
            }

            visual.attacker.position = sector.attackerSpawn;
            visual.defender.position = sector.defenderSpawn;
            if (visual.attackerBase != null) visual.attackerBase.RefreshVisual();
            if (visual.defenderBase != null) visual.defenderBase.RefreshVisual();

            if (wired != null && i < wired.Length)
            {
                wired[i].sectorName = sector.sectorName;
                wired[i].sectorBounds = map.SectorBounds(i);
            }
        }

        for (int edge = 0; edge < depthHandles.Count; edge++)
        {
            float edgeZ = edge <= 0 ? minZ : (edge >= map.sectors.Count ? maxZ : map.sectors[edge - 1].maxZ);
            Transform handle = depthHandles[edge];
            handle.position = new Vector3(centerX, 1.4f, edgeZ);
            handle.localScale = new Vector3(width, 2.2f, 2.2f);
        }

        if (widthMin != null)
        {
            widthMin.position = new Vector3(map.minX, 1.4f, centerZ);
            widthMin.localScale = new Vector3(2.2f, 2.2f, depth);
        }

        if (widthMax != null)
        {
            widthMax.position = new Vector3(map.maxX, 1.4f, centerZ);
            widthMax.localScale = new Vector3(2.2f, 2.2f, depth);
        }

        map.EnsureDecoration();
        SyncDecoration();
    }

    public void SetEditing(bool editing)
    {
        if (handlesRoot != null) handlesRoot.gameObject.SetActive(editing);

        foreach (BuiltSector visual in built)
        {
            foreach (Transform point in visual.points)
            {
                Transform grab = point.Find("Grab");
                if (grab != null) grab.gameObject.SetActive(editing);
            }

            SetGrabActive(visual.attacker, editing);
            SetGrabActive(visual.defender, editing);
        }

        if (decorationRoot != null)
        {
            for (int i = 0; i < decorationRoot.childCount; i++)
            {
                Transform piece = decorationRoot.GetChild(i);
                SetGrabActive(piece, editing);
                Transform remove = piece.Find("Remove");
                if (remove != null) remove.gameObject.SetActive(editing);
            }
        }
    }

    const float DecorationScale = 4f;

    void SyncDecoration()
    {
        if (root == null || map == null) return;
        map.EnsureDecoration();
        if (decorationRoot == null)
        {
            var decorationObject = new GameObject("Decoration");
            decorationObject.transform.SetParent(root.transform, false);
            decorationRoot = decorationObject.transform;
        }

        var alive = new HashSet<string>();
        for (int i = 0; i < map.decoration.Count; i++)
        {
            PlayableDecorationPiece piece = map.decoration[i];
            if (piece == null || string.IsNullOrEmpty(piece.pieceId)) continue;
            alive.Add(piece.pieceId);
            Transform visual = decorationRoot.Find(piece.pieceId);
            if (visual == null) visual = CreateDecorationVisual(piece);
            visual.position = piece.position;
            visual.rotation = Quaternion.Euler(0f, piece.yaw, 0f);
        }

        for (int i = decorationRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = decorationRoot.GetChild(i);
            if (!alive.Contains(child.name)) Object.Destroy(child.gameObject);
        }
    }

    Transform CreateDecorationVisual(PlayableDecorationPiece piece)
    {
        var visual = new GameObject(piece.pieceId);
        visual.transform.SetParent(decorationRoot, false);
        visual.transform.localScale = Vector3.one * DecorationScale;

        GameObject model = CreateDecorationModel(piece.catalogId);
        model.transform.SetParent(visual.transform, false);

        BoxCollider obstacle = visual.AddComponent<BoxCollider>();
        obstacle.center = new Vector3(0f, 0.25f, 0f);
        obstacle.size = new Vector3(0.9f, 0.5f, 0.9f);

        CreateDecorationHandle(visual.transform, "Grab", MapEditHandle.Kind.Decoration, piece.pieceId, new Vector3(0f, 0.7f, 0f), new Color(0.95f, 0.85f, 0.35f), 2.2f);
        CreateDecorationHandle(visual.transform, "Remove", MapEditHandle.Kind.DecorationRemove, piece.pieceId, new Vector3(0.55f, 0.85f, 0f), new Color(0.85f, 0.2f, 0.18f), 1.2f);
        return visual.transform;
    }

    void CreateDecorationHandle(Transform parent, string objectName, MapEditHandle.Kind kind, string pieceId, Vector3 localPosition, Color color, float worldSize)
    {
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = objectName;
        handle.transform.SetParent(parent, false);
        handle.transform.localPosition = localPosition;
        handle.transform.localScale = Vector3.one * (worldSize / DecorationScale);
        Paint(handle, color);
        MapEditHandle marker = handle.AddComponent<MapEditHandle>();
        marker.kind = kind;
        marker.decorationId = pieceId;
    }

    static GameObject CreateDecorationModel(string catalogId)
    {
        string modelName = catalogId switch
        {
            "parasol-a" => "detail-parasol-a",
            "parasol-b" => "detail-parasol-b",
            "awning" => "detail-awning",
            _ => "low-detail-building-n"
        };

#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            MarketDistrictLayout.ModelFolder + "/" + modelName + ".fbx");
        if (prefab != null)
        {
            GameObject model = Object.Instantiate(prefab);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            return model;
        }
#endif

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.transform.localScale = new Vector3(0.8f, 0.45f, 0.8f);
        fallback.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        Collider fallbackCollider = fallback.GetComponent<Collider>();
        if (fallbackCollider != null)
        {
            fallbackCollider.enabled = false;
            Object.Destroy(fallbackCollider);
        }

        return fallback;
    }

    public void BakeNavigation()
    {
        if (root == null || map == null) return;

        SetEditing(false);
        NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = root.AddComponent<NavMeshSurface>();

        Bounds bounds = map.WorldBounds();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.center = new Vector3(bounds.center.x, 2f, bounds.center.z);
        surface.size = new Vector3(bounds.size.x + 4f, 10f, bounds.size.z + 4f);
        surface.BuildNavMesh();
    }

    public static void FrameCamera(Bounds bounds)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float span = Mathf.Max(bounds.size.x, bounds.size.z, 20f);
        Vector3 look = bounds.center;
        look.y = 0f;
        cam.transform.position = look + new Vector3(0f, span * 0.9f, -span * 0.72f);
        cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position, Vector3.up);

        RTSCamera rtsCamera = cam.GetComponent<RTSCamera>();
        if (rtsCamera != null)
        {
            float framedHeight = cam.transform.position.y;
            rtsCamera.maxZoomHeight = Mathf.Max(80f, framedHeight * 1.35f);
            rtsCamera.minZoomHeight = Mathf.Min(rtsCamera.minZoomHeight, 8f);
        }
    }

    public static void EnsureGameplayLinks(GameManager gameManager)
    {
        if (gameManager == null) return;

        UnitSpawner attacker = FindSpawner("AttackerSpawner");
        UnitSpawner defender = FindSpawner("DefenderSpawner");

        if (attacker != null)
        {
            attacker.isDefenderSpawner = false;
            if (attacker.assaultPrefab == null)
            {
                attacker.assaultPrefab = LoadUnitPrefab("Assets/Prefabs/Units/Assault_Attacker.prefab");
            }

            gameManager.attackerSpawner = attacker;
        }

        if (defender != null)
        {
            defender.isDefenderSpawner = true;
            if (defender.assaultPrefab == null)
            {
                defender.assaultPrefab = LoadUnitPrefab("Assets/Prefabs/Units/Assault_Defender.prefab");
            }

            gameManager.defenderSpawner = defender;
        }
    }

    public static void LinkOriginal(GameManager gameManager)
    {
        if (gameManager == null) return;
        if (HasWiredSectors(gameManager)) return;

        var points = new List<CapturePoint>();
        foreach (CapturePoint point in Object.FindObjectsByType<CapturePoint>(FindObjectsInactive.Exclude))
        {
            if (point == null) continue;
            if (point.transform.root != null && point.transform.root.name == RootName) continue;
            points.Add(point);
        }

        points.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        if (points.Count == 0)
        {
            Debug.LogWarning("Original map has no capture points to run a Breakthrough match.");
            return;
        }

        Renderer groundRenderer = null;
        GameObject groundObject = GameObject.Find("Ground");
        if (groundObject != null) groundRenderer = groundObject.GetComponent<Renderer>();
        Bounds groundBounds = groundRenderer != null
            ? groundRenderer.bounds
            : new Bounds(Vector3.zero, new Vector3(50f, 10f, 160f));

        GameObject links = new GameObject("OriginalLinks");
        float slice = groundBounds.size.z / points.Count;
        var sectors = new Sector[points.Count];
        Transform attackerSpawner = FindSpawner("AttackerSpawner") != null ? FindSpawner("AttackerSpawner").transform : null;
        Transform defenderSpawner = FindSpawner("DefenderSpawner") != null ? FindSpawner("DefenderSpawner").transform : null;

        for (int i = 0; i < points.Count; i++)
        {
            float z0 = groundBounds.min.z + (slice * i);
            float z1 = z0 + slice;
            float inset = Mathf.Min(12f, slice * 0.2f);
            Vector3 attackerPos = new Vector3(groundBounds.center.x, 0.5f, z0 + inset);
            Vector3 defenderPos = new Vector3(groundBounds.center.x, 0.5f, z1 - inset);

            if (attackerSpawner != null && ContainsXZ(groundBounds, attackerSpawner.position, z0, z1))
            {
                attackerPos = attackerSpawner.position;
                attackerPos.y = 0.5f;
            }

            if (defenderSpawner != null && ContainsXZ(groundBounds, defenderSpawner.position, z0, z1))
            {
                defenderPos = defenderSpawner.position;
                defenderPos.y = 0.5f;
            }

            char letter = (char)('A' + i);
            points[i].capturePointName = "Sector " + letter;
            points[i].activeDuringSectorIndex = i;

            BaseZone attackerBase = CreateLinkedBase(links.transform, "Attacker Base " + letter, Faction.Attacker, attackerPos);
            BaseZone defenderBase = CreateLinkedBase(links.transform, "Defender Base " + letter, Faction.Defender, defenderPos);

            sectors[i] = new Sector
            {
                sectorName = "Sector " + letter,
                sectorAnnouncementText = "SECURE ALL OBJECTIVES",
                capturePoints = new[] { points[i] },
                attackerBase = attackerBase,
                defenderBase = defenderBase,
                sectorBounds = new Bounds(
                    new Vector3(groundBounds.center.x, 10f, (z0 + z1) * 0.5f),
                    new Vector3(groundBounds.size.x, 40f, Mathf.Max(1f, slice)))
            };
        }

        gameManager.UseSectors(sectors);
    }

    static bool HasWiredSectors(GameManager gameManager)
    {
        if (gameManager.sectors == null || gameManager.sectors.Length == 0) return false;
        Sector first = gameManager.sectors[0];
        return first != null && first.capturePoints != null && first.capturePoints.Length > 0 && first.capturePoints[0] != null;
    }

    static bool ContainsXZ(Bounds groundBounds, Vector3 point, float z0, float z1)
    {
        return point.x >= groundBounds.min.x && point.x <= groundBounds.max.x && point.z >= z0 && point.z <= z1;
    }

    static BaseZone CreateLinkedBase(Transform parent, string objectName, Faction faction, Vector3 position)
    {
        GameObject baseObject = new GameObject(objectName);
        baseObject.transform.SetParent(parent, false);
        baseObject.transform.position = position;
        BaseZone zone = baseObject.AddComponent<BaseZone>();
        zone.controllingFaction = faction;
        zone.spawnPoint = baseObject.transform;
        zone.baseName = objectName;
        zone.RefreshVisual();
        return zone;
    }

    void ApplyToGameManager()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null) return;

        wired = new Sector[built.Count];
        for (int i = 0; i < built.Count; i++)
        {
            wired[i] = new Sector
            {
                sectorName = map.sectors[i].sectorName,
                sectorAnnouncementText = "SECURE ALL OBJECTIVES",
                capturePoints = built[i].capturePoints,
                attackerBase = built[i].attackerBase,
                defenderBase = built[i].defenderBase,
                sectorBounds = map.SectorBounds(i)
            };
        }

        gameManager.UseSectors(wired);
    }

    Transform CreateControlPoint(Transform parent, string sectorName, int sectorIndex, int pointIndex)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        point.name = sectorName + " Point " + (pointIndex + 1);
        point.transform.SetParent(parent, false);
        point.transform.localScale = new Vector3(4f, 0.25f, 4f);
        RemoveCollider(point);

        BoxCollider trigger = point.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.2f, 16f, 2.2f);
        trigger.center = new Vector3(0f, 8f, 0f);

        CapturePoint capture = point.AddComponent<CapturePoint>();
        capture.capturePointName = sectorName + " " + (pointIndex + 1);
        capture.activeDuringSectorIndex = sectorIndex;
        Paint(point, new Color(0.85f, 0.7f, 0.25f));

        GameObject grab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grab.name = "Grab";
        grab.transform.SetParent(point.transform, false);
        grab.transform.localPosition = new Vector3(0f, 3f, 0f);
        grab.transform.localScale = new Vector3(0.45f, 6f, 0.45f);
        Paint(grab, new Color(0.95f, 0.85f, 0.4f));
        MapEditHandle handle = grab.AddComponent<MapEditHandle>();
        handle.kind = MapEditHandle.Kind.ControlPoint;
        handle.sectorIndex = sectorIndex;
        handle.pointIndex = pointIndex;
        return point.transform;
    }

    Transform CreateSpawn(Transform parent, Faction faction, int sectorIndex)
    {
        bool attacker = faction == Faction.Attacker;
        GameObject spawn = new GameObject(attacker ? "Attacker Spawn" : "Defender Spawn");
        spawn.transform.SetParent(parent, false);
        BaseZone zone = spawn.AddComponent<BaseZone>();
        zone.controllingFaction = faction;
        zone.spawnPoint = spawn.transform;
        zone.baseName = attacker ? "Attacker Spawn" : "Defender Spawn";
        zone.RefreshVisual();

        GameObject grab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grab.name = "Grab";
        grab.transform.SetParent(spawn.transform, false);
        grab.transform.localScale = new Vector3(3.2f, 3.2f, 3.2f);
        Paint(grab, attacker ? new Color(0.85f, 0.25f, 0.22f) : new Color(0.25f, 0.45f, 0.9f));
        MapEditHandle handle = grab.AddComponent<MapEditHandle>();
        handle.kind = attacker ? MapEditHandle.Kind.AttackerSpawn : MapEditHandle.Kind.DefenderSpawn;
        handle.sectorIndex = sectorIndex;
        return spawn.transform;
    }

    Transform CreateBorderHandle(Transform parent, MapEditHandle.Kind kind, int sectorIndex, int edgeIndex)
    {
        GameObject handleObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handleObject.name = kind.ToString();
        handleObject.transform.SetParent(parent, false);
        Paint(handleObject, new Color(0.95f, 0.95f, 0.95f, 1f));
        MapEditHandle handle = handleObject.AddComponent<MapEditHandle>();
        handle.kind = kind;
        handle.sectorIndex = sectorIndex;
        handle.depthEdgeIndex = edgeIndex;
        return handleObject.transform;
    }

    static void SetGrabActive(Transform owner, bool active)
    {
        if (owner == null) return;
        Transform grab = owner.Find("Grab");
        if (grab != null) grab.gameObject.SetActive(active);
    }

    static GameObject CreateCube(string objectName, Transform parent, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        Paint(cube, color);
        return cube;
    }

    static void Paint(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.material = CreateMaterial(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.color = color;
        return material;
    }

    static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
    }

    static Transform AddLabel(Transform parent, string text)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh mesh = labelObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 48;
        mesh.characterSize = 0.35f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(1f, 1f, 1f, 0.92f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) mesh.font = font;
        return labelObject.transform;
    }

    static UnitSpawner FindSpawner(string objectName)
    {
        GameObject spawnerObject = GameObject.Find(objectName);
        if (spawnerObject == null) return null;
        return spawnerObject.GetComponent<UnitSpawner>();
    }

    static GameObject LoadUnitPrefab(string assetPath)
    {
#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab != null) return prefab;
#endif
        Debug.LogError("Could not load a unit prefab at " + assetPath + ". A generated match needs it on the spawner.");
        return null;
    }
}
