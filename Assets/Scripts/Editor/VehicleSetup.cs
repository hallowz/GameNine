using UnityEngine;
using UnityEditor;
using Voidborne.Vehicles;

/// <summary>
/// Editor script: Voidborne > Setup Vehicles Vol 9.1
/// Creates all 6 frame assets and 18 starter component assets.
/// </summary>
public class VehicleSetup : MonoBehaviour
{
    [MenuItem("Voidborne/Setup Vehicles Vol 9.1")]
    public static void SetupVehicles()
    {
        CreateFrames();
        CreateComponents();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VehicleSetup] Vol 9.1 assets created successfully.");
    }

    // ─── Frame assets ─────────────────────────────────────────────────

    private static void CreateFrames()
    {
        CreateFrame("PushcartFrame",  FrameType.Pushcart,   1, 60f,  new[] {
            AP("CargoBed",   Vector3.zero,             AttachmentType.Utility) });

        CreateFrame("CycleFrame",     FrameType.Cycle,      1, 80f,  new[] {
            AP("Engine",     new Vector3(0, 0, 0.5f),  AttachmentType.Engine),
            AP("FrontSusp",  new Vector3(0, -0.3f, 1f),AttachmentType.Suspension),
            AP("RearSusp",   new Vector3(0, -0.3f,-1f),AttachmentType.Suspension),
            AP("Glazing",    new Vector3(0, 0.6f, 0.3f),AttachmentType.Glazing) });

        CreateFrame("BuggyFrame",     FrameType.Buggy,      2, 150f, new[] {
            AP("Engine",     new Vector3(0, 0, 1f),    AttachmentType.Engine),
            AP("FLSusp",     new Vector3(-0.6f,-0.3f, 1f), AttachmentType.Suspension),
            AP("FRSusp",     new Vector3( 0.6f,-0.3f, 1f), AttachmentType.Suspension),
            AP("RLSusp",     new Vector3(-0.6f,-0.3f,-1f), AttachmentType.Suspension),
            AP("RRSusp",     new Vector3( 0.6f,-0.3f,-1f), AttachmentType.Suspension),
            AP("Glazing",    new Vector3(0,  0.7f, 0.2f),  AttachmentType.Glazing),
            AP("CargoBed",   new Vector3(0,  0.4f,-0.8f),  AttachmentType.Utility) });

        CreateFrame("HaulerFrame",    FrameType.Hauler,     4, 350f, new[] {
            AP("EngineL",    new Vector3(-0.4f, 0, 1.5f),  AttachmentType.Engine),
            AP("EngineR",    new Vector3( 0.4f, 0, 1.5f),  AttachmentType.Engine),
            AP("FLSusp",     new Vector3(-0.8f,-0.3f, 1.2f), AttachmentType.Suspension),
            AP("FRSusp",     new Vector3( 0.8f,-0.3f, 1.2f), AttachmentType.Suspension),
            AP("MLSusp",     new Vector3(-0.8f,-0.3f, 0),    AttachmentType.Suspension),
            AP("MRSusp",     new Vector3( 0.8f,-0.3f, 0),    AttachmentType.Suspension),
            AP("RLSusp",     new Vector3(-0.8f,-0.3f,-1.2f), AttachmentType.Suspension),
            AP("RRSusp",     new Vector3( 0.8f,-0.3f,-1.2f), AttachmentType.Suspension),
            AP("WindshieldL",new Vector3(-0.4f, 1f, 1.2f),   AttachmentType.Glazing),
            AP("WindshieldR",new Vector3( 0.4f, 1f, 1.2f),   AttachmentType.Glazing),
            AP("CargoBed",   new Vector3(0,   0.5f,-0.8f),   AttachmentType.Utility) });

        CreateFrame("DrillFrame",     FrameType.DrillFrame, 1, 130f, new[] {
            AP("Engine",     new Vector3(0,  0,  0.8f),  AttachmentType.Engine),
            AP("FLSusp",     new Vector3(-0.5f,-0.3f, 0.8f), AttachmentType.Suspension),
            AP("FRSusp",     new Vector3( 0.5f,-0.3f, 0.8f), AttachmentType.Suspension),
            AP("RLSusp",     new Vector3(-0.5f,-0.3f,-0.8f), AttachmentType.Suspension),
            AP("RRSusp",     new Vector3( 0.5f,-0.3f,-0.8f), AttachmentType.Suspension),
            AP("Drill",      new Vector3(0,  0,  1.5f),  AttachmentType.Drill),
            AP("Glazing",    new Vector3(0,  0.5f,0.4f), AttachmentType.Glazing),
            AP("Lamps",      new Vector3(0,  0.4f,1.0f), AttachmentType.Utility) });

        CreateFrame("RotorFrame",     FrameType.RotorFrame, 2, 100f, new[] {
            AP("Engine",     new Vector3(0,  0,  0),     AttachmentType.Engine),
            AP("RotorTop",   new Vector3(0,  0.8f,0),    AttachmentType.Rotor),
            AP("RotorTail",  new Vector3(0,  0.4f,-1.2f),AttachmentType.Rotor),
            AP("Glazing",    new Vector3(0,  0.3f,0.5f), AttachmentType.Glazing),
            AP("Utility",    new Vector3(0, -0.3f,0),    AttachmentType.Utility) });
    }

    private static AttachmentPoint AP(string name, Vector3 pos, AttachmentType type) =>
        new AttachmentPoint { pointName = name, localPosition = pos, attachmentType = type };

    private static void CreateFrame(string assetName, FrameType ftype, int seats, float weight, AttachmentPoint[] points)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Frames/{assetName}.asset";
        EnsureDir("Assets/ScriptableObjects/Vehicles/Frames");

        var existing = AssetDatabase.LoadAssetAtPath<VehicleFrame>(path);
        if (existing != null) return;

        var frame = ScriptableObject.CreateInstance<VehicleFrame>();
        frame.frameName = assetName;
        frame.frameType = ftype;
        frame.maxSeatCount = seats;
        frame.baseWeight = weight;
        frame.attachmentPoints.AddRange(points);
        AssetDatabase.CreateAsset(frame, path);
    }

    // ─── Component assets ──────────────────────────────────────────────

    private static void CreateComponents()
    {
        EnsureDir("Assets/ScriptableObjects/Vehicles/Components");

        // Engines
        CreateEngine("CombustionEngine_Basic", FuelType.Combustion, 80f,  50f, false, 40f);
        CreateEngine("CombustionEngine_Heavy", FuelType.Combustion, 150f, 70f, true,  60f);
        CreateEngine("ElectricMotor_Basic",    FuelType.Electric,   60f,  5f,  false, 20f);

        // Suspension
        CreateSuspension("BasicSprings",       28000f, 3500f, 0.25f,  0f);
        CreateSuspension("HeavyCoil",          45000f, 6000f, 0.35f,  0.1f);
        CreateSuspension("ArticulatedJoints",  35000f, 4200f, 0.3f,   0.2f);

        // Glazing
        CreateGlazing("BasicGlass",       25f, false);
        CreateGlazing("LaminatedPane",    60f, false);
        CreateGlazing("MeshGuard",        0f,  true);

        // Armor
        CreateArmor("ThinSheetArmor",    5f,  20f);
        CreateArmor("ReinforcedPlate",   15f, 60f);

        // Utility
        CreateUtility("CargoBed_Small",       UtilityType.CargoBed, 9,  0f,   0f, 0f, 0f);
        CreateUtility("CargoBed_Large",       UtilityType.CargoBed, 36, 0f,   0f, 0f, 0f);
        CreateUtility("FuelTankExtension",    UtilityType.FuelTankExtension, 0, 50f, 0f, 0f, 30f);
        CreateUtility("WeaponMount",          UtilityType.WeaponMount, 0,    0f,  0f, 0f, 25f);
        CreateUtility("RepairKitRack",        UtilityType.RepairKitRack, 0,  0f,  0f, 0f, 15f);
        CreateUtility("TerrainLampArray",     UtilityType.TerrainLampArray, 0, 0f, 0f, 20f, 20f);
        CreateUtility("SignalDampener",       UtilityType.SignalDampener, 0,  0f,  0.4f, 0f, 30f);
    }

    private static void CreateEngine(string name, FuelType fuel, float power, float noise, bool overheat, float weight)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Components/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<EngineComponent>(path) != null) return;
        var e = ScriptableObject.CreateInstance<EngineComponent>();
        e.componentName = name;
        e.attachmentType = AttachmentType.Engine;
        e.fuelType = fuel;
        e.maxPower = power;
        e.noiseLevel = noise;
        e.overheatsOnSustain = overheat;
        e.weight = weight;
        AssetDatabase.CreateAsset(e, path);
    }

    private static void CreateSuspension(string name, float spring, float damper, float dist, float bonus)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Components/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<SuspensionComponent>(path) != null) return;
        var s = ScriptableObject.CreateInstance<SuspensionComponent>();
        s.componentName = name;
        s.attachmentType = AttachmentType.Suspension;
        s.springStrength = spring;
        s.damper = damper;
        s.suspensionDistance = dist;
        s.terrainHandlingBonus = bonus;
        s.weight = 15f;
        AssetDatabase.CreateAsset(s, path);
    }

    private static void CreateGlazing(string name, float shatter, bool isMesh)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Components/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<GlazingComponent>(path) != null) return;
        var g = ScriptableObject.CreateInstance<GlazingComponent>();
        g.componentName = name;
        g.attachmentType = AttachmentType.Glazing;
        g.shatterThreshold = shatter;
        g.isMesh = isMesh;
        g.weight = isMesh ? 10f : 8f;
        AssetDatabase.CreateAsset(g, path);
    }

    private static void CreateArmor(string name, float resist, float extra)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Components/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<ArmorComponent>(path) != null) return;
        var a = ScriptableObject.CreateInstance<ArmorComponent>();
        a.componentName = name;
        a.attachmentType = AttachmentType.Armor;
        a.damageResistancePerHit = resist;
        a.additionalWeight = extra;
        a.weight = 10f;
        AssetDatabase.CreateAsset(a, path);
    }

    private static void CreateUtility(string name, UtilityType utype, int slots, float fuel,
        float dampener, float lampRadius, float weight)
    {
        string path = $"Assets/ScriptableObjects/Vehicles/Components/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<UtilityComponent>(path) != null) return;
        var u = ScriptableObject.CreateInstance<UtilityComponent>();
        u.componentName = name;
        u.attachmentType = AttachmentType.Utility;
        u.utilityType = utype;
        u.cargoSlots = slots;
        u.fuelExtension = fuel;
        u.detectionRangeReduction = dampener;
        u.lampRadius = lampRadius;
        u.weight = weight;
        AssetDatabase.CreateAsset(u, path);
    }

    private static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
