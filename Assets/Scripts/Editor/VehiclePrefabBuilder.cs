using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Voidborne.Vehicles;

public static class VehiclePrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Vehicles";

    // Colors from the spec
    static readonly Color DarkSteel   = new Color(0.25f, 0.25f, 0.28f);
    static readonly Color Gunmetal    = new Color(0.30f, 0.30f, 0.32f);
    static readonly Color Olive       = new Color(0.40f, 0.55f, 0.40f);
    static readonly Color Iron        = new Color(0.35f, 0.35f, 0.38f);
    static readonly Color GlassColor  = new Color(0.40f, 0.60f, 0.70f, 0.5f);
    static readonly Color TireColor   = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color HubColor    = new Color(0.55f, 0.55f, 0.58f);
    static readonly Color SpringColor = new Color(0.50f, 0.50f, 0.52f);
    static readonly Color FrameTube   = new Color(0.30f, 0.30f, 0.32f);
    static readonly Color SeatColor   = new Color(0.20f, 0.12f, 0.08f);
    static readonly Color HandleColor = new Color(0.40f, 0.40f, 0.42f);
    static readonly Color WoodColor   = new Color(0.55f, 0.34f, 0.14f);
    static readonly Color RustOrange  = new Color(0.70f, 0.50f, 0.15f);
    static readonly Color BlueGray    = new Color(0.30f, 0.45f, 0.55f);
    static readonly Color TailBoom    = new Color(0.30f, 0.30f, 0.32f);
    static readonly Color RotorBlade  = new Color(0.50f, 0.50f, 0.52f);

    private const string MatFolder = "Assets/Materials/Vehicles";

    [MenuItem("Tools/Build Vehicle Prefabs")]
    public static void BuildAll()
    {
        // Ensure materials folder exists
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Vehicles");
        }

        // Create shared material assets on disk first (URP-safe)
        CreateMaterialAssets();

        BuildBuggy();
        BuildHauler();
        BuildCycle();
        BuildPushcart();
        BuildDrillRig();
        BuildGyrocopter();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VehiclePrefabBuilder] All 6 vehicle prefabs created in " + PrefabFolder);
    }

    // Cached materials loaded from disk
    static Material _matDarkSteel, _matGunmetal, _matOlive, _matIron, _matGlass;
    static Material _matTire, _matHub, _matSpring, _matFrameTube, _matSeat, _matHandle, _matHeadlight;
    static Material _matWood, _matRustOrange, _matBlueGray, _matTailBoom, _matRotorBlade;

    static void CreateMaterialAssets()
    {
        // Use the default-lit material from a temp primitive as template
        var tmpPrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Shader urpShader = tmpPrim.GetComponent<Renderer>().sharedMaterial.shader;
        Object.DestroyImmediate(tmpPrim);

        _matDarkSteel = CreateOrLoadMat("VehicleDarkSteel", DarkSteel, urpShader, false);
        _matGunmetal  = CreateOrLoadMat("VehicleGunmetal", Gunmetal, urpShader, false);
        _matOlive     = CreateOrLoadMat("VehicleOlive", Olive, urpShader, false);
        _matIron      = CreateOrLoadMat("VehicleIron", Iron, urpShader, false);
        _matGlass     = CreateOrLoadMat("VehicleGlass", GlassColor, urpShader, true);
        _matTire      = CreateOrLoadMat("VehicleTire", TireColor, urpShader, false);
        _matHub       = CreateOrLoadMat("VehicleHub", HubColor, urpShader, false);
        _matSpring    = CreateOrLoadMat("VehicleSpring", SpringColor, urpShader, false);
        _matFrameTube = CreateOrLoadMat("VehicleFrameTube", FrameTube, urpShader, false);
        _matSeat      = CreateOrLoadMat("VehicleSeat", SeatColor, urpShader, false);
        _matHandle    = CreateOrLoadMat("VehicleHandle", HandleColor, urpShader, false);
        _matHeadlight = CreateOrLoadMat("VehicleHeadlight", new Color(0.9f, 0.9f, 0.7f), urpShader, false);
        _matWood       = CreateOrLoadMat("VehicleWood", WoodColor, urpShader, false);
        _matRustOrange = CreateOrLoadMat("VehicleRustOrange", RustOrange, urpShader, false);
        _matBlueGray   = CreateOrLoadMat("VehicleBlueGray", BlueGray, urpShader, false);
        _matTailBoom   = CreateOrLoadMat("VehicleTailBoom", TailBoom, urpShader, false);
        _matRotorBlade = CreateOrLoadMat("VehicleRotorBlade", RotorBlade, urpShader, false);

        AssetDatabase.SaveAssets();
    }

    static Material CreateOrLoadMat(string name, Color color, Shader shader, bool transparent)
    {
        string path = MatFolder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetColor("_BaseColor", color);
            existing.color = color;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var mat = new Material(shader);
        mat.name = name;
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (transparent)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ────────────────────────────────────────────────────────────────
    //  BUGGY  (3.5m x 2.0m x 1.5m)
    // ────────────────────────────────────────────────────────────────

    static void BuildBuggy()
    {
        var root = new GameObject("Buggy_Model");

        // Chassis (always present)
        AddBox(root, "Chassis", new Vector3(0, 0.25f, 0), Vector3.zero,
            new Vector3(1.8f, 0.15f, 3.2f), DarkSteel);

        // Front bumper
        AddBox(root, "FrontBumper", new Vector3(0, 0.3f, 1.65f), Vector3.zero,
            new Vector3(1.8f, 0.25f, 0.15f), Gunmetal);

        // Rear bumper
        AddBox(root, "RearBumper", new Vector3(0, 0.3f, -1.65f), Vector3.zero,
            new Vector3(1.8f, 0.25f, 0.15f), Gunmetal);

        // Hood
        AddBox(root, "Hood", new Vector3(0, 0.92f, 1.0f), new Vector3(-5, 0, 0),
            new Vector3(1.6f, 0.08f, 1.0f), Olive);

        // Engine block
        AddBox(root, "EngineBlock", new Vector3(0, 0.55f, 1.0f), Vector3.zero,
            new Vector3(0.7f, 0.5f, 0.8f), Iron);

        // Exhaust pipe
        AddCylinder(root, "Exhaust", new Vector3(0.5f, 0.65f, 1.0f), Vector3.zero,
            new Vector3(0.08f, 0.15f, 0.08f), Iron);

        // Cockpit floor
        AddBox(root, "CockpitFloor", new Vector3(0, 0.36f, 0), Vector3.zero,
            new Vector3(1.8f, 0.08f, 1.4f), Olive);

        // Cockpit back wall
        AddBox(root, "CockpitBackWall", new Vector3(0, 0.76f, -0.66f), Vector3.zero,
            new Vector3(1.8f, 0.8f, 0.08f), Olive);

        // Windshield
        AddBox(root, "Windshield", new Vector3(0, 1.0f, 0.45f), new Vector3(-25, 0, 0),
            new Vector3(1.8f, 0.6f, 0.05f), GlassColor);

        // Door left
        var doorL = AddBox(root, "DoorLeft", new Vector3(-0.93f, 0.7f, -0.1f), Vector3.zero,
            new Vector3(0.06f, 0.7f, 1.2f), Olive);
        AddBox(doorL, "DoorLeftWindow", new Vector3(0, 0.15f, 0), Vector3.zero,
            new Vector3(0.04f, 0.25f, 0.6f), GlassColor);

        // Door right
        var doorR = AddBox(root, "DoorRight", new Vector3(0.93f, 0.7f, -0.1f), Vector3.zero,
            new Vector3(0.06f, 0.7f, 1.2f), Olive);
        AddBox(doorR, "DoorRightWindow", new Vector3(0, 0.15f, 0), Vector3.zero,
            new Vector3(0.04f, 0.25f, 0.6f), GlassColor);

        // Trunk door
        AddBox(root, "TrunkDoor", new Vector3(0, 0.6f, -1.45f), Vector3.zero,
            new Vector3(1.6f, 0.5f, 0.06f), Olive);

        // Wheels (4)
        AddWheel(root, "Wheel_FL", new Vector3(-0.85f, 0.35f, 1.1f), 0.35f, 0.22f);
        AddWheel(root, "Wheel_FR", new Vector3(0.85f, 0.35f, 1.1f), 0.35f, 0.22f);
        AddWheel(root, "Wheel_RL", new Vector3(-0.85f, 0.35f, -1.1f), 0.35f, 0.22f);
        AddWheel(root, "Wheel_RR", new Vector3(0.85f, 0.35f, -1.1f), 0.35f, 0.22f);

        // Spring visuals (4)
        AddCylinder(root, "Spring_FL", new Vector3(-0.85f, 0.48f, 1.1f), Vector3.zero,
            new Vector3(0.04f, 0.125f, 0.04f), SpringColor);
        AddCylinder(root, "Spring_FR", new Vector3(0.85f, 0.48f, 1.1f), Vector3.zero,
            new Vector3(0.04f, 0.125f, 0.04f), SpringColor);
        AddCylinder(root, "Spring_RL", new Vector3(-0.85f, 0.48f, -1.1f), Vector3.zero,
            new Vector3(0.04f, 0.125f, 0.04f), SpringColor);
        AddCylinder(root, "Spring_RR", new Vector3(0.85f, 0.48f, -1.1f), Vector3.zero,
            new Vector3(0.04f, 0.125f, 0.04f), SpringColor);

        // Physics setup
        SetupWheeledVehicle(root, 600f,
            new Vector3(1.8f, 0.8f, 3.2f), new Vector3(0, 0.5f, 0),
            new[] {
                new WheelDef { pos = new Vector3(-0.85f, 0.35f, 1.1f),  radius = 0.35f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(0.85f, 0.35f, 1.1f),   radius = 0.35f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(-0.85f, 0.35f, -1.1f), radius = 0.35f, isDrive = true,  isSteer = false },
                new WheelDef { pos = new Vector3(0.85f, 0.35f, -1.1f),  radius = 0.35f, isDrive = true,  isSteer = false },
            },
            "BuggyFrame", maxSpeed: 25f);

        SavePrefab(root, "Buggy_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  HAULER  (6.0m x 2.5m x 2.5m)
    // ────────────────────────────────────────────────────────────────

    static void BuildHauler()
    {
        var root = new GameObject("Hauler_Model");

        // Chassis
        AddBox(root, "Chassis", new Vector3(0, 0.3f, 0), Vector3.zero,
            new Vector3(2.4f, 0.2f, 5.8f), DarkSteel);

        // Front bumper
        AddBox(root, "FrontBumper", new Vector3(0, 0.4f, 2.95f), Vector3.zero,
            new Vector3(2.4f, 0.35f, 0.2f), Gunmetal);

        // Rear bumper
        AddBox(root, "RearBumper", new Vector3(0, 0.4f, -2.95f), Vector3.zero,
            new Vector3(2.4f, 0.35f, 0.2f), Gunmetal);

        // Hood
        AddBox(root, "Hood", new Vector3(0, 1.2f, 2.0f), new Vector3(-5, 0, 0),
            new Vector3(2.2f, 0.1f, 1.4f), Olive);

        // Engine (larger)
        AddBox(root, "EngineBlock", new Vector3(0, 0.7f, 2.0f), Vector3.zero,
            new Vector3(1.0f, 0.7f, 1.0f), Iron);

        // Dual exhaust
        AddCylinder(root, "ExhaustL", new Vector3(-0.6f, 0.85f, 2.0f), Vector3.zero,
            new Vector3(0.08f, 0.2f, 0.08f), Iron);
        AddCylinder(root, "ExhaustR", new Vector3(0.6f, 0.85f, 2.0f), Vector3.zero,
            new Vector3(0.08f, 0.2f, 0.08f), Iron);

        // Cab (cockpit)
        AddBox(root, "CabFloor", new Vector3(0, 0.5f, 0.8f), Vector3.zero,
            new Vector3(2.4f, 0.1f, 1.8f), Olive);
        AddBox(root, "CabBackWall", new Vector3(0, 1.1f, -0.05f), Vector3.zero,
            new Vector3(2.4f, 1.1f, 0.1f), Olive);
        AddBox(root, "CabRoof", new Vector3(0, 1.65f, 0.8f), Vector3.zero,
            new Vector3(2.4f, 0.08f, 1.8f), Olive);
        AddBox(root, "CabWindshield", new Vector3(0, 1.35f, 1.65f), new Vector3(-15, 0, 0),
            new Vector3(2.2f, 0.8f, 0.06f), GlassColor);

        // Doors
        AddBox(root, "DoorLeft", new Vector3(-1.25f, 0.95f, 0.8f), Vector3.zero,
            new Vector3(0.06f, 0.9f, 1.5f), Olive);
        AddBox(root, "DoorRight", new Vector3(1.25f, 0.95f, 0.8f), Vector3.zero,
            new Vector3(0.06f, 0.9f, 1.5f), Olive);

        // Open truck bed (floor + 3 walls)
        AddBox(root, "BedFloor", new Vector3(0, 0.5f, -1.5f), Vector3.zero,
            new Vector3(2.3f, 0.12f, 2.0f), DarkSteel);
        AddBox(root, "BedWallLeft", new Vector3(-1.15f, 0.75f, -1.5f), Vector3.zero,
            new Vector3(0.06f, 0.5f, 2.0f), Gunmetal);
        AddBox(root, "BedWallRight", new Vector3(1.15f, 0.75f, -1.5f), Vector3.zero,
            new Vector3(0.06f, 0.5f, 2.0f), Gunmetal);
        AddBox(root, "BedWallBack", new Vector3(0, 0.75f, -2.5f), Vector3.zero,
            new Vector3(2.3f, 0.5f, 0.06f), Gunmetal);

        // 6 wheels (FL/FR at Z=+2, ML/MR at Z=0, RL/RR at Z=-2)
        AddWheel(root, "Wheel_FL", new Vector3(-1.1f, 0.45f, 2.0f), 0.45f, 0.30f);
        AddWheel(root, "Wheel_FR", new Vector3(1.1f, 0.45f, 2.0f), 0.45f, 0.30f);
        AddWheel(root, "Wheel_ML", new Vector3(-1.1f, 0.45f, 0), 0.45f, 0.30f);
        AddWheel(root, "Wheel_MR", new Vector3(1.1f, 0.45f, 0), 0.45f, 0.30f);
        AddWheel(root, "Wheel_RL", new Vector3(-1.1f, 0.45f, -2.0f), 0.45f, 0.30f);
        AddWheel(root, "Wheel_RR", new Vector3(1.1f, 0.45f, -2.0f), 0.45f, 0.30f);

        // Physics setup — 6 wheels, front 2 steer
        SetupWheeledVehicle(root, 1800f,
            new Vector3(2.4f, 1.0f, 5.8f), new Vector3(0, 0.6f, 0),
            new[] {
                new WheelDef { pos = new Vector3(-1.1f, 0.45f, 2.0f),  radius = 0.45f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(1.1f, 0.45f, 2.0f),   radius = 0.45f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(-1.1f, 0.45f, 0),     radius = 0.45f, isDrive = true,  isSteer = false },
                new WheelDef { pos = new Vector3(1.1f, 0.45f, 0),      radius = 0.45f, isDrive = true,  isSteer = false },
                new WheelDef { pos = new Vector3(-1.1f, 0.45f, -2.0f), radius = 0.45f, isDrive = true,  isSteer = false },
                new WheelDef { pos = new Vector3(1.1f, 0.45f, -2.0f),  radius = 0.45f, isDrive = true,  isSteer = false },
            },
            "HaulerFrame", maxSpeed: 18f,
            cameraOffset: new Vector3(0f, 1.5f, 0.6f));

        SavePrefab(root, "Hauler_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  CYCLE  (2.0m x 0.8m x 1.2m)
    // ────────────────────────────────────────────────────────────────

    static void BuildCycle()
    {
        var root = new GameObject("Cycle_Model");

        // Frame tube (tilted cylinder)
        AddCylinder(root, "FrameTube", new Vector3(0, 0.6f, 0), new Vector3(0, 0, -15),
            new Vector3(0.05f, 0.8f, 0.05f), FrameTube);

        // Seat
        AddBox(root, "Seat", new Vector3(0, 0.85f, -0.15f), Vector3.zero,
            new Vector3(0.3f, 0.1f, 0.4f), SeatColor);

        // Engine (small)
        AddBox(root, "Engine", new Vector3(0, 0.45f, 0), Vector3.zero,
            new Vector3(0.25f, 0.3f, 0.35f), Iron);

        // Handlebars (horizontal cylinder)
        AddCylinder(root, "Handlebars", new Vector3(0, 1.05f, 0.55f), new Vector3(0, 0, 90),
            new Vector3(0.03f, 0.3f, 0.03f), HandleColor);

        // Headlight (small cube at front)
        AddBox(root, "Headlight", new Vector3(0, 0.95f, 0.7f), Vector3.zero,
            new Vector3(0.12f, 0.08f, 0.05f), new Color(0.9f, 0.9f, 0.7f));

        // Front fork (thin cylinder)
        AddCylinder(root, "FrontFork", new Vector3(0, 0.65f, 0.55f), new Vector3(-10, 0, 0),
            new Vector3(0.03f, 0.35f, 0.03f), FrameTube);

        // 2 wheels
        AddWheel(root, "Wheel_Front", new Vector3(0, 0.3f, 0.7f), 0.30f, 0.12f);
        AddWheel(root, "Wheel_Rear", new Vector3(0, 0.3f, -0.6f), 0.30f, 0.12f);

        // Physics setup — 2 wheels, front steers, rear drives
        SetupWheeledVehicle(root, 180f,
            new Vector3(0.5f, 0.7f, 1.8f), new Vector3(0, 0.5f, 0),
            new[] {
                new WheelDef { pos = new Vector3(0, 0.3f, 0.7f),  radius = 0.30f, isDrive = false, isSteer = true },
                new WheelDef { pos = new Vector3(0, 0.3f, -0.6f), radius = 0.30f, isDrive = true,  isSteer = false },
            },
            "CycleFrame", maxSpeed: 30f, isCycle: true,
            cameraOffset: new Vector3(0f, 1.4f, -0.2f));

        SavePrefab(root, "Cycle_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  PUSHCART  (1.5m x 1.0m x 1.0m)
    // ────────────────────────────────────────────────────────────────

    static void BuildPushcart()
    {
        var root = new GameObject("Pushcart_Model");

        // Open box — floor
        AddBox(root, "BoxFloor", new Vector3(0, 0.4f, 0), Vector3.zero,
            new Vector3(0.8f, 0.06f, 1.2f), WoodColor);

        // Box walls (4 sides)
        AddBox(root, "WallFront", new Vector3(0, 0.6f, 0.57f), Vector3.zero,
            new Vector3(0.8f, 0.4f, 0.06f), WoodColor);
        AddBox(root, "WallBack", new Vector3(0, 0.6f, -0.57f), Vector3.zero,
            new Vector3(0.8f, 0.4f, 0.06f), WoodColor);
        AddBox(root, "WallLeft", new Vector3(-0.37f, 0.6f, 0), Vector3.zero,
            new Vector3(0.06f, 0.4f, 1.2f), WoodColor);
        AddBox(root, "WallRight", new Vector3(0.37f, 0.6f, 0), Vector3.zero,
            new Vector3(0.06f, 0.4f, 1.2f), WoodColor);

        // Handle bars (2 parallel + crossbar)
        AddCylinder(root, "HandleL", new Vector3(-0.25f, 0.7f, -0.9f), new Vector3(-30, 0, 0),
            new Vector3(0.03f, 0.4f, 0.03f), HandleColor);
        AddCylinder(root, "HandleR", new Vector3(0.25f, 0.7f, -0.9f), new Vector3(-30, 0, 0),
            new Vector3(0.03f, 0.4f, 0.03f), HandleColor);
        AddCylinder(root, "HandleCross", new Vector3(0, 0.9f, -1.1f), new Vector3(0, 0, 90),
            new Vector3(0.03f, 0.25f, 0.03f), HandleColor);

        // 2 wheels at front
        AddWheel(root, "Wheel_L", new Vector3(-0.35f, 0.25f, 0.4f), 0.25f, 0.10f);
        AddWheel(root, "Wheel_R", new Vector3(0.35f, 0.25f, 0.4f), 0.25f, 0.10f);

        // Front support leg
        AddBox(root, "FrontLeg", new Vector3(0, 0.2f, -0.5f), Vector3.zero,
            new Vector3(0.04f, 0.4f, 0.04f), HandleColor);

        // Physics setup
        SetupPushcart(root, 80f,
            new Vector3(0.8f, 0.6f, 1.2f), new Vector3(0, 0.5f, 0),
            "PushcartFrame");

        SavePrefab(root, "Pushcart_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  DRILLRIG  (4.0m x 2.2m x 1.8m)
    // ────────────────────────────────────────────────────────────────

    static void BuildDrillRig()
    {
        var root = new GameObject("DrillRig_Model");

        // Chassis
        AddBox(root, "Chassis", new Vector3(0, 0.25f, 0), Vector3.zero,
            new Vector3(2.0f, 0.2f, 3.8f), Gunmetal);

        // Low cab
        AddBox(root, "CabBody", new Vector3(0, 0.6f, -0.5f), Vector3.zero,
            new Vector3(1.8f, 0.6f, 1.0f), Olive);
        AddBox(root, "CabWindshield", new Vector3(0, 0.85f, 0.0f), new Vector3(-15, 0, 0),
            new Vector3(1.7f, 0.4f, 0.05f), GlassColor);

        // Engine
        AddBox(root, "Engine", new Vector3(0, 0.5f, -1.2f), Vector3.zero,
            new Vector3(0.8f, 0.5f, 0.7f), Iron);

        // Drill arm
        AddCylinder(root, "DrillArm", new Vector3(0, 0.55f, 1.2f), new Vector3(-20, 0, 0),
            new Vector3(0.12f, 0.75f, 0.12f), Gunmetal);

        // Drill head (cone approximation — tapered cylinders)
        AddCylinder(root, "DrillHead", new Vector3(0, 0.3f, 1.9f), new Vector3(-70, 0, 0),
            new Vector3(0.25f, 0.3f, 0.25f), RustOrange);
        AddCylinder(root, "DrillTip", new Vector3(0, 0.15f, 2.15f), new Vector3(-70, 0, 0),
            new Vector3(0.12f, 0.15f, 0.12f), RustOrange);

        // Terrain lamp
        var lampGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lampGO.name = "TerrainLamp";
        lampGO.transform.SetParent(root.transform, false);
        lampGO.transform.localPosition = new Vector3(0, 0.95f, 0.3f);
        lampGO.transform.localScale = Vector3.one * 0.12f;
        Object.DestroyImmediate(lampGO.GetComponent<Collider>());
        SetColor(lampGO, new Color(0.9f, 0.9f, 0.7f));

        // 4 wheels
        AddWheel(root, "Wheel_FL", new Vector3(-0.95f, 0.40f, 1.5f), 0.40f, 0.28f);
        AddWheel(root, "Wheel_FR", new Vector3(0.95f, 0.40f, 1.5f), 0.40f, 0.28f);
        AddWheel(root, "Wheel_RL", new Vector3(-0.95f, 0.40f, -1.5f), 0.40f, 0.28f);
        AddWheel(root, "Wheel_RR", new Vector3(0.95f, 0.40f, -1.5f), 0.40f, 0.28f);

        // Physics setup
        SetupWheeledVehicle(root, 1200f,
            new Vector3(2.0f, 0.9f, 3.8f), new Vector3(0, 0.5f, 0),
            new[] {
                new WheelDef { pos = new Vector3(-0.95f, 0.40f, 1.5f),  radius = 0.40f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(0.95f, 0.40f, 1.5f),   radius = 0.40f, isDrive = true,  isSteer = true },
                new WheelDef { pos = new Vector3(-0.95f, 0.40f, -1.5f), radius = 0.40f, isDrive = true,  isSteer = false },
                new WheelDef { pos = new Vector3(0.95f, 0.40f, -1.5f),  radius = 0.40f, isDrive = true,  isSteer = false },
            },
            "DrillFrame", maxSpeed: 12f,
            cameraOffset: new Vector3(0f, 1.1f, -0.3f));

        SavePrefab(root, "DrillRig_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  GYROCOPTER  (3.0m x 2.0m x 2.5m tall)
    // ────────────────────────────────────────────────────────────────

    static void BuildGyrocopter()
    {
        var root = new GameObject("Gyrocopter_Model");

        // Fuselage
        AddBox(root, "Fuselage", new Vector3(0, 0.5f, 0), Vector3.zero,
            new Vector3(1.0f, 0.6f, 2.5f), BlueGray);

        // Cockpit bubble (cylinder approximation)
        AddCylinder(root, "CockpitBubble", new Vector3(0, 0.9f, 0.3f), Vector3.zero,
            new Vector3(0.9f, 0.25f, 0.9f), GlassColor);

        // Tail boom
        AddCylinder(root, "TailBoom", new Vector3(0, 0.6f, -1.8f), new Vector3(0, 0, 0),
            new Vector3(0.06f, 0.75f, 0.06f), TailBoom);

        // Tail rotor (horizontal)
        AddCylinder(root, "TailRotor", new Vector3(0, 0.7f, -2.5f), new Vector3(0, 0, 90),
            new Vector3(0.01f, 0.25f, 0.01f), RotorBlade);

        // Main rotor mast
        AddCylinder(root, "RotorMast", new Vector3(0, 1.3f, 0), Vector3.zero,
            new Vector3(0.04f, 0.3f, 0.04f), Gunmetal);

        // Main rotor blade
        AddBox(root, "RotorBlade", new Vector3(0, 1.6f, 0), Vector3.zero,
            new Vector3(3.0f, 0.02f, 0.15f), RotorBlade);

        // Landing skids
        AddCylinder(root, "SkidL", new Vector3(-0.5f, 0.1f, 0), new Vector3(90, 0, 0),
            new Vector3(0.03f, 0.9f, 0.03f), HandleColor);
        AddCylinder(root, "SkidR", new Vector3(0.5f, 0.1f, 0), new Vector3(90, 0, 0),
            new Vector3(0.03f, 0.9f, 0.03f), HandleColor);

        // Skid struts
        AddCylinder(root, "StrutFL", new Vector3(-0.5f, 0.3f, 0.5f), new Vector3(0, 0, 15),
            new Vector3(0.025f, 0.25f, 0.025f), HandleColor);
        AddCylinder(root, "StrutFR", new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0, 0, -15),
            new Vector3(0.025f, 0.25f, 0.025f), HandleColor);
        AddCylinder(root, "StrutRL", new Vector3(-0.5f, 0.3f, -0.5f), new Vector3(0, 0, 15),
            new Vector3(0.025f, 0.25f, 0.025f), HandleColor);
        AddCylinder(root, "StrutRR", new Vector3(0.5f, 0.3f, -0.5f), new Vector3(0, 0, -15),
            new Vector3(0.025f, 0.25f, 0.025f), HandleColor);

        // Engine (rear)
        AddBox(root, "Engine", new Vector3(0, 0.5f, -0.8f), Vector3.zero,
            new Vector3(0.5f, 0.4f, 0.5f), Iron);

        // Landing skid colliders (so the copter rests on skids, not the fuselage)
        var skidColL = new GameObject("SkidCollider_L");
        skidColL.transform.SetParent(root.transform, false);
        skidColL.transform.localPosition = new Vector3(-0.5f, 0.05f, 0);
        var scL = skidColL.AddComponent<BoxCollider>();
        scL.size = new Vector3(0.08f, 0.1f, 1.8f);

        var skidColR = new GameObject("SkidCollider_R");
        skidColR.transform.SetParent(root.transform, false);
        skidColR.transform.localPosition = new Vector3(0.5f, 0.05f, 0);
        var scR = skidColR.AddComponent<BoxCollider>();
        scR.size = new Vector3(0.08f, 0.1f, 1.8f);

        // Physics setup — body collider raised above skids so fuselage doesn't embed
        SetupRotorVehicle(root, 400f,
            new Vector3(1.0f, 0.5f, 2.5f), new Vector3(0, 0.6f, 0),
            "RotorFrame");

        SavePrefab(root, "Gyrocopter_Model");
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────

    static GameObject AddBox(GameObject parent, string name, Vector3 localPos, Vector3 localRot, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localRot;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        SetColor(go, color);
        return go;
    }

    static GameObject AddCylinder(GameObject parent, string name, Vector3 localPos, Vector3 localRot, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localRot;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        SetColor(go, color);
        return go;
    }

    static void AddWheel(GameObject parent, string name, Vector3 pos, float radius, float width)
    {
        // Wheel root is NOT rotated — WheelCollider.GetWorldPose expects identity-rotation mesh root.
        // The cylinder primitives inside are rotated Z=90 so their axis aligns with X (sideways).
        var wheelRoot = new GameObject(name);
        wheelRoot.transform.SetParent(parent.transform, false);
        wheelRoot.transform.localPosition = pos;

        // Tire — cylinder rotated so axis = X
        var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tire.name = "Tire";
        tire.transform.SetParent(wheelRoot.transform, false);
        tire.transform.localPosition = Vector3.zero;
        tire.transform.localEulerAngles = new Vector3(0, 0, 90);
        tire.transform.localScale = new Vector3(radius * 2f, width * 0.5f, radius * 2f);
        Object.DestroyImmediate(tire.GetComponent<Collider>());
        SetColor(tire, TireColor);

        // Hub — same rotation
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "Hub";
        hub.transform.SetParent(wheelRoot.transform, false);
        hub.transform.localPosition = Vector3.zero;
        hub.transform.localEulerAngles = new Vector3(0, 0, 90);
        float hubR = radius * 0.43f;
        hub.transform.localScale = new Vector3(hubR * 2f, width * 0.55f, hubR * 2f);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        SetColor(hub, HubColor);
    }

    static Material GetMatForColor(Color color)
    {
        // Match to pre-created material by color
        if (color == DarkSteel)  return _matDarkSteel;
        if (color == Gunmetal)   return _matGunmetal;
        if (color == Olive)      return _matOlive;
        if (color == Iron)       return _matIron;
        if (color == GlassColor) return _matGlass;
        if (color == TireColor)  return _matTire;
        if (color == HubColor)   return _matHub;
        if (color == SpringColor) return _matSpring;
        if (color == FrameTube)  return _matFrameTube;
        if (color == SeatColor)  return _matSeat;
        if (color == HandleColor) return _matHandle;
        if (color == WoodColor)   return _matWood;
        if (color == RustOrange)  return _matRustOrange;
        if (color == BlueGray)    return _matBlueGray;
        if (color == TailBoom)    return _matTailBoom;
        if (color == RotorBlade)  return _matRotorBlade;
        return _matHeadlight; // fallback
    }

    static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.sharedMaterial = GetMatForColor(color);
    }

    static void SavePrefab(GameObject root, string name)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"  Saved prefab: {path}");
    }

    // ────────────────────────────────────────────────────────────────
    //  Physics & vehicle component setup
    // ────────────────────────────────────────────────────────────────

    struct WheelDef
    {
        public Vector3 pos;
        public float radius;
        public bool isDrive;
        public bool isSteer;
    }

    /// <summary>
    /// Adds Rigidbody, collider, WheelColliders, and all vehicle scripts
    /// to a root GameObject so it functions as a driveable wheeled vehicle.
    /// </summary>
    static void SetupWheeledVehicle(GameObject root, float mass, Vector3 colliderSize, Vector3 colliderCenter,
        WheelDef[] wheels, string frameAssetName, float maxSpeed = 15f, bool isCycle = false,
        Vector3? cameraOffset = null)
    {
        // Rigidbody
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;  // slight air/rolling drag so vehicles don't coast forever
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Main body collider
        var box = root.AddComponent<BoxCollider>();
        box.size = colliderSize;
        box.center = colliderCenter;

        // VehicleFrame reference
        var frame = FindFrame(frameAssetName);

        // Required components (added via RequireComponent on VehicleBase, but we add explicitly for clarity)
        var ds = root.AddComponent<VehicleDamageSystem>();
        var fuel = root.AddComponent<VehicleFuel>();
        var assembled = root.AddComponent<AssembledVehicle>();
        if (frame != null)
        {
            var aso = new SerializedObject(assembled);
            aso.FindProperty("_frame").objectReferenceValue = frame;
            aso.ApplyModifiedPropertiesWithoutUndo();
        }

        // WheelColliders + visual tracking
        var wheelColliders = new List<WheelCollider>();
        var wheelMeshes = new List<Transform>();
        var driveFlags = new List<bool>();
        var steerFlags = new List<bool>();

        foreach (var wd in wheels)
        {
            // WheelCollider GO (separate from visual)
            var wcGO = new GameObject("WC_" + wheelColliders.Count);
            wcGO.transform.SetParent(root.transform, false);
            wcGO.transform.localPosition = wd.pos;
            var wc = wcGO.AddComponent<WheelCollider>();
            wc.radius = wd.radius;
            wc.suspensionDistance = 0.3f;
            // Spring and damper will be auto-calculated by WheeledVehicle.ApplySuspensionToWheels()

            wheelColliders.Add(wc);
            driveFlags.Add(wd.isDrive);
            steerFlags.Add(wd.isSteer);

            // Find the visual wheel mesh child by matching position
            Transform meshTransform = FindNearestChild(root.transform, wd.pos);
            wheelMeshes.Add(meshTransform != null ? meshTransform : wcGO.transform);
        }

        // Vehicle script
        WheeledVehicle vehicle;
        if (isCycle)
            vehicle = root.AddComponent<CycleVehicle>();
        else
            vehicle = root.AddComponent<WheeledVehicle>();

        // Set serialized fields via SerializedObject
        var so = new SerializedObject(vehicle);
        SetArray(so, "wheelColliders", wheelColliders.ToArray());
        SetArray(so, "wheelMeshes", wheelMeshes.ToArray());
        SetBoolArray(so, "isDriveWheel", driveFlags.ToArray());
        SetBoolArray(so, "isSteerWheel", steerFlags.ToArray());
        so.FindProperty("baseMaxSpeed").floatValue = maxSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Set per-vehicle camera offset
        SetCameraOffset(vehicle, cameraOffset ?? new Vector3(0f, 1.2f, -0.3f));

        // Supporting components
        root.AddComponent<VehicleInput>();
        root.AddComponent<VehicleCollisionDamage>();
        root.AddComponent<VehicleBody>();

        // Pre-install default engine and suspension so the vehicle is driveable out of the box
        InstallDefaultComponents(assembled, frameAssetName);
    }

    static void SetupRotorVehicle(GameObject root, float mass, Vector3 colliderSize, Vector3 colliderCenter, string frameAssetName,
        Vector3? cameraOffset = null)
    {
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var box = root.AddComponent<BoxCollider>();
        box.size = colliderSize;
        box.center = colliderCenter;

        var frame = FindFrame(frameAssetName);
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();
        var assembled = root.AddComponent<AssembledVehicle>();
        if (frame != null)
        {
            var aso = new SerializedObject(assembled);
            aso.FindProperty("_frame").objectReferenceValue = frame;
            aso.ApplyModifiedPropertiesWithoutUndo();
        }

        // Low-friction physics material for smooth takeoff/landing
        var physMat = new PhysicsMaterial("RotorPhysMat");
        physMat.dynamicFriction = 0.1f;
        physMat.staticFriction = 0.1f;
        physMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        box.material = physMat;

        var rv = root.AddComponent<RotorVehicle>();
        root.AddComponent<VehicleInput>();
        SetCameraOffset(rv, cameraOffset ?? new Vector3(0f, 1.3f, 0.1f));

        // Install default components (engine + rotor)
        InstallDefaultComponents(assembled, frameAssetName);
    }

    static void SetupPushcart(GameObject root, float mass, Vector3 colliderSize, Vector3 colliderCenter, string frameAssetName,
        Vector3? cameraOffset = null)
    {
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var box = root.AddComponent<BoxCollider>();
        box.size = colliderSize;
        box.center = colliderCenter;

        var frame = FindFrame(frameAssetName);
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();
        var assembled = root.AddComponent<AssembledVehicle>();
        if (frame != null)
        {
            var aso = new SerializedObject(assembled);
            aso.FindProperty("_frame").objectReferenceValue = frame;
            aso.ApplyModifiedPropertiesWithoutUndo();
        }

        var pv = root.AddComponent<PushcartVehicle>();
        root.AddComponent<VehicleInput>();
        SetCameraOffset(pv, cameraOffset ?? new Vector3(0f, 1.5f, -1.0f));
    }

    static void SetCameraOffset(VehicleBase vehicle, Vector3 offset)
    {
        var so = new SerializedObject(vehicle);
        var prop = so.FindProperty("driverCameraOffset");
        if (prop != null)
        {
            prop.vector3Value = offset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void InstallDefaultComponents(AssembledVehicle assembled, string frameHint)
    {
        var components = new List<VehicleComponent>();

        // Find appropriate engine
        string engineName = frameHint.Contains("Hauler") || frameHint.Contains("Drill")
            ? "CombustionEngine_Heavy"
            : "CombustionEngine_Basic";
        var engine = FindComponent<EngineComponent>(engineName);
        if (engine != null) components.Add(engine);

        // Find suspension (wheeled vehicles)
        var suspension = FindComponent<SuspensionComponent>("BasicSprings");
        if (suspension != null) components.Add(suspension);

        // Install rotor for rotor frames
        if (frameHint.Contains("Rotor"))
        {
            var rotors = AssetDatabase.FindAssets("t:RotorComponent",
                new[] { "Assets/ScriptableObjects/Vehicles/Components" });
            foreach (var guid in rotors)
            {
                var rotor = AssetDatabase.LoadAssetAtPath<RotorComponent>(AssetDatabase.GUIDToAssetPath(guid));
                if (rotor != null) { components.Add(rotor); break; }
            }
        }

        // Serialize into the _defaultComponents list
        var so = new SerializedObject(assembled);
        var prop = so.FindProperty("_defaultComponents");
        prop.arraySize = components.Count;
        for (int i = 0; i < components.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = components[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T FindComponent<T>(string name) where T : VehicleComponent
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name + " " + name,
            new[] { "Assets/ScriptableObjects/Vehicles/Components" });
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return null;
    }

    static VehicleFrame FindFrame(string name)
    {
        var guids = AssetDatabase.FindAssets("t:VehicleFrame " + name, new[] { "Assets/ScriptableObjects/Vehicles/Frames" });
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<VehicleFrame>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return null;
    }

    static Transform FindNearestChild(Transform parent, Vector3 localPos)
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Transform child in parent)
        {
            float d = Vector3.SqrMagnitude(child.localPosition - localPos);
            if (d < bestDist)
            {
                bestDist = d;
                best = child;
            }
        }
        return bestDist < 0.5f ? best : null;
    }

    static void SetArray<T>(SerializedObject so, string propName, T[] items) where T : Object
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static void SetBoolArray(SerializedObject so, string propName, bool[] items)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).boolValue = items[i];
    }
}
