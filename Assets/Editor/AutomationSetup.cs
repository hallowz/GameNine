#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;

namespace Voidborne.Editor
{
    /// <summary>
    /// Editor setup for Volume 8.1 — Conveyor Belts, Tubes and Routing.
    /// Menu: Voidborne > Setup Automation Vol 8.1
    ///
    /// Creates:
    ///   • AutomationTickManager singleton in the scene.
    ///   • AutomationPlacementController + AutomationItemHandler + AutomationTestKit on Player.
    ///   • Prefabs with connector ports and URP-compatible materials.
    ///   • AutomationItem SOs registered in ItemDatabase.
    /// </summary>
    public static class AutomationSetup
    {
        private const string PrefabDir = "Assets/Prefabs/Automation";
        private const string ItemDir   = "Assets/ScriptableObjects/Automation";

        [MenuItem("Voidborne/Setup Automation Vol 8.1")]
        public static void SetupAutomation()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(ItemDir);

            EnsureTickManager();

            string beltPath         = CreateConveyorBeltPrefab();
            string fastBeltPath     = CreateFastBeltPrefab();
            string slopeBeltPath    = CreateSlopeBeltPrefab();
            string splitBeltPath    = CreateSplitBeltPrefab();
            string tubePath         = CreatePneumaticTubePrefab();
            string hopperPath       = CreateHopperPrefab();
            string filterHopperPath = CreateFilterHopperPrefab();
            string sorterPath       = CreateBeltSorterPrefab();
            string valvePath        = CreateOverflowValvePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MakeItem("conveyor_belt",      "Conveyor Belt",
                "Rubber-and-steel belt. Holds 5 items. Items visible on top.",
                beltPath,         0.5f,  5f,  5, "Basic (0.5 s)");
            MakeItem("fast_conveyor_belt", "Fast Conveyor Belt",
                "High-speed belt. Moves items twice as fast. Draws more power.",
                fastBeltPath,     0.2f, 12f,  5, "Fast (0.2 s)");
            MakeItem("slope_belt",         "Slope Belt",
                "Angled conveyor for moving items between vertical levels.",
                slopeBeltPath,    0.5f,  7f,  4, "Basic (0.5 s)");
            MakeItem("split_belt",         "Split Belt",
                "Forks into two output lanes. Alternates, or falls back when one is full.",
                splitBeltPath,    0.5f,  8f,  5, "Basic (0.5 s)");
            MakeItem("pneumatic_tube",     "Pneumatic Tube",
                "Sealed tube. Items travel invisibly at high speed. Ideal for long runs through solid rock.",
                tubePath,         0.15f,10f,  3, "Ultra-fast (0.15 s)");
            MakeItem("hopper",             "Hopper",
                "Connects a container to a belt or tube. Set to Output (push) or Input (pull) mode.",
                hopperPath,       0.5f,  0f,  1, "");
            MakeItem("filter_hopper",      "Filter Hopper",
                "Hopper that only passes items of the configured type.",
                filterHopperPath, 0.5f,  0f,  1, "");
            MakeItem("belt_sorter",        "Belt Sorter",
                "Routes items by type across up to 3 output lanes.",
                sorterPath,       0.5f,  6f,  5, "");
            MakeItem("overflow_valve",     "Overflow Valve",
                "Blocks flow when downstream is full. Opens when space clears.",
                valvePath,        0.5f,  0f,  1, "");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterAllItems();
            SetupPlayer();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AutomationSetup] Vol 8.1 setup complete.");
        }

        // ── Scene setup ────────────────────────────────────────────────────

        private static void EnsureTickManager()
        {
            if (Object.FindObjectOfType<AutomationTickManager>() != null) return;
            new GameObject("AutomationTickManager").AddComponent<AutomationTickManager>();
        }

        private static void SetupPlayer()
        {
            GameObject playerGO = GameObject.FindWithTag("Player")
                ?? Object.FindObjectOfType<PlayerInventory>()?.gameObject;

            if (playerGO == null)
            {
                Debug.LogWarning("[AutomationSetup] Player not found — add components manually.");
                return;
            }

            if (playerGO.GetComponent<AutomationPlacementController>() == null)
                playerGO.AddComponent<AutomationPlacementController>();
            if (playerGO.GetComponent<AutomationItemHandler>() == null)
                playerGO.AddComponent<AutomationItemHandler>();
            Debug.Log("[AutomationSetup] Player components added.");
        }

        // ── Prefab creators ────────────────────────────────────────────────

        private static string CreateConveyorBeltPrefab()
        {
            const string path = PrefabDir + "/ConveyorBelt.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("ConveyorBelt");
            MakePrim(root, "Platform",     PrimitiveType.Cube,
                new Vector3(1f, 0.08f, 1f), Vector3.zero,
                new Color(0.28f, 0.18f, 0.08f));
            MakePrimNoCol(root, "BeltSurface", PrimitiveType.Cube,
                new Vector3(0.85f, 0.04f, 0.92f), new Vector3(0f, 0.06f, 0f),
                new Color(0.15f, 0.10f, 0.04f));
            // Seam strips
            for (int i = -2; i <= 2; i++)
                MakePrimNoCol(root, $"Seam{i}", PrimitiveType.Cube,
                    new Vector3(0.88f, 0.045f, 0.04f), new Vector3(0f, 0.06f, i * 0.18f),
                    new Color(0.25f, 0.16f, 0.06f));
            MakePrimNoCol(root, "RollerFront", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.04f,  0.44f),
                new Color(0.55f, 0.55f, 0.60f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakePrimNoCol(root, "RollerBack", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.04f, -0.44f),
                new Color(0.55f, 0.55f, 0.60f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakePrimNoCol(root, "RailLeft",  PrimitiveType.Cube,
                new Vector3(0.04f, 0.12f, 0.92f), new Vector3(-0.46f, 0.10f, 0f),
                new Color(0.42f, 0.42f, 0.46f));
            MakePrimNoCol(root, "RailRight", PrimitiveType.Cube,
                new Vector3(0.04f, 0.12f, 0.92f), new Vector3( 0.46f, 0.10f, 0f),
                new Color(0.42f, 0.42f, 0.46f));
            // Arrow indicator
            MakePrimNoCol(root, "ArrowIndicator", PrimitiveType.Cube,
                new Vector3(0.14f, 0.02f, 0.28f), new Vector3(0f, 0.10f, 0.08f),
                new Color(0.85f, 0.80f, 0.10f));

            root.AddComponent<ConveyorBelt>();
            AddBoxCollider(root, new Vector3(1f, 0.12f, 1f), new Vector3(0f, 0.06f, 0f));

            // Ports: input back (-Z), output front (+Z)
            AddPort(root, "input",  AutomationConnector.PortType.Input,  new Vector3(0f, 0.12f, -0.52f));
            AddPort(root, "output", AutomationConnector.PortType.Output, new Vector3(0f, 0.12f,  0.52f));

            return SavePrefab(root, path);
        }

        private static string CreateFastBeltPrefab()
        {
            const string path = PrefabDir + "/FastConveyorBelt.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("FastConveyorBelt");
            MakePrim(root, "Platform", PrimitiveType.Cube,
                new Vector3(1f, 0.08f, 1f), Vector3.zero,
                new Color(0.08f, 0.16f, 0.38f));
            MakePrimNoCol(root, "BeltSurface", PrimitiveType.Cube,
                new Vector3(0.85f, 0.04f, 0.92f), new Vector3(0f, 0.06f, 0f),
                new Color(0.06f, 0.12f, 0.28f));
            for (int i = -3; i <= 3; i++)
                MakePrimNoCol(root, $"Seam{i}", PrimitiveType.Cube,
                    new Vector3(0.88f, 0.045f, 0.03f), new Vector3(0f, 0.06f, i * 0.12f),
                    new Color(0.04f, 0.08f, 0.20f));
            MakePrimNoCol(root, "RollerFront", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.04f,  0.44f),
                new Color(0.20f, 0.55f, 1.00f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakePrimNoCol(root, "RollerBack", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.04f, -0.44f),
                new Color(0.20f, 0.55f, 1.00f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakePrimNoCol(root, "RailLeft",  PrimitiveType.Cube,
                new Vector3(0.04f, 0.14f, 0.92f), new Vector3(-0.46f, 0.11f, 0f),
                new Color(0.18f, 0.40f, 0.78f));
            MakePrimNoCol(root, "RailRight", PrimitiveType.Cube,
                new Vector3(0.04f, 0.14f, 0.92f), new Vector3( 0.46f, 0.11f, 0f),
                new Color(0.18f, 0.40f, 0.78f));
            // Double cyan arrows
            MakePrimNoCol(root, "Arrow1", PrimitiveType.Cube,
                new Vector3(0.10f, 0.02f, 0.22f), new Vector3(-0.12f, 0.10f, 0.04f),
                new Color(0.15f, 0.85f, 1.00f));
            MakePrimNoCol(root, "Arrow2", PrimitiveType.Cube,
                new Vector3(0.10f, 0.02f, 0.22f), new Vector3( 0.12f, 0.10f, 0.04f),
                new Color(0.15f, 0.85f, 1.00f));

            root.AddComponent<ConveyorBelt>();
            AddBoxCollider(root, new Vector3(1f, 0.12f, 1f), new Vector3(0f, 0.06f, 0f));
            AddPort(root, "input",  AutomationConnector.PortType.Input,  new Vector3(0f, 0.12f, -0.52f));
            AddPort(root, "output", AutomationConnector.PortType.Output, new Vector3(0f, 0.12f,  0.52f));

            return SavePrefab(root, path);
        }

        private static string CreateSlopeBeltPrefab()
        {
            const string path = PrefabDir + "/SlopeConveyorBelt.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("SlopeConveyorBelt");

            // Angled ramp body
            var ramp = MakePrim(root, "Ramp", PrimitiveType.Cube,
                new Vector3(1f, 0.08f, 1.3f), new Vector3(0f, 0.20f, 0f),
                new Color(0.35f, 0.22f, 0.09f));
            ramp.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

            MakePrimNoCol(root, "BeltSurface", PrimitiveType.Cube,
                new Vector3(0.85f, 0.04f, 1.25f), new Vector3(0f, 0.28f, 0f),
                new Color(0.12f, 0.08f, 0.03f)).transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

            MakePrimNoCol(root, "RollerBottom", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.04f, -0.55f),
                new Color(0.55f, 0.55f, 0.60f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MakePrimNoCol(root, "RollerTop", PrimitiveType.Cylinder,
                new Vector3(0.90f, 0.08f, 0.08f), new Vector3(0f, 0.38f, 0.50f),
                new Color(0.55f, 0.55f, 0.60f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Orange slope stripe
            MakePrimNoCol(root, "SlopeStripe", PrimitiveType.Cube,
                new Vector3(0.12f, 0.02f, 0.60f), new Vector3(0f, 0.29f, 0f),
                new Color(0.90f, 0.55f, 0.08f)).transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

            root.AddComponent<ConveyorBelt>();
            AddBoxCollider(root, new Vector3(1f, 0.45f, 1.3f), new Vector3(0f, 0.22f, 0f));

            // Input at base (low end), output at top (high end)
            AddPort(root, "input",  AutomationConnector.PortType.Input,  new Vector3(0f, 0.04f, -0.60f));
            AddPort(root, "output", AutomationConnector.PortType.Output, new Vector3(0f, 0.40f,  0.55f));

            return SavePrefab(root, path);
        }

        private static string CreateSplitBeltPrefab()
        {
            const string path = PrefabDir + "/SplitConveyorBelt.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("SplitConveyorBelt");

            // Input stem (center, coming from -Z)
            MakePrim(root, "InputLane", PrimitiveType.Cube,
                new Vector3(0.48f, 0.08f, 0.55f), new Vector3(0f, 0f, -0.25f),
                new Color(0.22f, 0.40f, 0.18f));
            // Left fork
            MakePrimNoCol(root, "ForkLeft", PrimitiveType.Cube,
                new Vector3(0.55f, 0.08f, 0.48f), new Vector3(-0.52f, 0f, 0.24f),
                new Color(0.20f, 0.38f, 0.16f));
            // Right fork
            MakePrimNoCol(root, "ForkRight", PrimitiveType.Cube,
                new Vector3(0.55f, 0.08f, 0.48f), new Vector3( 0.52f, 0f, 0.24f),
                new Color(0.20f, 0.38f, 0.16f));
            // Central junction plate
            MakePrimNoCol(root, "Junction", PrimitiveType.Cube,
                new Vector3(1.10f, 0.10f, 0.40f), new Vector3(0f, 0.01f, 0.15f),
                new Color(0.30f, 0.52f, 0.24f));
            // Rollers
            MakePrimNoCol(root, "RollerIn", PrimitiveType.Cylinder,
                new Vector3(0.50f, 0.08f, 0.08f), new Vector3(0f, 0.04f, -0.50f),
                new Color(0.55f, 0.55f, 0.60f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Split arrows (green)
            MakePrimNoCol(root, "ArrowL", PrimitiveType.Cube,
                new Vector3(0.10f, 0.02f, 0.22f), new Vector3(-0.32f, 0.11f, 0.12f),
                new Color(0.60f, 1.00f, 0.40f));
            MakePrimNoCol(root, "ArrowR", PrimitiveType.Cube,
                new Vector3(0.10f, 0.02f, 0.22f), new Vector3( 0.32f, 0.11f, 0.12f),
                new Color(0.60f, 1.00f, 0.40f));

            root.AddComponent<ConveyorBelt>();
            AddBoxCollider(root, new Vector3(1.10f, 0.12f, 1.00f), new Vector3(0f, 0.06f, 0f));

            AddPort(root, "input",            AutomationConnector.PortType.Input,  new Vector3(0f,    0.12f, -0.52f));
            AddPort(root, "output",           AutomationConnector.PortType.Output, new Vector3(-0.80f, 0.12f,  0.24f));
            AddPort(root, "secondary_output", AutomationConnector.PortType.Output, new Vector3( 0.80f, 0.12f,  0.24f));

            return SavePrefab(root, path);
        }

        private static string CreatePneumaticTubePrefab()
        {
            const string path = PrefabDir + "/PneumaticTube.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("PneumaticTube");

            // Outer shell
            var outer = MakePrimNoCol(root, "Shell", PrimitiveType.Cylinder,
                new Vector3(0.42f, 0.50f, 0.42f), Vector3.zero,
                new Color(0.10f, 0.12f, 0.16f));
            outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Inner glow core (brighter colour = visible glow hint)
            var inner = MakePrimNoCol(root, "GlowCore", PrimitiveType.Cylinder,
                new Vector3(0.24f, 0.52f, 0.24f), Vector3.zero,
                new Color(0.05f, 0.50f, 0.80f));
            inner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // End collars
            for (int s = -1; s <= 1; s += 2)
            {
                var collar = MakePrimNoCol(root, s < 0 ? "CollarA" : "CollarB",
                    PrimitiveType.Cylinder,
                    new Vector3(0.48f, 0.07f, 0.48f),
                    new Vector3(0f, 0f, s * 0.46f),
                    new Color(0.18f, 0.50f, 0.78f));
                collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            root.AddComponent<PneumaticTube>();
            AddBoxCollider(root, new Vector3(0.42f, 0.42f, 1.00f), Vector3.zero);

            // Tubes use bidirectional ports — the placement controller handles direction
            AddPort(root, "input",  AutomationConnector.PortType.Input,  new Vector3(0f, 0f, -0.52f));
            AddPort(root, "output", AutomationConnector.PortType.Output, new Vector3(0f, 0f,  0.52f));

            return SavePrefab(root, path);
        }

        private static string CreateHopperPrefab()
        {
            const string path = PrefabDir + "/Hopper.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("Hopper");
            BuildHopperShape(root,
                new Color(0.52f, 0.38f, 0.18f),
                new Color(0.42f, 0.30f, 0.12f),
                new Color(0.32f, 0.22f, 0.08f));

            root.AddComponent<Hopper>();
            AddBoxCollider(root, new Vector3(0.82f, 0.72f, 0.82f), new Vector3(0f, 0.05f, 0f));
            AddPort(root, "belt_port", AutomationConnector.PortType.Input, new Vector3(0f, 0.00f, 0.42f));

            return SavePrefab(root, path);
        }

        private static string CreateFilterHopperPrefab()
        {
            const string path = PrefabDir + "/FilterHopper.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("FilterHopper");
            BuildHopperShape(root,
                new Color(0.72f, 0.58f, 0.08f),
                new Color(0.55f, 0.44f, 0.06f),
                new Color(0.38f, 0.30f, 0.04f));

            // Yellow filter grate bars
            for (int i = -1; i <= 1; i++)
                MakePrimNoCol(root, $"Grate{i}", PrimitiveType.Cube,
                    new Vector3(0.76f, 0.04f, 0.06f),
                    new Vector3(0f, 0.28f, i * 0.18f),
                    new Color(0.90f, 0.78f, 0.05f));

            root.AddComponent<Hopper>();
            AddBoxCollider(root, new Vector3(0.82f, 0.72f, 0.82f), new Vector3(0f, 0.05f, 0f));
            AddPort(root, "belt_port", AutomationConnector.PortType.Input, new Vector3(0f, 0.00f, 0.42f));

            return SavePrefab(root, path);
        }

        private static void BuildHopperShape(GameObject root, Color lip, Color panel, Color spout)
        {
            MakePrim(root, "Lip", PrimitiveType.Cube,
                new Vector3(0.82f, 0.08f, 0.82f), new Vector3(0f, 0.30f, 0f), lip);
            MakePrimNoCol(root, "PanelFront", PrimitiveType.Cube,
                new Vector3(0.82f, 0.50f, 0.06f), new Vector3(0f,  0.06f,  0.30f), panel);
            MakePrimNoCol(root, "PanelBack",  PrimitiveType.Cube,
                new Vector3(0.82f, 0.50f, 0.06f), new Vector3(0f,  0.06f, -0.30f), panel);
            MakePrimNoCol(root, "PanelLeft",  PrimitiveType.Cube,
                new Vector3(0.06f, 0.50f, 0.82f), new Vector3(-0.30f, 0.06f, 0f), panel);
            MakePrimNoCol(root, "PanelRight", PrimitiveType.Cube,
                new Vector3(0.06f, 0.50f, 0.82f), new Vector3( 0.30f, 0.06f, 0f), panel);
            MakePrimNoCol(root, "Interior",   PrimitiveType.Cube,
                new Vector3(0.62f, 0.10f, 0.62f), new Vector3(0f, 0.24f, 0f),
                new Color(0.08f, 0.06f, 0.02f));
            MakePrimNoCol(root, "Spout",      PrimitiveType.Cylinder,
                new Vector3(0.26f, 0.22f, 0.26f), new Vector3(0f, -0.25f, 0f), spout);
        }

        private static string CreateBeltSorterPrefab()
        {
            const string path = PrefabDir + "/BeltSorter.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("BeltSorter");

            MakePrim(root, "JunctionPlate", PrimitiveType.Cube,
                new Vector3(1.00f, 0.10f, 1.00f), Vector3.zero,
                new Color(0.16f, 0.16f, 0.20f));
            // Belt strip (centre input channel)
            MakePrimNoCol(root, "BeltCenter", PrimitiveType.Cube,
                new Vector3(0.28f, 0.04f, 0.88f), new Vector3(0f, 0.07f, 0f),
                new Color(0.10f, 0.10f, 0.14f));
            // Left and right output channels
            MakePrimNoCol(root, "BeltLeft",  PrimitiveType.Cube,
                new Vector3(0.44f, 0.04f, 0.28f), new Vector3(-0.34f, 0.07f, 0.32f),
                new Color(0.10f, 0.10f, 0.14f));
            MakePrimNoCol(root, "BeltRight", PrimitiveType.Cube,
                new Vector3(0.44f, 0.04f, 0.28f), new Vector3( 0.34f, 0.07f, 0.32f),
                new Color(0.10f, 0.10f, 0.14f));
            // Diverter arm parent + bar
            var arm = new GameObject("DiverterArm");
            arm.transform.SetParent(root.transform);
            arm.transform.localPosition = new Vector3(0f, 0.12f, 0.10f);
            MakePrimNoCol(arm, "Bar", PrimitiveType.Cube,
                new Vector3(0.68f, 0.05f, 0.12f), new Vector3(0.24f, 0f, 0f),
                new Color(0.88f, 0.78f, 0.08f));
            MakePrimNoCol(root, "Pivot", PrimitiveType.Sphere,
                new Vector3(0.14f, 0.14f, 0.14f), new Vector3(0f, 0.12f, 0.10f),
                new Color(0.52f, 0.52f, 0.56f));

            root.AddComponent<BeltSorter>();
            AddBoxCollider(root, new Vector3(1f, 0.20f, 1f), new Vector3(0f, 0.10f, 0f));

            AddPort(root, "input",          AutomationConnector.PortType.Input,  new Vector3(0f,    0.12f, -0.52f));
            AddPort(root, "default_output", AutomationConnector.PortType.Output, new Vector3(0f,    0.12f,  0.52f));
            AddPort(root, "left_output",    AutomationConnector.PortType.Output, new Vector3(-0.52f,0.12f,  0.32f));
            AddPort(root, "right_output",   AutomationConnector.PortType.Output, new Vector3( 0.52f,0.12f,  0.32f));

            return SavePrefab(root, path);
        }

        private static string CreateOverflowValvePrefab()
        {
            const string path = PrefabDir + "/OverflowValve.prefab";
            if (AssetExists(path)) return path;

            var root = new GameObject("OverflowValve");

            MakePrim(root, "Housing", PrimitiveType.Cube,
                new Vector3(0.56f, 0.56f, 0.56f), Vector3.zero,
                new Color(0.20f, 0.26f, 0.20f));

            // Valve wheel
            var wheel = new GameObject("ValveWheel");
            wheel.transform.SetParent(root.transform);
            wheel.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            MakePrimNoCol(wheel, "Hub", PrimitiveType.Cylinder,
                new Vector3(0.18f, 0.06f, 0.18f), Vector3.zero,
                new Color(0.62f, 0.66f, 0.62f));
            MakePrimNoCol(wheel, "SpokeH", PrimitiveType.Cube,
                new Vector3(0.56f, 0.04f, 0.06f), Vector3.zero,
                new Color(0.55f, 0.60f, 0.55f));
            MakePrimNoCol(wheel, "SpokeV", PrimitiveType.Cube,
                new Vector3(0.06f, 0.04f, 0.56f), Vector3.zero,
                new Color(0.55f, 0.60f, 0.55f));
            // Rim
            var rim = MakePrimNoCol(wheel, "Rim", PrimitiveType.Cylinder,
                new Vector3(0.60f, 0.04f, 0.60f), Vector3.zero,
                new Color(0.48f, 0.52f, 0.48f));
            MakePrimNoCol(wheel, "RimHole", PrimitiveType.Cylinder,
                new Vector3(0.50f, 0.06f, 0.50f), Vector3.zero,
                new Color(0.20f, 0.26f, 0.20f));

            // Status light (green = open)
            MakePrimNoCol(root, "StatusLight", PrimitiveType.Sphere,
                new Vector3(0.14f, 0.14f, 0.14f), new Vector3(0.30f, 0.30f, 0.28f),
                Color.green);

            // Side ports
            for (int s = -1; s <= 1; s += 2)
            {
                var port = MakePrimNoCol(root, s < 0 ? "PortL" : "PortR",
                    PrimitiveType.Cylinder,
                    new Vector3(0.16f, 0.14f, 0.16f),
                    new Vector3(s * 0.35f, 0f, 0f),
                    new Color(0.32f, 0.38f, 0.32f));
                port.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            root.AddComponent<OverflowValve>();
            AddBoxCollider(root, new Vector3(0.82f, 0.72f, 0.56f), new Vector3(0f, 0.08f, 0f));

            AddPort(root, "input",  AutomationConnector.PortType.Input,  new Vector3(-0.42f, 0f, 0f));
            AddPort(root, "output", AutomationConnector.PortType.Output, new Vector3( 0.42f, 0f, 0f));

            return SavePrefab(root, path);
        }

        // ── Port / Connector helpers ───────────────────────────────────────

        /// <summary>
        /// Adds a child GameObject with AutomationConnector and a small indicator sphere.
        /// </summary>
        private static void AddPort(GameObject parent, string portId,
            AutomationConnector.PortType portType, Vector3 localPos)
        {
            var portGO = new GameObject($"Port_{portId}");
            portGO.transform.SetParent(parent.transform);
            portGO.transform.localPosition = localPos;

            var conn      = portGO.AddComponent<AutomationConnector>();
            conn.portId   = portId;
            conn.portType = portType;

            // Indicator sphere (orange = free, turns green when connected at runtime)
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Indicator";
            sphere.transform.SetParent(portGO.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale    = Vector3.one * 0.12f;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());

            var r   = sphere.GetComponent<Renderer>();
            var mat = MakeMaterial(new Color(1f, 0.55f, 0f)); // orange, matches FreeColor
            r.sharedMaterial = mat;

            conn.indicator = r;
        }

        // ── Item SO creation ───────────────────────────────────────────────

        private static void MakeItem(string id, string displayName, string description,
            string prefabPath, float tickInterval, float powerDraw, int capacity, string speedLabel)
        {
            string assetPath = $"{ItemDir}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<AutomationItem>(assetPath) != null) return;

            var item = ScriptableObject.CreateInstance<AutomationItem>();
            item.itemId         = id;
            item.displayName    = displayName;
            item.description    = description;
            item.itemType       = ItemType.Machine;
            item.maxStackSize   = 64;
            item.weight         = 2f;
            item.tickInterval   = tickInterval;
            item.powerDrawWatts = powerDraw;
            item.capacity       = capacity;
            item.speedLabel     = speedLabel;

            if (!string.IsNullOrEmpty(prefabPath))
                item.devicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            AssetDatabase.CreateAsset(item, assetPath);
        }

        // ── Item Database registration ─────────────────────────────────────

        private static void RegisterAllItems()
        {
            var db = ItemDatabase.GetOrLoad();
            if (db == null) { Debug.LogWarning("[AutomationSetup] ItemDatabase not found."); return; }

            string[] ids = {
                "conveyor_belt", "fast_conveyor_belt", "slope_belt", "split_belt",
                "pneumatic_tube", "hopper", "filter_hopper", "belt_sorter", "overflow_valve"
            };

            bool changed = false;
            foreach (string id in ids)
            {
                var item = AssetDatabase.LoadAssetAtPath<AutomationItem>($"{ItemDir}/{id}.asset");
                if (item == null) continue;

                bool found = false;
                foreach (var e in db.items)
                    if (e != null && e.itemId == id) { found = true; break; }

                if (!found) { db.items.Add(item); changed = true; }
            }

            if (changed) EditorUtility.SetDirty(db);
        }

        // ── Primitive helpers ──────────────────────────────────────────────

        private static GameObject MakePrim(GameObject parent, string name,
            PrimitiveType type, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
            return go;
        }

        private static GameObject MakePrimNoCol(GameObject parent, string name,
            PrimitiveType type, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = MakePrim(parent, name, type, scale, localPos, color);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void AddBoxCollider(GameObject go, Vector3 size, Vector3 center)
        {
            var col = go.AddComponent<BoxCollider>();
            col.size   = size;
            col.center = center;
        }

        /// <summary>Creates a material compatible with both Built-in RP and URP.</summary>
        private static Material MakeMaterial(Color color)
        {
            // Try shaders in order of preference.
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");

            if (sh == null)
            {
                Debug.LogWarning("[AutomationSetup] No suitable shader found — materials may appear pink. " +
                                 "Ensure the project's render pipeline package is imported.");
                return new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard"));
            }

            var mat = new Material(sh);
            // Set colour for both pipelines (URP uses _BaseColor, Built-in uses _Color).
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
            return mat;
        }

        private static string SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        private static bool AssetExists(string assetPath)
            => File.Exists(Path.Combine(Application.dataPath, "../", assetPath).Replace("\\", "/"));
    }
}
#endif
