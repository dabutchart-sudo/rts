#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts the authored Greybox Battlefield 01 blockout into a playable Breakthrough scene.
/// This deliberately remains an Editor operation so the generated scene contains normal,
/// inspectable GameObjects rather than runtime-only map setup.
/// </summary>
public static class GreyboxBattlefieldGameplayWiring
{
    private const string SceneName = "GreyboxBattlefield01";
    private const string GeneratedRootName = "GreyboxBattlefield01_Generated";
    private const string GameplayRootName = "GREYBOX GAMEPLAY";
    private const string CapturePrefabPath = "Assets/Prefabs/CaptureZone.prefab";
    private const string AttackerAssaultPrefabPath = "Assets/Prefabs/Units/Assault_Attacker.prefab";
    private const string DefenderAssaultPrefabPath = "Assets/Prefabs/Units/Assault_Defender.prefab";

    [MenuItem("RTS/Maps/Wire Greybox Battlefield 01 for Play")]
    public static void WireForPlay()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            EditorUtility.DisplayDialog(
                "Greybox Battlefield 01",
                "Open/build GreyboxBattlefield01 first, then run this command again.",
                "OK");
            return;
        }

        GameObject generatedRoot = GameObject.Find(GeneratedRootName);
        if (generatedRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Greybox Battlefield 01",
                "The generated map root was not found. Run RTS > Maps > Build Greybox Battlefield 01 (Safe) first.",
                "OK");
            return;
        }

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
        {
            Debug.LogError("MAP WIRING: No GameManager was found in GreyboxBattlefield01.");
            return;
        }

        CaptureCatalogTemplate catalog = CaptureCatalogTemplate.FromScene();

        RemovePreviousGameplayRoot(generatedRoot.transform);
        RemoveLegacyCapturePoints();
        DisableLegacyPrototypeGeometry(generatedRoot.transform);
        DisableTemporaryTerraces(generatedRoot.transform);

        GameObject gameplayRoot = new GameObject(GameplayRootName);
        gameplayRoot.transform.SetParent(generatedRoot.transform, false);

        CapturePoint a1 = CreateCapturePoint(gameplayRoot.transform, "A1", 0, new Vector3(-38f, 0.18f, 63f), catalog);
        CapturePoint a2 = CreateCapturePoint(gameplayRoot.transform, "A2", 0, new Vector3(32f, 0.18f, 78f), catalog);
        CapturePoint b1 = CreateCapturePoint(gameplayRoot.transform, "B1", 1, new Vector3(-34f, 0.18f, 143f), catalog);
        CapturePoint b2 = CreateCapturePoint(gameplayRoot.transform, "B2", 1, new Vector3(37f, 0.18f, 154f), catalog);
        CapturePoint c1 = CreateCapturePoint(gameplayRoot.transform, "C1", 2, new Vector3(-34f, 0.18f, 231f), catalog);
        CapturePoint c2 = CreateCapturePoint(gameplayRoot.transform, "C2", 2, new Vector3(36f, 0.18f, 242f), catalog);

        BaseZone attackerS1 = CreateBase(gameplayRoot.transform, "S1_AttackerBase", Faction.Attacker, new Vector3(-8f, 0.18f, -6f));
        BaseZone defenderS1 = CreateBase(gameplayRoot.transform, "S1_DefenderBase", Faction.Defender, new Vector3(-8f, 0.18f, 108f));
        BaseZone attackerS2 = CreateBase(gameplayRoot.transform, "S2_AttackerBase", Faction.Attacker, new Vector3(-10f, 0.18f, 111f));
        BaseZone defenderS2 = CreateBase(gameplayRoot.transform, "S2_DefenderBase", Faction.Defender, new Vector3(-6f, 0.18f, 197f));
        BaseZone attackerS3 = CreateBase(gameplayRoot.transform, "S3_AttackerBase", Faction.Attacker, new Vector3(-6f, 0.18f, 198f));
        BaseZone defenderS3 = CreateBase(gameplayRoot.transform, "S3_DefenderBase", Faction.Defender, new Vector3(4f, 0.18f, 282f));

        gameManager.sectors = new[]
        {
            new Sector
            {
                sectorName = "Sector 1 - Farm Approach",
                sectorAnnouncementText = "SECURE THE FARM AND CHECKPOINT",
                capturePoints = new[] { a1, a2 },
                attackerBase = attackerS1,
                defenderBase = defenderS1,
                sectorBounds = new Bounds(new Vector3(0f, 0f, 52f), new Vector3(190f, 20f, 128f))
            },
            new Sector
            {
                sectorName = "Sector 2 - Industrial Crossing",
                sectorAnnouncementText = "PUSH THROUGH THE INDUSTRIAL CROSSING",
                capturePoints = new[] { b1, b2 },
                attackerBase = attackerS2,
                defenderBase = defenderS2,
                sectorBounds = new Bounds(new Vector3(0f, 0f, 154f), new Vector3(190f, 20f, 92f))
            },
            new Sector
            {
                sectorName = "Sector 3 - Fortified Ridge",
                sectorAnnouncementText = "BREAK THE FINAL DEFENSIVE LINE",
                capturePoints = new[] { c1, c2 },
                attackerBase = attackerS3,
                defenderBase = defenderS3,
                sectorBounds = new Bounds(new Vector3(0f, 0f, 246f), new Vector3(190f, 20f, 100f))
            }
        };

        gameManager.currentSectorIndex = 0;
        gameManager.battlefieldParent = null;

        RepairSpawnerReferences(gameManager);

        if (gameManager.attackerSpawner != null)
        {
            gameManager.attackerSpawner.transform.position = attackerS1.GetSpawnPoint().position;
        }

        if (gameManager.defenderSpawner != null)
        {
            gameManager.defenderSpawner.transform.position = defenderS1.GetSpawnPoint().position;
        }

        ConfigureNavigation(generatedRoot.transform);

        EditorUtility.SetDirty(gameManager);
        if (gameManager.attackerSpawner != null) EditorUtility.SetDirty(gameManager.attackerSpawner);
        if (gameManager.defenderSpawner != null) EditorUtility.SetDirty(gameManager.defenderSpawner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = gameplayRoot;

        string catalogNote = catalog.HasAnyPurchases
            ? "existing purchase catalog copied"
            : "specialist catalog will be supplied by runtime bootstraps";

        string attackerSpawnerState = DescribeSpawner(gameManager.attackerSpawner);
        string defenderSpawnerState = DescribeSpawner(gameManager.defenderSpawner);

        Debug.Log(
            "MAP WIRING: Greybox Battlefield 01 READY FOR PLAY - " +
            "3 sectors, 6 capture points, 6 dynamic bases, NavMesh baked; " + catalogNote + ".\n" +
            $"MAP WIRING SPAWNERS: Attacker={attackerSpawnerState}; Defender={defenderSpawnerState}");
    }

    private static void RepairSpawnerReferences(GameManager gameManager)
    {
        UnitSpawner attacker = gameManager.attackerSpawner;
        UnitSpawner defender = gameManager.defenderSpawner;

        if (attacker == null)
        {
            GameObject attackerObject = GameObject.Find("AttackerSpawner");
            if (attackerObject != null) attacker = attackerObject.GetComponent<UnitSpawner>();
        }

        if (defender == null)
        {
            GameObject defenderObject = GameObject.Find("DefenderSpawner");
            if (defenderObject != null) defender = defenderObject.GetComponent<UnitSpawner>();
        }

        if (attacker == null || defender == null)
        {
            UnitSpawner[] allSpawners = Object.FindObjectsByType<UnitSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (UnitSpawner spawner in allSpawners)
            {
                if (spawner == null) continue;
                if (spawner.isDefenderSpawner && defender == null) defender = spawner;
                if (!spawner.isDefenderSpawner && attacker == null) attacker = spawner;
            }
        }

        if (attacker != null)
        {
            attacker.isDefenderSpawner = false;
            if (attacker.assaultPrefab == null)
            {
                attacker.assaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AttackerAssaultPrefabPath);
            }
        }

        if (defender != null)
        {
            defender.isDefenderSpawner = true;
            if (defender.assaultPrefab == null)
            {
                defender.assaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefenderAssaultPrefabPath);
            }
        }

        gameManager.attackerSpawner = attacker;
        gameManager.defenderSpawner = defender;

        if (gameManager.attackerSpawner == null)
        {
            Debug.LogError("MAP WIRING: Attacker UnitSpawner reference could not be repaired.");
        }
        else if (gameManager.attackerSpawner.assaultPrefab == null)
        {
            Debug.LogError($"MAP WIRING: Attacker assault prefab missing. Expected {AttackerAssaultPrefabPath}.");
        }

        if (gameManager.defenderSpawner == null)
        {
            Debug.LogError("MAP WIRING: Defender UnitSpawner reference could not be repaired.");
        }
        else if (gameManager.defenderSpawner.assaultPrefab == null)
        {
            Debug.LogError($"MAP WIRING: Defender assault prefab missing. Expected {DefenderAssaultPrefabPath}.");
        }
    }

    private static string DescribeSpawner(UnitSpawner spawner)
    {
        if (spawner == null) return "MISSING";
        string prefabName = spawner.assaultPrefab != null ? spawner.assaultPrefab.name : "NO PREFAB";
        return $"{spawner.name}, prefab={prefabName}, defender={spawner.isDefenderSpawner}";
    }

    private static CapturePoint CreateCapturePoint(
        Transform parent,
        string pointName,
        int sectorIndex,
        Vector3 position,
        CaptureCatalogTemplate catalog)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CapturePrefabPath);
        GameObject instance;

        if (prefab != null)
        {
            instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetParent(parent, false);
            BoxCollider fallbackCollider = instance.GetComponent<BoxCollider>();
            if (fallbackCollider != null) fallbackCollider.isTrigger = true;
            instance.AddComponent<CapturePoint>();
        }

        if (instance == null)
        {
            throw new System.InvalidOperationException($"Could not create capture point {pointName}.");
        }

        instance.name = $"CapturePoint_{pointName}";
        instance.transform.position = position;

        CapturePoint capturePoint = instance.GetComponent<CapturePoint>();
        if (capturePoint == null) capturePoint = instance.AddComponent<CapturePoint>();

        capturePoint.capturePointName = pointName;
        capturePoint.activeDuringSectorIndex = sectorIndex;
        capturePoint.captureRateMultiplier = 0.33f;
        capturePoint.attackerPurchasables = catalog.CloneAttacker();
        capturePoint.defenderPurchasables = catalog.CloneDefender();
        capturePoint.purchaseSpawnPoints = CreatePurchaseSpawnPoints(instance.transform);

        EditorUtility.SetDirty(capturePoint);
        return capturePoint;
    }

    private static Transform[] CreatePurchaseSpawnPoints(Transform capturePointRoot)
    {
        Transform existing = capturePointRoot.Find("PurchaseSpawnPoints");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject holder = new GameObject("PurchaseSpawnPoints");
        holder.transform.SetParent(capturePointRoot, false);

        Vector3[] localOffsets =
        {
            new Vector3(-0.35f, 0.2f, -0.35f),
            new Vector3(0.35f, 0.2f, -0.35f),
            new Vector3(-0.35f, 0.2f, 0.35f),
            new Vector3(0.35f, 0.2f, 0.35f)
        };

        Transform[] points = new Transform[localOffsets.Length];
        for (int i = 0; i < localOffsets.Length; i++)
        {
            GameObject point = new GameObject($"Spawn_{i + 1}");
            point.transform.SetParent(holder.transform, false);
            point.transform.localPosition = localOffsets[i];
            points[i] = point.transform;
        }

        return points;
    }

    private static BaseZone CreateBase(Transform parent, string name, Faction faction, Vector3 position)
    {
        GameObject baseObject = new GameObject(name);
        baseObject.transform.SetParent(parent, false);
        baseObject.transform.position = position;

        BaseZone baseZone = baseObject.AddComponent<BaseZone>();
        baseZone.baseName = name;
        baseZone.controllingFaction = faction;
        baseZone.radius = 5f;

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(baseObject.transform, false);
        spawn.transform.localPosition = Vector3.zero;
        spawn.transform.localRotation = faction == Faction.Attacker
            ? Quaternion.identity
            : Quaternion.Euler(0f, 180f, 0f);

        baseZone.spawnPoint = spawn.transform;
        EditorUtility.SetDirty(baseZone);
        return baseZone;
    }

    private static void RemovePreviousGameplayRoot(Transform generatedRoot)
    {
        Transform existing = generatedRoot.Find(GameplayRootName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    private static void RemoveLegacyCapturePoints()
    {
        CapturePoint[] existingPoints = Object.FindObjectsByType<CapturePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CapturePoint point in existingPoints)
        {
            if (point != null) Object.DestroyImmediate(point.gameObject);
        }
    }

    private static void DisableLegacyPrototypeGeometry(Transform generatedRoot)
    {
        DisableNamedObjectOutsideGeneratedRoot("Ground", generatedRoot);
        DisableNamedObjectOutsideGeneratedRoot("ObjectiveTarget", generatedRoot);
        DisableNamedObjectOutsideGeneratedRoot("TankSpawner", generatedRoot);
    }

    private static void DisableNamedObjectOutsideGeneratedRoot(string objectName, Transform generatedRoot)
    {
        GameObject candidate = GameObject.Find(objectName);
        if (candidate == null) return;
        if (candidate.transform == generatedRoot || candidate.transform.IsChildOf(generatedRoot)) return;
        candidate.SetActive(false);
    }

    private static void DisableTemporaryTerraces(Transform generatedRoot)
    {
        string[] names = { "West Rise S1", "East Rise S2", "Final Ridge" };
        foreach (string name in names)
        {
            Transform child = FindDeepChild(generatedRoot, name);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private static void ConfigureNavigation(Transform generatedRoot)
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface == null) continue;
            surface.RemoveData();
            if (!surface.transform.IsChildOf(generatedRoot) && surface.transform != generatedRoot)
            {
                surface.enabled = false;
            }
        }

        Transform ground = FindDeepChild(generatedRoot, "Battlefield Ground");
        if (ground == null)
        {
            Debug.LogError("MAP WIRING: Generated Battlefield Ground was not found; NavMesh was not baked.");
            return;
        }

        NavMeshSurface generatedSurface = ground.GetComponent<NavMeshSurface>();
        if (generatedSurface == null) generatedSurface = ground.gameObject.AddComponent<NavMeshSurface>();

        generatedSurface.collectObjects = CollectObjects.All;
        generatedSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        generatedSurface.BuildNavMesh();
        EditorUtility.SetDirty(generatedSurface);
    }

    private sealed class CaptureCatalogTemplate
    {
        private PurchasableUnit[] attacker;
        private PurchasableUnit[] defender;

        public bool HasAnyPurchases =>
            (attacker != null && attacker.Length > 0) ||
            (defender != null && defender.Length > 0);

        public PurchasableUnit[] CloneAttacker() => CloneArray(attacker);
        public PurchasableUnit[] CloneDefender() => CloneArray(defender);

        public static CaptureCatalogTemplate FromScene()
        {
            CaptureCatalogTemplate template = new CaptureCatalogTemplate();
            CapturePoint[] points = Object.FindObjectsByType<CapturePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (CapturePoint point in points)
            {
                if (point == null) continue;

                if ((template.attacker == null || template.attacker.Length == 0) &&
                    point.attackerPurchasables != null && point.attackerPurchasables.Length > 0)
                {
                    template.attacker = CloneArray(point.attackerPurchasables);
                }

                if ((template.defender == null || template.defender.Length == 0) &&
                    point.defenderPurchasables != null && point.defenderPurchasables.Length > 0)
                {
                    template.defender = CloneArray(point.defenderPurchasables);
                }
            }

            return template;
        }

        private static PurchasableUnit[] CloneArray(PurchasableUnit[] source)
        {
            if (source == null || source.Length == 0) return new PurchasableUnit[0];

            List<PurchasableUnit> result = new List<PurchasableUnit>();
            foreach (PurchasableUnit unit in source)
            {
                if (unit == null) continue;

                result.Add(new PurchasableUnit
                {
                    unitDisplayName = unit.unitDisplayName,
                    unitPrefab = unit.unitPrefab,
                    xpCost = unit.xpCost,
                    unitIcon = unit.unitIcon,
                    useExplicitClassification = unit.useExplicitClassification,
                    unitCategory = unit.unitCategory,
                    unitClass = unit.unitClass
                });
            }

            return result.ToArray();
        }
    }
}
#endif
