using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Temporary look test. Not used by the match.
// Open Assets/Scenes/SoldierLookDev and press Play, or use RTS → Look → Soldier Uniform Test.
public class SoldierLookDev : MonoBehaviour
{
    const float LoopRadiusX = 3.55f;
    const float LoopRadiusZ = 2.25f;
    const float StepLength = 0.48f;

    readonly List<Walker> walkers = new List<Walker>();
    Camera viewCamera;
    float yaw = 28f;
    float pitch = 14f;
    float distance = 7.6f;
    float lapMeters;
    Texture2D camo;
    GUIStyle labelStyle;
    GUIStyle nameStyle;
    GUIStyle goldStyle;
    GUIStyle panelStyle;

    class Walker
    {
        public Transform root;
        public Transform pelvis;
        public Transform spine;
        public Transform chest;
        public Transform head;
        public Transform thighL;
        public Transform shinL;
        public Transform footL;
        public Transform thighR;
        public Transform shinR;
        public Transform footR;
        public Transform armL;
        public Transform foreL;
        public Transform handL;
        public Transform armR;
        public Transform foreR;
        public Transform handR;
        public Transform rifle;
        public Transform gripL;
        public Transform gripR;
        public Transform soleL;
        public Transform soleR;
        public string title;
        public Color labelColor;
        public float pathT;
        public float legSwing;
        public float kneeBend;
        public float armSwing;
        public float polish;
        public float bob;
        public Quaternion armLRest;
        public Quaternion foreLRest;
        public Quaternion armRRest;
        public Quaternion foreRRest;
        public Vector3 rifleRestPos;
        public Quaternion rifleRestRot;
        public bool steadyRifle;
    }

    void Start()
    {
        lapMeters = MeasureLoop();
        camo = MakeCamo();
        EnsureStage();
        SpawnReferenceUnit();
        Spawn(Rifleman(), 0.00f);
        Spawn(BlockSoldier(), 0.20f);
        Spawn(Miniature(), 0.40f);
        Spawn(Operator(), 0.60f);
        Spawn(Gunner(), 0.80f);
    }

    void Update()
    {
        float metersPerSecond = 0.78f;
        float deltaT = metersPerSecond / lapMeters * Time.deltaTime;
        for (int i = 0; i < walkers.Count; i++)
        {
            Walker walker = walkers[i];
            walker.pathT = Mathf.Repeat(walker.pathT + deltaT, 1f);
            PlaceOnLoop(walker);
            PoseWalk(walker);
        }

        Orbit();
    }

    void OnGUI()
    {
        EnsureGui();
        GUI.Box(new Rect(16f, 16f, 430f, 168f), GUIContent.none, panelStyle);
        GUI.Label(new Rect(28f, 24f, 400f, 152f),
            "Temporary uniform test. Not part of the match.\n\n" +
            "Rifleman, in gold, is the detailed one.\n" +
            "Block, Miniature, Operator, and Gunner are the other four.\n" +
            "The red capsule off the path is today's match unit.\n\n" +
            "Right-drag to look. Scroll to zoom.",
            labelStyle);

        if (viewCamera == null) return;
        for (int i = 0; i < walkers.Count; i++)
        {
            Walker walker = walkers[i];
            if (walker.head == null) continue;
            Vector3 world = walker.head.position + Vector3.up * (0.28f * walker.root.lossyScale.y);
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            if (screen.z < 0f) continue;
            Rect rect = new Rect(screen.x - 70f, Screen.height - screen.y - 18f, 140f, 24f);
            GUI.Label(rect, walker.title, walker.labelColor == Color.white ? nameStyle : goldStyle);
        }
    }

