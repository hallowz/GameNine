using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Vehicles;

/// <summary>
/// Editor utility that creates the four default vehicle prefabs for Volume 9.2.
/// Run via menu: Voidborne/Vehicles/Build Default Prefabs
/// </summary>
public static class VehiclePrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Vehicles";
    private const string FrameFolder  = "Assets/ScriptableObjects/Vehicles/Frames";
    private const string CompFolder   = "Assets/ScriptableObjects/Vehicles/Components";

    [MenuItem("Voidborne/Vehicles/Build Default Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabFolder);

        BuildBuggy();
        BuildCycle();
        BuildHauler();
        BuildDrillRig();
        BuildPushcart();
        BuildGyrocopter();
        BuildVehicleWorkbench();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VehiclePrefabBuilder] All default vehicle prefabs + workbench created in " + PrefabFolder);
    }

    // ─── Buggy ──────────────────────────────────────────────────────────

    private static void BuildBuggy()
    {
        // Root
        var root = new GameObject("Buggy_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 600f;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<WheeledVehicle>();

        // Frame SO
        WireFrame(assembled, "BuggyFrame");

        // Body — low box on 4 sphere wheels; seats 2
        var body = CreatePrimCube(root, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.5f, 3.0f));

        // Driver seat marker
        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.55f, 0.4f);

        // Wheels: FL, FR, RL, RR
        // WheelCollider positions use local coords on root
        float wy    = 0f;
        float wx    = 0.75f;
        float wzF   = 1.1f;
        float wzR   = -1.1f;
        float wRad  = 0.32f;

        var wcFL = CreateWheelCollider(root, "WC_FL", new Vector3(-wx, wy, wzF),  wRad);
        var wcFR = CreateWheelCollider(root, "WC_FR", new Vector3( wx, wy, wzF),  wRad);
        var wcRL = CreateWheelCollider(root, "WC_RL", new Vector3(-wx, wy, wzR), wRad);
        var wcRR = CreateWheelCollider(root, "WC_RR", new Vector3( wx, wy, wzR), wRad);

        // Wheel meshes (sphere primitives)
        var wmFL = CreateWheelMesh(root, "WM_FL", new Vector3(-wx, wy, wzF),  wRad);
        var wmFR = CreateWheelMesh(root, "WM_FR", new Vector3( wx, wy, wzF),  wRad);
        var wmRL = CreateWheelMesh(root, "WM_RL", new Vector3(-wx, wy, wzR), wRad);
        var wmRR = CreateWheelMesh(root, "WM_RR", new Vector3( wx, wy, wzR), wRad);

        // Wire up serialized fields via SerializedObject
        var so = new SerializedObject(vehicle);
        SetWheelArrays(so, new[] { wcFL, wcFR, wcRL, wcRR },
                            new[] { wmFL, wmFR, wmRL, wmRR },
                            driveAll: true,
                            steerIndices: new[] { 0, 1 });
        so.FindProperty("maxSteerAngle").floatValue = 32f;
        so.FindProperty("baseMaxSpeed").floatValue  = 18f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Seat transforms
        var seatsArr = so.FindProperty("seatTransforms");
        if (seatsArr != null)
        {
            seatsArr.arraySize = 2;
            seatsArr.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            // Passenger seat
            var passSeat = new GameObject("PassengerSeat");
            passSeat.transform.SetParent(root.transform);
            passSeat.transform.localPosition = new Vector3(0.5f, 0.55f, -0.3f);
            seatsArr.GetArrayElementAtIndex(1).objectReferenceValue = passSeat.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SavePrefab(root, "Buggy_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Cycle ──────────────────────────────────────────────────────────

    private static void BuildCycle()
    {
        var root = new GameObject("Cycle_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 180f;
        rb.centerOfMass = new Vector3(0f, 0.1f, 0f);

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<CycleVehicle>();

        // Frame SO
        WireFrame(assembled, "CycleFrame");

        // Body — thin upright rectangle
        CreatePrimCube(root, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.8f, 2.0f));

        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        float wRad = 0.35f;
        float wy   = 0f;

        var wcF = CreateWheelCollider(root, "WC_F", new Vector3(0f, wy,  1.0f), wRad);
        var wcR = CreateWheelCollider(root, "WC_R", new Vector3(0f, wy, -1.0f), wRad);

        // For a cycle, "left" = front wheel (local x = -tiny offset) and "right" = rear
        // We use negative x for front to match isLeft check in WheeledVehicle
        wcF.transform.localPosition = new Vector3(-0.01f, wy, 1.0f);  // tiny negative x = "left"
        wcR.transform.localPosition = new Vector3( 0.01f, wy, -1.0f); // tiny positive x = "right"

        var wmF = CreateWheelMesh(root, "WM_F", wcF.transform.localPosition, wRad);
        var wmR = CreateWheelMesh(root, "WM_R", wcR.transform.localPosition, wRad);

        var so = new SerializedObject(vehicle);
        SetWheelArrays(so, new[] { wcF, wcR },
                            new[] { wmF, wmR },
                            driveAll: true,
                            steerIndices: new[] { 0 }); // front steers
        so.FindProperty("maxSteerAngle").floatValue = 35f;
        so.FindProperty("baseMaxSpeed").floatValue  = 28f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var seatsArr = so.FindProperty("seatTransforms");
        if (seatsArr != null)
        {
            seatsArr.arraySize = 1;
            seatsArr.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SavePrefab(root, "Cycle_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Hauler ─────────────────────────────────────────────────────────

    private static void BuildHauler()
    {
        var root = new GameObject("Hauler_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 1800f;
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<WheeledVehicle>();

        // Frame SO
        WireFrame(assembled, "HaulerFrame");

        // Body — tall wide box
        CreatePrimCube(root, "Body", new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 1.4f, 4.5f));

        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 1.0f, 1.5f);

        float wRad = 0.45f;
        float wy   = 0f;
        float wx   = 1.0f;

        // 6 wheels: front, mid, rear axles
        var wcFL = CreateWheelCollider(root, "WC_FL", new Vector3(-wx, wy,  1.8f), wRad);
        var wcFR = CreateWheelCollider(root, "WC_FR", new Vector3( wx, wy,  1.8f), wRad);
        var wcML = CreateWheelCollider(root, "WC_ML", new Vector3(-wx, wy,  0.0f), wRad);
        var wcMR = CreateWheelCollider(root, "WC_MR", new Vector3( wx, wy,  0.0f), wRad);
        var wcRL = CreateWheelCollider(root, "WC_RL", new Vector3(-wx, wy, -1.8f), wRad);
        var wcRR = CreateWheelCollider(root, "WC_RR", new Vector3( wx, wy, -1.8f), wRad);

        var wmFL = CreateWheelMesh(root, "WM_FL", new Vector3(-wx, wy,  1.8f), wRad);
        var wmFR = CreateWheelMesh(root, "WM_FR", new Vector3( wx, wy,  1.8f), wRad);
        var wmML = CreateWheelMesh(root, "WM_ML", new Vector3(-wx, wy,  0.0f), wRad);
        var wmMR = CreateWheelMesh(root, "WM_MR", new Vector3( wx, wy,  0.0f), wRad);
        var wmRL = CreateWheelMesh(root, "WM_RL", new Vector3(-wx, wy, -1.8f), wRad);
        var wmRR = CreateWheelMesh(root, "WM_RR", new Vector3( wx, wy, -1.8f), wRad);

        var so = new SerializedObject(vehicle);
        SetWheelArrays(so,
            new[] { wcFL, wcFR, wcML, wcMR, wcRL, wcRR },
            new[] { wmFL, wmFR, wmML, wmMR, wmRL, wmRR },
            driveAll: true,
            steerIndices: new[] { 0, 1 }); // only front axle steers
        so.FindProperty("maxSteerAngle").floatValue = 22f;
        so.FindProperty("baseMaxSpeed").floatValue  = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var seatsArr = so.FindProperty("seatTransforms");
        if (seatsArr != null)
        {
            seatsArr.arraySize = 1;
            seatsArr.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SavePrefab(root, "Hauler_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Drill Rig ──────────────────────────────────────────────────────

    private static void BuildDrillRig()
    {
        var root = new GameObject("DrillRig_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 500f;
        rb.centerOfMass = new Vector3(0f, -0.2f, 0.5f); // forward heavy (drill)

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<DrillVehicle>();

        // Frame SO
        WireFrame(assembled, "DrillFrame");

        // Body — low-profile box
        CreatePrimCube(root, "Body", new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.45f, 2.8f));

        // Drill head — forward cone (approximate with scaled capsule)
        var drillGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        drillGo.name = "DrillHead";
        drillGo.transform.SetParent(root.transform);
        drillGo.transform.localPosition = new Vector3(0f, 0.15f, 1.85f);
        drillGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        drillGo.transform.localScale    = new Vector3(0.35f, 0.6f, 0.35f);
        RemoveColliders(drillGo);

        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.45f, -0.2f);

        float wRad = 0.28f;
        float wy   = 0f;
        float wx   = 0.7f;

        var wcFL = CreateWheelCollider(root, "WC_FL", new Vector3(-wx, wy,  0.9f), wRad);
        var wcFR = CreateWheelCollider(root, "WC_FR", new Vector3( wx, wy,  0.9f), wRad);
        var wcRL = CreateWheelCollider(root, "WC_RL", new Vector3(-wx, wy, -0.9f), wRad);
        var wcRR = CreateWheelCollider(root, "WC_RR", new Vector3( wx, wy, -0.9f), wRad);

        var wmFL = CreateWheelMesh(root, "WM_FL", new Vector3(-wx, wy,  0.9f), wRad);
        var wmFR = CreateWheelMesh(root, "WM_FR", new Vector3( wx, wy,  0.9f), wRad);
        var wmRL = CreateWheelMesh(root, "WM_RL", new Vector3(-wx, wy, -0.9f), wRad);
        var wmRR = CreateWheelMesh(root, "WM_RR", new Vector3( wx, wy, -0.9f), wRad);

        var so = new SerializedObject(vehicle);
        SetWheelArrays(so, new[] { wcFL, wcFR, wcRL, wcRR },
                            new[] { wmFL, wmFR, wmRL, wmRR },
                            driveAll: true,
                            steerIndices: new[] { 0, 1 });
        so.FindProperty("maxSteerAngle").floatValue = 28f;
        so.FindProperty("baseMaxSpeed").floatValue  = 12f;

        // Wire drill head visual
        var drillHeadProp = so.FindProperty("drillHeadVisual");
        if (drillHeadProp != null)
        {
            drillHeadProp.objectReferenceValue = drillGo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        var seatsArr = so.FindProperty("seatTransforms");
        if (seatsArr != null)
        {
            seatsArr.arraySize = 1;
            seatsArr.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SavePrefab(root, "DrillRig_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Pushcart ────────────────────────────────────────────────────────

    private static void BuildPushcart()
    {
        string path = $"{PrefabFolder}/Pushcart_Default.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("Pushcart_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 80f;
        rb.centerOfMass = new Vector3(0f, -0.1f, 0f);

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<PushcartVehicle>();

        // Frame SO
        var frameSO = AssetDatabase.LoadAssetAtPath<VehicleFrame>($"{FrameFolder}/PushcartFrame.asset");
        if (frameSO != null)
        {
            var soA = new SerializedObject(assembled);
            soA.FindProperty("_frame").objectReferenceValue = frameSO;
            soA.ApplyModifiedPropertiesWithoutUndo();
        }

        // Body — two side rails, cross beams, and flat cargo platform
        CreatePrimCube(root, "LeftRail",  new Vector3(-0.55f, 0.15f, 0f), new Vector3(0.12f, 0.12f, 2.4f));
        CreatePrimCube(root, "RightRail", new Vector3( 0.55f, 0.15f, 0f), new Vector3(0.12f, 0.12f, 2.4f));
        CreatePrimCube(root, "FrontBeam", new Vector3(0f, 0.15f,  1.1f),  new Vector3(1.22f, 0.12f, 0.12f));
        CreatePrimCube(root, "RearBeam",  new Vector3(0f, 0.15f, -1.1f),  new Vector3(1.22f, 0.12f, 0.12f));
        CreatePrimCube(root, "CargoBed",  new Vector3(0f, 0.22f, 0f),     new Vector3(1.1f, 0.06f, 2.2f));

        // Small visual wheels
        float wy = 0.1f, wx = 0.65f, wzF = 0.9f, wzR = -0.9f;
        float wRad = 0.12f;
        CreateWheelMesh(root, "Wheel_FL", new Vector3(-wx, wy, wzF),  wRad);
        CreateWheelMesh(root, "Wheel_FR", new Vector3( wx, wy, wzF),  wRad);
        CreateWheelMesh(root, "Wheel_RL", new Vector3(-wx, wy, wzR), wRad);
        CreateWheelMesh(root, "Wheel_RR", new Vector3( wx, wy, wzR), wRad);

        // Box collider
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.15f, 0f);
        col.size   = new Vector3(1.3f, 0.35f, 2.5f);

        // Push handle (driver stands behind cart)
        var driverSeat = new GameObject("PushHandle");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.6f, -1.4f);

        // Wire up serialized fields
        var cargoBedLarge = AssetDatabase.LoadAssetAtPath<UtilityComponent>($"{CompFolder}/CargoBed_Large.asset");
        var so = new SerializedObject(vehicle);
        var seats = so.FindProperty("seatTransforms");
        if (seats != null)
        {
            seats.arraySize = 1;
            seats.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
        }
        so.FindProperty("baseMaxSpeed").floatValue = 3f;
        var defaultCargoProp = so.FindProperty("defaultCargoBed");
        if (defaultCargoProp != null && cargoBedLarge != null)
            defaultCargoProp.objectReferenceValue = cargoBedLarge;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, "Pushcart_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Gyrocopter ──────────────────────────────────────────────────────

    private static void BuildGyrocopter()
    {
        string path = $"{PrefabFolder}/Gyrocopter_Default.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("Gyrocopter_Default");
        var rb   = root.AddComponent<Rigidbody>();
        rb.mass  = 300f;
        rb.centerOfMass   = new Vector3(0f, 0.1f, 0f);
        rb.angularDamping = 4f;

        var assembled = root.AddComponent<AssembledVehicle>();
        root.AddComponent<VehicleDamageSystem>();
        root.AddComponent<VehicleFuel>();

        var vehicle = root.AddComponent<RotorVehicle>();

        // Frame SO
        var frameSO = AssetDatabase.LoadAssetAtPath<VehicleFrame>($"{FrameFolder}/RotorFrame.asset");
        if (frameSO != null)
        {
            var soA = new SerializedObject(assembled);
            soA.FindProperty("_frame").objectReferenceValue = frameSO;
            soA.ApplyModifiedPropertiesWithoutUndo();
        }

        // Fuselage
        CreatePrimCube(root, "Fuselage", new Vector3(0f, 0.05f, 0f), new Vector3(1.0f, 0.3f, 2.0f));
        // Tail boom
        CreatePrimCube(root, "TailBoom", new Vector3(0f, 0.1f, -1.5f), new Vector3(0.2f, 0.15f, 1.2f));
        // Windshield
        CreatePrimCube(root, "Windshield", new Vector3(0f, 0.35f, 0.7f), new Vector3(0.9f, 0.4f, 0.05f));

        // Rotor discs (visual only, positions match RotorFrame attachment points)
        var rotorTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rotorTop.name = "RotorDisc_Top";
        rotorTop.transform.SetParent(root.transform);
        rotorTop.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        rotorTop.transform.localScale = new Vector3(1.4f, 0.03f, 1.4f);
        RemoveColliders(rotorTop);

        var rotorTail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rotorTail.name = "RotorDisc_Tail";
        rotorTail.transform.SetParent(root.transform);
        rotorTail.transform.localPosition = new Vector3(0f, 0.45f, -1.2f);
        rotorTail.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
        RemoveColliders(rotorTail);

        // Body collider
        var bodyCol = root.AddComponent<BoxCollider>();
        bodyCol.center = new Vector3(0f, 0.05f, 0f);
        bodyCol.size   = new Vector3(1.0f, 0.3f, 2.2f);

        // Seats
        var driverSeat = new GameObject("DriverSeat");
        driverSeat.transform.SetParent(root.transform);
        driverSeat.transform.localPosition = new Vector3(0f, 0.25f, 0.3f);

        var passSeat = new GameObject("PassengerSeat");
        passSeat.transform.SetParent(root.transform);
        passSeat.transform.localPosition = new Vector3(0f, 0.25f, -0.3f);

        var so = new SerializedObject(vehicle);
        var seats = so.FindProperty("seatTransforms");
        if (seats != null)
        {
            seats.arraySize = 2;
            seats.GetArrayElementAtIndex(0).objectReferenceValue = driverSeat.transform;
            seats.GetArrayElementAtIndex(1).objectReferenceValue = passSeat.transform;
        }
        so.FindProperty("baseMaxSpeed").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, "Gyrocopter_Default");
        Object.DestroyImmediate(root);
    }

    // ─── Vehicle Workbench ───────────────────────────────────────────────

    private static void BuildVehicleWorkbench()
    {
        string path = $"{PrefabFolder}/VehicleWorkbench.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("VehicleWorkbench");

        // Visual — sturdy table with tool rack
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.SetParent(root.transform);
        table.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        table.transform.localScale    = new Vector3(2.0f, 0.9f, 1.2f);
        RemoveColliders(table);

        // Tool rack (vertical back panel)
        var rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = "ToolRack";
        rack.transform.SetParent(root.transform);
        rack.transform.localPosition = new Vector3(0f, 1.2f, -0.5f);
        rack.transform.localScale    = new Vector3(2.0f, 0.6f, 0.1f);
        RemoveColliders(rack);

        // Box collider for interaction / physics
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.45f, 0f);
        col.size   = new Vector3(2.0f, 0.9f, 1.2f);

        // VehicleWorkbench component
        root.AddComponent<VehicleWorkbench>();

        SavePrefab(root, "VehicleWorkbench");
        Object.DestroyImmediate(root);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>Sets the _frame field on an AssembledVehicle via SerializedObject.</summary>
    private static void WireFrame(AssembledVehicle assembled, string frameName)
    {
        var frameSO = AssetDatabase.LoadAssetAtPath<VehicleFrame>($"{FrameFolder}/{frameName}.asset");
        if (frameSO == null)
        {
            Debug.LogWarning($"[VehiclePrefabBuilder] {frameName}.asset not found — assign frame manually.");
            return;
        }
        var so = new SerializedObject(assembled);
        so.FindProperty("_frame").objectReferenceValue = frameSO;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreatePrimCube(GameObject parent, string name, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        RemoveColliders(go);
        return go;
    }

    private static WheelCollider CreateWheelCollider(GameObject parent, string name, Vector3 localPos, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        var wc = go.AddComponent<WheelCollider>();
        wc.radius = radius;
        wc.suspensionDistance = 0.3f;
        // Spring/damper are placeholder defaults; WheeledVehicle.ApplySuspensionToWheels()
        // recalculates from mass at runtime.
        var spring = wc.suspensionSpring;
        spring.spring = 5000f;
        spring.damper = 1500f;
        spring.targetPosition = 0.5f;
        wc.suspensionSpring = spring;
        wc.forwardFriction  = BuildFriction(1.0f);
        wc.sidewaysFriction = BuildFriction(1.0f);
        return wc;
    }

    private static WheelFrictionCurve BuildFriction(float stiffness)
    {
        var c = new WheelFrictionCurve();
        c.extremumSlip    = 0.4f;
        c.extremumValue   = 1.0f;
        c.asymptoteSlip   = 0.8f;
        c.asymptoteValue  = 0.5f;
        c.stiffness       = stiffness;
        return c;
    }

    private static Transform CreateWheelMesh(GameObject parent, string name, Vector3 localPos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * (radius * 2f);
        RemoveColliders(go);
        return go.transform;
    }

    private static void RemoveColliders(GameObject go)
    {
        foreach (var c in go.GetComponents<Collider>())
            Object.DestroyImmediate(c);
    }

    /// <summary>Sets wheelColliders, wheelMeshes, isDriveWheel, isSteerWheel arrays on a WheeledVehicle SerializedObject.</summary>
    private static void SetWheelArrays(SerializedObject so, WheelCollider[] wcs, Transform[] wms,
                                        bool driveAll, int[] steerIndices)
    {
        int n = wcs.Length;

        var wcProp = so.FindProperty("wheelColliders");
        if (wcProp != null)
        {
            wcProp.arraySize = n;
            for (int i = 0; i < n; i++)
                wcProp.GetArrayElementAtIndex(i).objectReferenceValue = wcs[i];
        }

        var wmProp = so.FindProperty("wheelMeshes");
        if (wmProp != null)
        {
            wmProp.arraySize = n;
            for (int i = 0; i < n; i++)
                wmProp.GetArrayElementAtIndex(i).objectReferenceValue = wms[i];
        }

        var driveProp = so.FindProperty("isDriveWheel");
        if (driveProp != null)
        {
            driveProp.arraySize = n;
            for (int i = 0; i < n; i++)
                driveProp.GetArrayElementAtIndex(i).boolValue = driveAll;
        }

        var steerProp = so.FindProperty("isSteerWheel");
        if (steerProp != null)
        {
            steerProp.arraySize = n;
            for (int i = 0; i < n; i++)
            {
                bool steers = System.Array.IndexOf(steerIndices, i) >= 0;
                steerProp.GetArrayElementAtIndex(i).boolValue = steers;
            }
        }
    }

    private static void SavePrefab(GameObject go, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Debug.Log($"[VehiclePrefabBuilder] Created {path}");
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
