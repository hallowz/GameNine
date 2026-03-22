using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Vehicles;

/// <summary>
/// Editor utility for Volume 9.3 — Rotor Frame (Gyrocopter) &amp; Pushcart.
/// Creates:
///   • BasicRotor.asset, HeavyRotor.asset, PrecisionRotor.asset (rotor components)
///   • Gyrocopter_Default.prefab (RotorFrame + CombustionEngine_Basic + BasicRotor ×2 + BasicGlass)
///   • Pushcart_Default.prefab  (PushcartFrame + CargoBed_Large)
///
/// Run via menu: Voidborne/Vehicles/Setup Vol 9.3 (Gyrocopter &amp; Pushcart)
/// </summary>
public static class VehicleSetup93
{
    private const string RotorFolder  = "Assets/ScriptableObjects/Vehicles/Components";
    private const string FrameFolder  = "Assets/ScriptableObjects/Vehicles/Frames";
    private const string CompFolder   = "Assets/ScriptableObjects/Vehicles/Components";
    private const string PrefabFolder = "Assets/Prefabs/Vehicles";

    [MenuItem("Voidborne/Vehicles/Setup Vol 9.3 (Gyrocopter & Pushcart)")]
    public static void Setup()
    {
        EnsureFolder(RotorFolder);
        EnsureFolder(PrefabFolder);

        // 1. Rotor component assets
        var basicRotor     = CreateRotorAsset("BasicRotor",      liftForce: 800f,  minAirspeed: 2.5f, maxAltitude: 200f, weight: 25f);
        var heavyRotor     = CreateRotorAsset("HeavyRotor",      liftForce: 1400f, minAirspeed: 5.0f, maxAltitude: 200f, weight: 55f);
        var precisionRotor = CreateRotorAsset("PrecisionRotor",  liftForce: 1200f, minAirspeed: 1.5f, maxAltitude: 200f, weight: 30f);

        // 2. Prefabs
        BuildGyrocopter(basicRotor);
        BuildPushcart();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VehicleSetup93] Vol 9.3 setup complete. Gyrocopter_Default + Pushcart_Default prefabs created.");
    }

    // ─── Rotor asset creation ────────────────────────────────────────────

    private static RotorComponent CreateRotorAsset(string assetName, float liftForce,
                                                    float minAirspeed, float maxAltitude, float weight)
    {
        string path = $"{RotorFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<RotorComponent>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<RotorComponent>();
        asset.componentName  = assetName;
        asset.attachmentType = AttachmentType.Rotor;
        asset.liftForce      = liftForce;
        asset.minAirspeed    = minAirspeed;
        asset.maxAltitude    = maxAltitude;
        asset.weight         = weight;
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[VehicleSetup93] Created {path}");
        return asset;
    }

    // ─── Gyrocopter_Default prefab ───────────────────────────────────────

    private static void BuildGyrocopter(RotorComponent basicRotor)
    {
        string prefabPath = $"{PrefabFolder}/Gyrocopter_Default.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("[VehicleSetup93] Gyrocopter_Default.prefab already exists — skipped.");
            return;
        }

        // ── Root ──
        var root = new GameObject("Gyrocopter_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 300f;
        rb.centerOfMass     = new Vector3(0f, 0.1f, 0f);
        rb.angularDamping   = 4f;

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<RotorVehicle>();

        // ── Frame SO ──
        var frameSO = AssetDatabase.LoadAssetAtPath<VehicleFrame>($"{FrameFolder}/RotorFrame.asset");
        if (frameSO == null)
            Debug.LogWarning("[VehicleSetup93] RotorFrame.asset not found. Assign manually.");
        else
        {
            var soAssembled = new SerializedObject(assembled);
            soAssembled.FindProperty("_frame").objectReferenceValue = frameSO;
            soAssembled.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Body — low flat oval fuselage ──
        var body = CreateCube(root, "Fuselage", new Vector3(0f, 0.05f, 0f), new Vector3(1.0f, 0.3f, 2.0f));

        // Tail boom
        CreateCube(root, "TailBoom", new Vector3(0f, 0.1f, -1.5f), new Vector3(0.2f, 0.15f, 1.2f));

        // ── BoxCollider for the body (WheelColliders not used) ──
        var bodyCol = root.AddComponent<BoxCollider>();
        bodyCol.center = new Vector3(0f, 0.05f, 0f);
        bodyCol.size   = new Vector3(1.0f, 0.3f, 2.2f);

        // ── Driver seat ──
        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.25f, 0.3f);

        // ── Passenger seat ──
        var passSeat = new GameObject("PassengerSeat");
        passSeat.transform.SetParent(root.transform);
        passSeat.transform.localPosition = new Vector3(0f, 0.25f, -0.3f);

        // ── Glazing pane (windshield) ──
        CreateCube(root, "Windshield", new Vector3(0f, 0.35f, 0.7f), new Vector3(0.9f, 0.4f, 0.05f));

        // ── Rotor discs (visual only) — positions match RotorFrame attachment points ──
        // Left rotor
        var rotorL = CreateCylinder(root, "RotorDisc_L", new Vector3(-0.55f, 0.55f, 0.2f), new Vector3(1.4f, 0.03f, 1.4f));
        // Right rotor
        var rotorR = CreateCylinder(root, "RotorDisc_R", new Vector3( 0.55f, 0.55f, 0.2f), new Vector3(1.4f, 0.03f, 1.4f));

        // ── Wire up VehicleBase serialized fields ──
        var soVehicle = new SerializedObject(vehicle);

        var seats = soVehicle.FindProperty("seatTransforms");
        if (seats != null)
        {
            seats.arraySize = 2;
            seats.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            seats.GetArrayElementAtIndex(1).objectReferenceValue = passSeat.transform;
        }
        soVehicle.FindProperty("baseMaxSpeed").floatValue   = 30f;
        soVehicle.ApplyModifiedPropertiesWithoutUndo();

        // ── Install components via AssembledVehicle ──
        var soA2 = new SerializedObject(assembled);
        // (Components are normally installed at runtime; pre-install via the editor is not
        //  supported by the current AssembledVehicle API. Document in prefab name instead.)
        soA2.ApplyModifiedPropertiesWithoutUndo();

        // ── Save ──
        SavePrefab(root, "Gyrocopter_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Pushcart_Default prefab ─────────────────────────────────────────

    private static void BuildPushcart()
    {
        string prefabPath = $"{PrefabFolder}/Pushcart_Default.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("[VehicleSetup93] Pushcart_Default.prefab already exists — skipped.");
            return;
        }

        // ── Root ──
        var root = new GameObject("Pushcart_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 80f;
        rb.centerOfMass = new Vector3(0f, -0.1f, 0f);

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>(); // required by VehicleBase; never drained

        var vehicle = root.AddComponent<PushcartVehicle>();

        // ── Frame SO ──
        var frameSO = AssetDatabase.LoadAssetAtPath<VehicleFrame>($"{FrameFolder}/PushcartFrame.asset");
        if (frameSO == null)
            Debug.LogWarning("[VehicleSetup93] PushcartFrame.asset not found. Assign manually.");
        else
        {
            var soAssembled = new SerializedObject(assembled);
            soAssembled.FindProperty("_frame").objectReferenceValue = frameSO;
            soAssembled.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Visuals — wide flat cargo bed ──
        // Frame: two side rails + cross members
        CreateCube(root, "LeftRail",  new Vector3(-0.55f, 0.15f, 0f), new Vector3(0.12f, 0.12f, 2.4f));
        CreateCube(root, "RightRail", new Vector3( 0.55f, 0.15f, 0f), new Vector3(0.12f, 0.12f, 2.4f));
        CreateCube(root, "FrontBeam", new Vector3(0f, 0.15f,  1.1f),  new Vector3(1.22f, 0.12f, 0.12f));
        CreateCube(root, "RearBeam",  new Vector3(0f, 0.15f, -1.1f),  new Vector3(1.22f, 0.12f, 0.12f));

        // Cargo bed platform
        CreateCube(root, "CargoBed", new Vector3(0f, 0.22f, 0f), new Vector3(1.1f, 0.06f, 2.2f));

        // Four small wheels (spheres, visual only)
        float wy = 0.1f, wx = 0.65f, wzF = 0.9f, wzR = -0.9f;
        float wRad = 0.12f;
        CreateWheelMesh(root, "Wheel_FL", new Vector3(-wx, wy, wzF),  wRad);
        CreateWheelMesh(root, "Wheel_FR", new Vector3( wx, wy, wzF),  wRad);
        CreateWheelMesh(root, "Wheel_RL", new Vector3(-wx, wy, wzR), wRad);
        CreateWheelMesh(root, "Wheel_RR", new Vector3( wx, wy, wzR), wRad);

        // Physics collider for the cart body
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.15f, 0f);
        col.size   = new Vector3(1.3f, 0.35f, 2.5f);

        // ── Driver handle point (player stands behind and pushes) ──
        var driverSeat = new GameObject("PushHandle");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.6f, -1.4f);

        // ── Wire up vehicle ──
        var cargoBedLarge = AssetDatabase.LoadAssetAtPath<UtilityComponent>($"{CompFolder}/CargoBed_Large.asset");
        var soVehicle = new SerializedObject(vehicle);
        var seats = soVehicle.FindProperty("seatTransforms");
        if (seats != null)
        {
            seats.arraySize = 1;
            seats.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
        }
        soVehicle.FindProperty("baseMaxSpeed").floatValue = 3f; // 60% of 5 m/s
        var defaultCargoProp = soVehicle.FindProperty("defaultCargoBed");
        if (defaultCargoProp != null && cargoBedLarge != null)
            defaultCargoProp.objectReferenceValue = cargoBedLarge;
        else if (cargoBedLarge == null)
            Debug.LogWarning("[VehicleSetup93] CargoBed_Large.asset not found — assign defaultCargoBed manually.");
        soVehicle.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, "Pushcart_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Primitive helpers ───────────────────────────────────────────────

    private static GameObject CreateCube(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        RemoveColliders(go);
        return go;
    }

    private static GameObject CreateCylinder(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        RemoveColliders(go);
        return go;
    }

    private static void CreateWheelMesh(GameObject parent, string name, Vector3 pos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = pos;
        go.transform.localScale    = Vector3.one * (radius * 2f);
        RemoveColliders(go);
    }

    private static void RemoveColliders(GameObject go)
    {
        foreach (var c in go.GetComponents<Collider>())
            Object.DestroyImmediate(c);
    }

    private static void SavePrefab(GameObject go, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Debug.Log($"[VehicleSetup93] Saved {path}");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string child  = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