    void EnsureStage()
    {
        viewCamera = Camera.main;
        if (viewCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            viewCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = new Color(0.63f, 0.72f, 0.80f);
        viewCamera.fieldOfView = 30f;
        viewCamera.nearClipPlane = 0.05f;
        viewCamera.farClipPlane = 80f;

        Light sun = FindAnyObjectByType<Light>();
        if (sun == null)
        {
            GameObject sunObject = new GameObject("Sun");
            sun = sunObject.AddComponent<Light>();
        }

        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

        GameObject fillObject = new GameObject("Fill");
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.70f, 0.78f, 0.88f);
        fill.intensity = 0.38f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(18f, 150f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.46f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.18f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        Paint(ground, new Color(0.36f, 0.40f, 0.34f), 0.08f, null);
        Destroy(ground.GetComponent<Collider>());

        LineRenderer track = gameObject.AddComponent<LineRenderer>();
        track.loop = true;
        track.positionCount = 72;
        track.startWidth = 1.05f;
        track.endWidth = 1.05f;
        track.useWorldSpace = true;
        track.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material trackMaterial = LitMaterial(new Color(0.28f, 0.30f, 0.27f), 0.04f, null);
        track.material = trackMaterial;
        for (int i = 0; i < track.positionCount; i++)
        {
            Vector3 point = LoopPoint(i / (float)track.positionCount);
            point.y = 0.02f;
            track.SetPosition(i, point);
        }
    }

    void SpawnReferenceUnit()
    {
        GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        unit.name = "Today's match unit";
        unit.transform.position = new Vector3(0f, 0.9f, -3.55f);
        unit.transform.localScale = new Vector3(0.42f, 0.9f, 0.42f);
        Paint(unit, new Color(0.72f, 0.16f, 0.14f), 0.18f, null);
        Destroy(unit.GetComponent<Collider>());

        Walker label = new Walker
        {
            root = unit.transform,
            head = Bone(unit.transform, "Label", new Vector3(0f, 1.2f, 0f)),
            title = "Today's unit",
            labelColor = Color.white
        };
        walkers.Add(label);
    }

    void Spawn(FigureSpec spec, float pathT)
    {
        Walker walker = new Walker
        {
            title = spec.title,
            labelColor = spec.goldLabel ? new Color(0.95f, 0.82f, 0.35f) : Color.white,
            pathT = pathT,
            legSwing = spec.legSwing,
            kneeBend = spec.kneeBend,
            armSwing = spec.armSwing,
            polish = spec.polish,
            bob = spec.bob,
            steadyRifle = spec.steadyRifle
        };

        GameObject rootObject = new GameObject(spec.title);
        walker.root = rootObject.transform;
        walker.root.localScale = Vector3.one * spec.scale;

        walker.pelvis = Bone(walker.root, "Pelvis", new Vector3(0f, 0.98f, 0f));
        walker.spine = Bone(walker.pelvis, "Spine", new Vector3(0f, 0.06f, 0f));
        walker.chest = Bone(walker.spine, "Chest", new Vector3(0f, 0.22f, 0f));
        Transform neck = Bone(walker.chest, "Neck", new Vector3(0f, 0.22f, 0f));
        walker.head = Bone(neck, "Head", new Vector3(0f, 0.08f, 0.01f));

        walker.thighL = Bone(walker.pelvis, "Thigh L", new Vector3(-0.10f * spec.bulk, -0.04f, 0f));
        walker.shinL = Bone(walker.thighL, "Shin L", new Vector3(0f, -0.44f, 0f));
        walker.footL = Bone(walker.shinL, "Foot L", new Vector3(0f, -0.42f, 0f));
        walker.thighR = Bone(walker.pelvis, "Thigh R", new Vector3(0.10f * spec.bulk, -0.04f, 0f));
        walker.shinR = Bone(walker.thighR, "Shin R", new Vector3(0f, -0.44f, 0f));
        walker.footR = Bone(walker.shinR, "Foot R", new Vector3(0f, -0.42f, 0f));

        Transform clavicleL = Bone(walker.chest, "Clavicle L", new Vector3(-0.16f * spec.bulk, 0.14f, 0f));
        walker.armL = Bone(clavicleL, "Arm L", new Vector3(-0.06f, -0.02f, 0f));
        walker.foreL = Bone(walker.armL, "Forearm L", new Vector3(0f, -0.28f, 0f));
        Transform handL = Bone(walker.foreL, "Hand L", new Vector3(0f, -0.25f, 0f));
        Transform clavicleR = Bone(walker.chest, "Clavicle R", new Vector3(0.16f * spec.bulk, 0.14f, 0f));
        walker.armR = Bone(clavicleR, "Arm R", new Vector3(0.06f, -0.02f, 0f));
        walker.foreR = Bone(walker.armR, "Forearm R", new Vector3(0f, -0.28f, 0f));
        Transform handR = Bone(walker.foreR, "Hand R", new Vector3(0f, -0.25f, 0f));
        walker.handL = handL;
        walker.handR = handR;

        Material cloth = LitMaterial(spec.cloth, 0.16f, spec.useCamo ? camo : null);
        Material skin = LitMaterial(spec.skin, 0.28f, null);
        Material vestMat = LitMaterial(spec.vest, 0.12f, null);
        Material pouchMat = LitMaterial(spec.pouch, 0.14f, null);
        Material bootMat = LitMaterial(spec.boot, 0.08f, null);
        Material gearMat = LitMaterial(spec.gear, 0.22f, null);
        Material gloveMat = LitMaterial(new Color(0.18f, 0.16f, 0.15f), 0.2f, null);
        Material darkMat = LitMaterial(new Color(0.10f, 0.10f, 0.11f), 0.45f, null);

        if (spec.blocky)
        {
            Box(walker.pelvis, new Vector3(0.22f, 0.16f, 0.14f), Vector3.zero, cloth);
            Box(walker.chest, new Vector3(0.36f * spec.bulk, 0.36f, 0.20f), new Vector3(0f, 0.02f, 0f), cloth);
            Box(walker.head, new Vector3(0.18f, 0.20f, 0.18f), new Vector3(0f, 0.06f, 0f), skin);
            Box(walker.head, new Vector3(0.22f, 0.12f, 0.22f), new Vector3(0f, 0.14f, 0f), spec.useCamo ? cloth : gearMat);
            LimbBox(walker.thighL, 0.44f, new Vector3(0.12f, 0.44f, 0.12f), cloth);
            LimbBox(walker.shinL, 0.42f, new Vector3(0.10f, 0.42f, 0.10f), cloth);
            LimbBox(walker.thighR, 0.44f, new Vector3(0.12f, 0.44f, 0.12f), cloth);
            LimbBox(walker.shinR, 0.42f, new Vector3(0.10f, 0.42f, 0.10f), cloth);
            LimbBox(walker.armL, 0.28f, new Vector3(0.09f, 0.28f, 0.09f), cloth);
            LimbBox(walker.foreL, 0.25f, new Vector3(0.08f, 0.25f, 0.08f), cloth);
            LimbBox(walker.armR, 0.28f, new Vector3(0.09f, 0.28f, 0.09f), cloth);
            LimbBox(walker.foreR, 0.25f, new Vector3(0.08f, 0.25f, 0.08f), cloth);
            Box(walker.footL, new Vector3(0.10f, 0.08f, 0.22f), new Vector3(0f, -0.02f, 0.06f), bootMat);
            Box(walker.footR, new Vector3(0.10f, 0.08f, 0.22f), new Vector3(0f, -0.02f, 0.06f), bootMat);
            Box(handL, new Vector3(0.06f, 0.08f, 0.05f), new Vector3(0f, -0.04f, 0f), skin);
            Box(handR, new Vector3(0.06f, 0.08f, 0.05f), new Vector3(0f, -0.04f, 0f), skin);
        }
        else
        {
            AddMesh(walker.pelvis, Loft(new[]
            {
                V(0.12f * spec.bulk, -0.06f, 0.09f * spec.bulk),
                V(0.13f * spec.bulk, 0.02f, 0.09f * spec.bulk),
                V(0.11f * spec.bulk, 0.08f, 0.08f * spec.bulk)
            }, 12), cloth, Vector3.zero);

            AddMesh(walker.chest, Torso(spec.bulk), cloth, new Vector3(0f, -0.16f, 0f));
            AddMesh(neck, Limb(0.08f, 0.045f, 0.04f, 0f, 8), skin, Vector3.zero);
            AddMesh(walker.head, Head(spec.headScale), skin, new Vector3(0f, 0.02f, 0f));
            AddMesh(walker.thighL, Limb(0.44f, 0.075f * spec.bulk, 0.055f * spec.bulk, 0.18f, 12), cloth, Vector3.zero);
            AddMesh(walker.shinL, Limb(0.42f, 0.055f * spec.bulk, 0.04f * spec.bulk, 0.22f, 12), cloth, Vector3.zero);
            AddMesh(walker.thighR, Limb(0.44f, 0.075f * spec.bulk, 0.055f * spec.bulk, 0.18f, 12), cloth, Vector3.zero);
            AddMesh(walker.shinR, Limb(0.42f, 0.055f * spec.bulk, 0.04f * spec.bulk, 0.22f, 12), cloth, Vector3.zero);

            Material sleeve = spec.bareForearms ? skin : cloth;
            AddMesh(walker.armL, Limb(0.28f, 0.05f * spec.bulk, 0.042f, 0.08f, 10), cloth, Vector3.zero);
            AddMesh(walker.foreL, Limb(0.25f, 0.04f, 0.032f, 0.05f, 10), sleeve, Vector3.zero);
            AddMesh(walker.armR, Limb(0.28f, 0.05f * spec.bulk, 0.042f, 0.08f, 10), cloth, Vector3.zero);
            AddMesh(walker.foreR, Limb(0.25f, 0.04f, 0.032f, 0.05f, 10), sleeve, Vector3.zero);
            AddMesh(handL, Head(0.42f), spec.gloves ? gloveMat : skin, new Vector3(0f, -0.03f, 0f));
            AddMesh(handR, Head(0.42f), spec.gloves ? gloveMat : skin, new Vector3(0f, -0.03f, 0f));

            AddShoulder(clavicleL, cloth, spec.bulk);
            AddShoulder(clavicleR, cloth, spec.bulk);
        }

        if (spec.helmet)
        {
            if (spec.blocky)
            {
                Box(walker.head, new Vector3(0.24f, 0.10f, 0.24f), new Vector3(0f, 0.16f, 0.02f), gearMat);
            }
            else
            {
                AddMesh(walker.head, Helmet(spec.headScale), spec.useCamo ? cloth : gearMat, new Vector3(0f, 0.07f, 0f));
                Box(walker.head, new Vector3(0.16f * spec.headScale, 0.025f, 0.08f), new Vector3(0f, 0.05f, 0.09f), spec.useCamo ? cloth : gearMat);
                if (spec.detailed)
                {
                    Box(walker.head, new Vector3(0.07f, 0.035f, 0.04f), new Vector3(0f, 0.10f, 0.09f), darkMat);
                }
            }
        }

        if (spec.cap)
        {
            Box(walker.head, new Vector3(0.16f, 0.07f, 0.16f), new Vector3(0f, 0.12f, 0f), gearMat);
            Box(walker.head, new Vector3(0.16f, 0.015f, 0.10f), new Vector3(0f, 0.085f, 0.10f), gearMat);
        }

        if (spec.sunglasses)
        {
            Box(walker.head, new Vector3(0.11f, 0.028f, 0.02f), new Vector3(0f, 0.045f, 0.075f), darkMat);
        }

        if (spec.detailed)
        {
            AddMesh(walker.head, Brow(spec.headScale), skin, new Vector3(0f, 0.045f, 0.07f));
            Box(walker.head, new Vector3(0.018f, 0.028f, 0.02f), new Vector3(0f, 0.02f, 0.085f), skin);
            Box(walker.head, new Vector3(0.018f, 0.016f, 0.012f), new Vector3(-0.028f, 0.03f, 0.072f), darkMat);
            Box(walker.head, new Vector3(0.018f, 0.016f, 0.012f), new Vector3(0.028f, 0.03f, 0.072f), darkMat);
            AddMesh(walker.head, Ear(), skin, new Vector3(-0.075f, 0.01f, 0f));
            AddMesh(walker.head, Ear(), skin, new Vector3(0.075f, 0.01f, 0f));
        }

        if (spec.wearVest)
        {
            Box(walker.chest, new Vector3(0.30f * spec.bulk, 0.28f, 0.08f), new Vector3(0f, 0.02f, 0.09f), vestMat);
            Box(walker.chest, new Vector3(0.26f * spec.bulk, 0.24f, 0.05f), new Vector3(0f, 0.02f, -0.08f), vestMat);
            Box(walker.chest, new Vector3(0.05f, 0.16f, 0.05f), new Vector3(-0.12f * spec.bulk, 0.10f, 0.02f), vestMat);
            Box(walker.chest, new Vector3(0.05f, 0.16f, 0.05f), new Vector3(0.12f * spec.bulk, 0.10f, 0.02f), vestMat);
        }

        for (int i = 0; i < spec.pouches; i++)
        {
            float x = (i - (spec.pouches - 1) * 0.5f) * 0.07f;
            Box(walker.chest, new Vector3(0.055f, 0.09f, 0.035f), new Vector3(x, -0.08f, 0.13f), pouchMat);
        }

        if (spec.detailed)
        {
            Box(walker.chest, new Vector3(0.07f, 0.06f, 0.03f), new Vector3(0.10f, 0.06f, 0.12f), pouchMat);
            Box(walker.chest, new Vector3(0.045f, 0.08f, 0.03f), new Vector3(-0.14f, 0.02f, 0.08f), darkMat);
            Box(walker.chest, new Vector3(0.008f, 0.10f, 0.008f), new Vector3(-0.14f, 0.12f, 0.08f), darkMat);
            Box(walker.pelvis, new Vector3(0.28f * spec.bulk, 0.035f, 0.16f), new Vector3(0f, 0.02f, 0f), pouchMat);
            Box(walker.shinL, new Vector3(0.07f, 0.06f, 0.03f), new Vector3(0f, -0.18f, 0.045f), pouchMat);
            Box(walker.shinR, new Vector3(0.07f, 0.06f, 0.03f), new Vector3(0f, -0.18f, 0.045f), pouchMat);
        }

        if (spec.kneePads && !spec.detailed)
        {
            Box(walker.shinL, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(0f, -0.02f, 0.04f), pouchMat);
            Box(walker.shinR, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(0f, -0.02f, 0.04f), pouchMat);
        }

        if (spec.backpack)
        {
            Box(walker.chest, new Vector3(0.26f, 0.32f, 0.12f), new Vector3(0f, 0.02f, -0.16f), gearMat);
            Box(walker.chest, new Vector3(0.10f, 0.12f, 0.08f), new Vector3(0f, -0.16f, -0.14f), pouchMat);
        }

        AddBoots(walker, bootMat, spec.detailed, spec.blocky);
        walker.soleL = Bone(walker.footL, "Sole L", new Vector3(0f, -0.07f, 0.05f));
        walker.soleR = Bone(walker.footR, "Sole R", new Vector3(0f, -0.07f, 0.05f));

        Transform rifleParent = spec.steadyRifle ? walker.chest : handR;
        walker.rifle = BuildWeapon(rifleParent, spec.machineGun, darkMat, pouchMat);
        if (spec.steadyRifle)
        {
            walker.rifle.localPosition = spec.machineGun
                ? new Vector3(0.03f, -0.06f, 0.22f)
                : new Vector3(0.04f, -0.05f, 0.18f);
            walker.rifle.localRotation = Quaternion.Euler(spec.machineGun ? 8f : 12f, spec.machineGun ? 4f : 10f, 0f);
            walker.rifleRestPos = walker.rifle.localPosition;
            walker.rifleRestRot = walker.rifle.localRotation;
            walker.gripR = Bone(walker.rifle, "Grip R", spec.machineGun
                ? new Vector3(0.02f, -0.05f, -0.02f)
                : new Vector3(0.015f, -0.04f, -0.04f));
            walker.gripL = Bone(walker.rifle, "Grip L", spec.machineGun
                ? new Vector3(-0.01f, 0.01f, 0.30f)
                : new Vector3(-0.01f, 0.0f, 0.18f));
        }
        else
        {
            walker.rifle.localPosition = new Vector3(0.02f, -0.04f, 0.08f);
            walker.rifle.localRotation = Quaternion.Euler(70f, 0f, 0f);
            walker.armLRest = Quaternion.Euler(18f, 0f, -16f);
            walker.foreLRest = Quaternion.Euler(-18f, 0f, 0f);
            walker.armRRest = Quaternion.Euler(18f, 0f, 16f);
            walker.foreRRest = Quaternion.Euler(-22f, 0f, 0f);
            walker.armL.localRotation = walker.armLRest;
            walker.foreL.localRotation = walker.foreLRest;
            walker.armR.localRotation = walker.armRRest;
            walker.foreR.localRotation = walker.foreRRest;
        }

        walkers.Add(walker);
    }

    static FigureSpec Rifleman()
    {
        return new FigureSpec
        {
            title = "Rifleman",
            goldLabel = true,
            detailed = true,
            useCamo = true,
            helmet = true,
            wearVest = true,
            gloves = true,
            pouches = 3,
            steadyRifle = true,
            polish = 1f,
            bob = 0.012f,
            legSwing = 20f,
            kneeBend = 46f,
            armSwing = 4f,
            bulk = 1f,
            scale = 1f,
            headScale = 1f,
            skin = new Color(0.74f, 0.56f, 0.43f),
            cloth = Color.white,
            vest = new Color(0.31f, 0.35f, 0.27f),
            pouch = new Color(0.48f, 0.40f, 0.28f),
            boot = new Color(0.15f, 0.11f, 0.09f),
            gear = new Color(0.40f, 0.36f, 0.26f)
        };
    }

    static FigureSpec BlockSoldier()
    {
        return new FigureSpec
        {
            title = "Block",
            blocky = true,
            helmet = true,
            wearVest = true,
            pouches = 2,
            steadyRifle = false,
            polish = 0.65f,
            bob = 0.016f,
            legSwing = 22f,
            kneeBend = 40f,
            armSwing = 16f,
            bulk = 1.05f,
            scale = 1f,
            headScale = 1f,
            skin = new Color(0.78f, 0.62f, 0.48f),
            cloth = new Color(0.34f, 0.40f, 0.26f),
            vest = new Color(0.45f, 0.40f, 0.28f),
            pouch = new Color(0.40f, 0.34f, 0.22f),
            boot = new Color(0.18f, 0.14f, 0.11f),
            gear = new Color(0.55f, 0.48f, 0.32f)
        };
    }

    static FigureSpec Miniature()
    {
        return new FigureSpec
        {
            title = "Miniature",
            helmet = true,
            wearVest = true,
            pouches = 1,
            steadyRifle = false,
            polish = 0.55f,
            bob = 0.02f,
            legSwing = 22f,
            kneeBend = 42f,
            armSwing = 18f,
            bulk = 1.35f,
            scale = 0.78f,
            headScale = 1.55f,
            skin = new Color(0.80f, 0.64f, 0.50f),
            cloth = new Color(0.42f, 0.50f, 0.26f),
            vest = new Color(0.76f, 0.64f, 0.38f),
            pouch = new Color(0.55f, 0.42f, 0.24f),
            boot = new Color(0.22f, 0.16f, 0.12f),
            gear = new Color(0.32f, 0.36f, 0.24f)
        };
    }

    static FigureSpec Operator()
    {
        return new FigureSpec
        {
            title = "Operator",
            cap = true,
            sunglasses = true,
            wearVest = true,
            bareForearms = true,
            pouches = 1,
            steadyRifle = true,
            polish = 0.85f,
            bob = 0.01f,
            legSwing = 20f,
            kneeBend = 44f,
            armSwing = 4f,
            bulk = 0.88f,
            scale = 1.02f,
            headScale = 1f,
            skin = new Color(0.62f, 0.46f, 0.36f),
            cloth = new Color(0.20f, 0.22f, 0.20f),
            vest = new Color(0.14f, 0.15f, 0.14f),
            pouch = new Color(0.24f, 0.24f, 0.22f),
            boot = new Color(0.10f, 0.10f, 0.10f),
            gear = new Color(0.16f, 0.17f, 0.16f)
        };
    }

    static FigureSpec Gunner()
    {
        return new FigureSpec
        {
            title = "Gunner",
            helmet = true,
            wearVest = true,
            backpack = true,
            kneePads = true,
            pouches = 2,
            machineGun = true,
            steadyRifle = true,
            polish = 0.75f,
            bob = 0.012f,
            legSwing = 18f,
            kneeBend = 40f,
            armSwing = 3f,
            bulk = 1.22f,
            scale = 1.04f,
            headScale = 1.05f,
            skin = new Color(0.70f, 0.52f, 0.40f),
            cloth = new Color(0.33f, 0.38f, 0.24f),
            vest = new Color(0.28f, 0.32f, 0.22f),
            pouch = new Color(0.42f, 0.38f, 0.26f),
            boot = new Color(0.16f, 0.13f, 0.10f),
            gear = new Color(0.30f, 0.34f, 0.22f)
        };
    }

    void PlaceOnLoop(Walker walker)
    {
        if (walker.pelvis == null) return;
        Vector3 point = LoopPoint(walker.pathT);
        walker.root.SetPositionAndRotation(new Vector3(point.x, 0f, point.z), Quaternion.LookRotation(LoopForward(walker.pathT), Vector3.up));
    }

    void PoseWalk(Walker walker)
    {
        if (walker.pelvis == null) return;
        float step = StepLength * Mathf.Max(0.4f, walker.root.lossyScale.y);
        float phase = walker.pathT * lapMeters / step * Mathf.PI;

        float leftThigh = Mathf.Sin(phase) * walker.legSwing;
        float rightThigh = Mathf.Sin(phase + Mathf.PI) * walker.legSwing;
        float leftKnee = KneeBend(phase, walker.kneeBend);
        float rightKnee = KneeBend(phase + Mathf.PI, walker.kneeBend);

        walker.thighL.localRotation = Quaternion.Euler(leftThigh, 0f, 0f);
        walker.thighR.localRotation = Quaternion.Euler(rightThigh, 0f, 0f);
        walker.shinL.localRotation = Quaternion.Euler(leftKnee, 0f, 0f);
        walker.shinR.localRotation = Quaternion.Euler(rightKnee, 0f, 0f);
        walker.footL.localRotation = Quaternion.Euler(FootPitch(phase, leftThigh, leftKnee, walker.polish), 0f, 0f);
        walker.footR.localRotation = Quaternion.Euler(FootPitch(phase + Mathf.PI, rightThigh, rightKnee, walker.polish), 0f, 0f);

        float weight = (rightKnee - leftKnee) / Mathf.Max(1f, walker.kneeBend);
        float sway = Mathf.Sin(phase);
        walker.pelvis.localPosition = new Vector3(-weight * 0.04f * walker.polish, 0.98f + (1f - Mathf.Abs(sway)) * walker.bob, 0f);
        walker.pelvis.localRotation = Quaternion.Euler(3f, sway * 5f * walker.polish, weight * 5f * walker.polish);
        walker.spine.localRotation = Quaternion.Euler(6f, -sway * 7f * walker.polish, -weight * 2f * walker.polish);
        walker.head.localRotation = Quaternion.Euler(-3f, sway * 4f * walker.polish, 0f);

        if (walker.steadyRifle && walker.gripL != null && walker.gripR != null)
        {
            walker.rifle.localPosition = walker.rifleRestPos + new Vector3(0f, Mathf.Sin(phase * 2f) * 0.004f, 0f);
            walker.rifle.localRotation = walker.rifleRestRot;
            ReachArm(walker.armL, walker.foreL, walker.gripL.position, walker.root.right * -0.45f + Vector3.down * 0.15f);
            ReachArm(walker.armR, walker.foreR, walker.gripR.position, walker.root.right * 0.45f + Vector3.down * 0.15f);
        }
        else
        {
            SwingArm(walker.armL, walker.foreL, -sway * walker.armSwing, -14f);
            SwingArm(walker.armR, walker.foreR, sway * walker.armSwing, 14f);
        }

        float lowest = Mathf.Min(walker.soleL.position.y, walker.soleR.position.y);
        walker.root.position += Vector3.up * (0.02f - lowest);
    }

    static float KneeBend(float phase, float lift)
    {
        return 8f + Mathf.Pow(Mathf.Clamp01(Mathf.Cos(phase)), 1.4f) * lift;
    }

    static float FootPitch(float phase, float thigh, float knee, float polish)
    {
        float toeUp = Mathf.Clamp01(Mathf.Cos(phase));
        float heelStrike = Mathf.Clamp01(Mathf.Sin(phase));
        float toeOff = Mathf.Clamp01(-Mathf.Sin(phase));
        float worldPitch = toeOff * 28f - toeUp * 18f - heelStrike * 10f;
        return Mathf.Lerp(-thigh * 0.4f, worldPitch - thigh - knee, polish);
    }

    static void SwingArm(Transform upper, Transform fore, float swing, float outward)
    {
        float elbow = 22f + Mathf.Clamp01(-swing / 30f) * 20f;
        upper.localRotation = Quaternion.Euler(swing, 0f, outward);
        fore.localRotation = Quaternion.Euler(-elbow, 0f, 0f);
    }

    static void ReachArm(Transform upper, Transform fore, Vector3 target, Vector3 poleOffset)
    {
        const float upperLen = 0.28f;
        const float lowerLen = 0.25f;
        float scale = Mathf.Max(0.01f, upper.lossyScale.y);
        float upperReach = upperLen * scale;
        float lowerReach = lowerLen * scale;
        Vector3 shoulder = upper.position;
        Vector3 toTarget = target - shoulder;
        float reach = upperReach + lowerReach - 0.01f;
        float distance = Mathf.Clamp(toTarget.magnitude, 0.08f, reach);
        Vector3 targetDir = toTarget.sqrMagnitude < 0.0001f ? Vector3.forward : toTarget.normalized;
        Vector3 clampedTarget = shoulder + targetDir * distance;
        float shoulderAngle = Mathf.Acos(Mathf.Clamp(
            (upperReach * upperReach + distance * distance - lowerReach * lowerReach) / (2f * upperReach * distance), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 pole = shoulder + poleOffset;
        Vector3 axis = Vector3.Cross(targetDir, pole - shoulder);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(targetDir, Vector3.up);
        Vector3 upperDir = Quaternion.AngleAxis(shoulderAngle, axis.normalized) * targetDir;
        Vector3 straight = shoulder + targetDir * upperReach;
        if ((shoulder + upperDir * upperReach - pole).sqrMagnitude > (straight - pole).sqrMagnitude)
            upperDir = Quaternion.AngleAxis(-shoulderAngle, axis.normalized) * targetDir;
        Vector3 elbow = shoulder + upperDir * upperReach;
        Vector3 foreDir = clampedTarget - elbow;
        if (foreDir.sqrMagnitude < 0.0001f) foreDir = targetDir;
        upper.rotation = LimbRotation(upperDir, pole - shoulder);
        fore.rotation = LimbRotation(foreDir, pole - elbow);
    }

    static Quaternion LimbRotation(Vector3 limbDirection, Vector3 poleHint)
    {
        Vector3 direction = limbDirection.normalized;
        Vector3 forward = Vector3.ProjectOnPlane(poleHint, direction);
        if (forward.sqrMagnitude < 0.0004f) forward = Vector3.ProjectOnPlane(Vector3.up, direction);
        if (forward.sqrMagnitude < 0.0004f) forward = Vector3.right;
        return Quaternion.LookRotation(forward.normalized, -direction);
    }

    void Orbit()
    {
        if (viewCamera == null) return;
        bool dragging = Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (dragging)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * 0.22f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.12f, 4f, 38f);
        }
        else
        {
            yaw += 5f * Time.deltaTime;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.65f, 3.4f, 14f);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = new Vector3(0f, 1.05f, 0f);
        viewCamera.transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
    }

    static Vector3 LoopPoint(float t)
    {
        float angle = t * Mathf.PI * 2f;
        return new Vector3(Mathf.Sin(angle) * LoopRadiusX, 0f, Mathf.Cos(angle) * LoopRadiusZ);
    }

    static Vector3 LoopForward(float t)
    {
        float angle = t * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle) * LoopRadiusX, 0f, -Mathf.Sin(angle) * LoopRadiusZ).normalized;
    }

    static float MeasureLoop()
    {
        float length = 0f;
        Vector3 previous = LoopPoint(0f);
        const int steps = 80;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = LoopPoint(i / (float)steps);
            length += Vector3.Distance(previous, point);
            previous = point;
        }

        return length;
    }

    Transform BuildWeapon(Transform parent, bool machineGun, Material dark, Material tan)
    {
        Transform weapon = Bone(parent, machineGun ? "Machine gun" : "Rifle", Vector3.zero);
        if (machineGun)
        {
            Box(weapon, new Vector3(0.07f, 0.08f, 0.28f), new Vector3(0f, 0f, 0.02f), dark);
            Box(weapon, new Vector3(0.05f, 0.05f, 0.42f), new Vector3(0f, 0.01f, 0.32f), dark);
            Box(weapon, new Vector3(0.025f, 0.025f, 0.28f), new Vector3(0f, 0.015f, 0.62f), dark);
            Box(weapon, new Vector3(0.07f, 0.09f, 0.10f), new Vector3(-0.06f, -0.02f, 0.08f), tan);
            Box(weapon, new Vector3(0.035f, 0.10f, 0.04f), new Vector3(0f, -0.08f, -0.04f), dark);
            Box(weapon, new Vector3(0.04f, 0.08f, 0.14f), new Vector3(0f, 0.02f, -0.16f), dark);
        }
        else
        {
            Box(weapon, new Vector3(0.04f, 0.07f, 0.16f), new Vector3(0f, 0.01f, -0.16f), dark);
            Box(weapon, new Vector3(0.045f, 0.055f, 0.20f), Vector3.zero, dark);
            Box(weapon, new Vector3(0.038f, 0.04f, 0.16f), new Vector3(0f, 0.005f, 0.16f), dark);
            Box(weapon, new Vector3(0.018f, 0.018f, 0.20f), new Vector3(0f, 0.01f, 0.32f), dark);
            Box(weapon, new Vector3(0.028f, 0.11f, 0.035f), new Vector3(0f, -0.07f, 0.02f), tan);
            Box(weapon, new Vector3(0.028f, 0.07f, 0.03f), new Vector3(0f, -0.05f, -0.05f), dark);
            Box(weapon, new Vector3(0.03f, 0.035f, 0.09f), new Vector3(0f, 0.045f, 0.02f), dark);
            Box(weapon, new Vector3(0.022f, 0.028f, 0.05f), new Vector3(0f, 0.07f, 0.03f), dark);
        }

        return weapon;
    }

    void AddBoots(Walker walker, Material boot, bool detailed, bool blocky)
    {
        if (blocky) return;
        Box(walker.footL, new Vector3(0.09f, 0.07f, 0.20f), new Vector3(0f, -0.03f, 0.05f), boot);
        Box(walker.footR, new Vector3(0.09f, 0.07f, 0.20f), new Vector3(0f, -0.03f, 0.05f), boot);
        if (!detailed) return;
        Box(walker.footL, new Vector3(0.095f, 0.02f, 0.22f), new Vector3(0f, -0.065f, 0.05f), boot);
        Box(walker.footR, new Vector3(0.095f, 0.02f, 0.22f), new Vector3(0f, -0.065f, 0.05f), boot);
        Box(walker.shinL, new Vector3(0.08f, 0.10f, 0.08f), new Vector3(0f, -0.38f, 0f), boot);
        Box(walker.shinR, new Vector3(0.08f, 0.10f, 0.08f), new Vector3(0f, -0.38f, 0f), boot);
    }

    void AddShoulder(Transform clavicle, Material cloth, float bulk)
    {
        AddMesh(clavicle, Head(0.55f * bulk), cloth, new Vector3(0f, 0f, 0f));
    }

    static Mesh Torso(float bulk)
    {
        return Loft(new[]
        {
            V(0.115f * bulk, 0.00f, 0.085f * bulk),
            V(0.125f * bulk, 0.08f, 0.090f * bulk),
            V(0.155f * bulk, 0.18f, 0.105f * bulk),
            V(0.185f * bulk, 0.30f, 0.115f * bulk),
            V(0.150f * bulk, 0.40f, 0.095f * bulk)
        }, 14);
    }

    static Mesh Head(float scale)
    {
        const int rows = 8;
        var rings = new Vector3[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            float bell = Mathf.Sin(t * Mathf.PI);
            float rx = (0.055f + bell * 0.035f) * scale;
            float rz = (0.062f + bell * 0.030f) * scale;
            if (t < 0.28f) rx *= 0.9f;
            rings[i] = V(rx, Mathf.Lerp(-0.07f, 0.10f, t) * scale, rz);
        }

        return Loft(rings, 14);
    }

    static Mesh Helmet(float scale)
    {
        const int rows = 6;
        var rings = new Vector3[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            float bell = Mathf.Sin(t * Mathf.PI * 0.92f);
            rings[i] = V((0.09f + bell * 0.02f) * scale, Mathf.Lerp(0f, 0.11f, t) * scale, (0.095f + bell * 0.015f) * scale);
        }

        return Loft(rings, 14);
    }

    static Mesh Brow(float scale)
    {
        return Loft(new[]
        {
            V(0.055f * scale, 0f, 0.012f),
            V(0.06f * scale, 0.015f, 0.018f),
            V(0.04f * scale, 0.03f, 0.01f)
        }, 8);
    }

    static Mesh Ear()
    {
        return Loft(new[]
        {
            V(0.01f, -0.02f, 0.008f),
            V(0.016f, 0f, 0.012f),
            V(0.01f, 0.02f, 0.008f)
        }, 6);
    }

    static Mesh Limb(float length, float radiusTop, float radiusBottom, float bulge, int segments)
    {
        const int rows = 6;
        var rings = new Vector3[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            float radius = Mathf.Lerp(radiusTop, radiusBottom, t);
            radius *= 1f + Mathf.Sin(t * Mathf.PI) * bulge;
            rings[i] = V(radius, -length * t, radius);
        }

        return Loft(rings, segments);
    }

    static Mesh Loft(Vector3[] rings, int segments)
    {
        int rows = rings.Length;
        var vertices = new List<Vector3>(rows * segments + 2);
        var uvs = new List<Vector2>(rows * segments + 2);
        for (int r = 0; r < rows; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                float u = s / (float)segments;
                float angle = u * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * rings[r].x, rings[r].y, Mathf.Sin(angle) * rings[r].z));
                uvs.Add(new Vector2(u, rows == 1 ? 0f : r / (float)(rows - 1)));
            }
        }

        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, rings[0].y, 0f));
        uvs.Add(new Vector2(0.5f, 0f));
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, rings[rows - 1].y, 0f));
        uvs.Add(new Vector2(0.5f, 1f));

        var triangles = new List<int>();
        for (int r = 0; r < rows - 1; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int i0 = r * segments + s;
                int i1 = r * segments + next;
                int i2 = (r + 1) * segments + s;
                int i3 = (r + 1) * segments + next;
                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i1);
                triangles.Add(i2);
                triangles.Add(i3);
            }
        }

        for (int s = 0; s < segments; s++)
        {
            int next = (s + 1) % segments;
            triangles.Add(bottomCenter);
            triangles.Add(next);
            triangles.Add(s);
            int top = (rows - 1) * segments;
            triangles.Add(topCenter);
            triangles.Add(top + s);
            triangles.Add(top + next);
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Vector3[] normals = mesh.normals;
        Vector3 outward = new Vector3(vertices[0].x, 0f, vertices[0].z);
        if (outward.sqrMagnitude > 0.0001f && normals.Length > 0 && Vector3.Dot(normals[0], outward) < 0f)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }

            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
        }

        return mesh;
    }

    static Texture2D MakeCamo()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        Color coyote = new Color(0.58f, 0.50f, 0.34f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = coyote;

        var random = new System.Random(19);
        Color[] spots =
        {
            new Color(0.36f, 0.40f, 0.24f),
            new Color(0.40f, 0.30f, 0.18f),
            new Color(0.73f, 0.66f, 0.46f),
            new Color(0.27f, 0.28f, 0.18f),
            new Color(0.62f, 0.54f, 0.36f)
        };

        for (int blob = 0; blob < 54; blob++)
        {
            int cx = random.Next(size);
            int cy = random.Next(size);
            int radius = 7 + random.Next(blob < 18 ? 22 : 10);
            Color color = spots[random.Next(spots.Length)];
            int radiusSq = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int falloff = x * x + y * y;
                    if (falloff > radiusSq) continue;
                    int px = (cx + x + size) % size;
                    int py = (cy + y + size) % size;
                    float blend = 1f - falloff / (float)radiusSq;
                    int index = py * size + px;
                    pixels[index] = Color.Lerp(pixels[index], color, blend * 0.85f);
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void AddMesh(Transform parent, Mesh mesh, Material material, Vector3 localPosition)
    {
        var part = new GameObject(mesh.name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    Transform Box(Transform parent, Vector3 size, Vector3 localPosition, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = "Box";
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;
        Destroy(part.GetComponent<Collider>());
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return part.transform;
    }

    void LimbBox(Transform bone, float length, Vector3 size, Material material)
    {
        Box(bone, size, new Vector3(0f, -length * 0.5f, 0f), material);
    }

    void Paint(GameObject target, Color color, float smoothness, Texture texture)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = LitMaterial(color, smoothness, texture);
    }

    Material LitMaterial(Color color, float smoothness, Texture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Cull", 0f);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_BaseMap", new Vector2(2.2f, 2.2f));
            material.SetTextureScale("_MainTex", new Vector2(2.2f, 2.2f));
        }

        return material;
    }

    static Transform Bone(Transform parent, string boneName, Vector3 localPosition)
    {
        var bone = new GameObject(boneName);
        bone.transform.SetParent(parent, false);
        bone.transform.localPosition = localPosition;
        return bone.transform;
    }

    static Vector3 V(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    void EnsureGui()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        nameStyle = new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        goldStyle = new GUIStyle(nameStyle)
        {
            normal = { textColor = new Color(0.98f, 0.86f, 0.40f) }
        };
        Texture2D panel = new Texture2D(1, 1);
        panel.SetPixel(0, 0, new Color(0.08f, 0.10f, 0.12f, 0.88f));
        panel.Apply();
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panel } };
    }

    class FigureSpec
    {
        public string title;
        public bool goldLabel;
        public bool detailed;
        public bool blocky;
        public bool useCamo;
        public bool helmet;
        public bool cap;
        public bool sunglasses;
        public bool wearVest;
        public bool gloves;
        public bool bareForearms;
        public bool backpack;
        public bool kneePads;
        public bool machineGun;
        public bool steadyRifle;
        public int pouches;
        public float polish;
        public float bob;
        public float legSwing;
        public float kneeBend;
        public float armSwing;
        public float bulk;
        public float scale;
        public float headScale;
        public Color skin;
        public Color cloth;
        public Color vest;
        public Color pouch;
        public Color boot;
        public Color gear;
    }

#if UNITY_EDITOR
    [MenuItem("RTS/Look/Soldier Uniform Test")]
    static void OpenTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SoldierLookDev.unity");
    }
#endif
}
