#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// Editor setup script for Vol 8.2 — Auto-Miner, Drill Head & Drone Ports.
    /// Menu: Voidborne > Setup Automation Vol 8.2
    ///
    /// Creates:
    ///   • AutoMiner prefab    — PowerNode (50W draw) + AutoMiner + DrillHead + status text
    ///   • DronePort prefab    — PowerNode (30W draw) + DronePort + launch arm + status text
    ///   • AutoMiner item SO   (AutomationItem referencing AutoMiner prefab)
    ///   • DronePort item SO   (AutomationItem referencing DronePort prefab)
    ///   Registers both items in ItemDatabase if found.
    /// </summary>
    public static class AutomationSetup82
    {
        private const string PrefabFolder = "Assets/Prefabs/Automation";
        private const string ItemFolder   = "Assets/ScriptableObjects/Items/Automation";

        [MenuItem("Voidborne/Setup Automation Vol 8.2")]
        public static void SetupAutomationVol82()
        {
            EnsureFolders();

            GameObject minerPrefab = CreateAutoMinerPrefab();
            GameObject portPrefab  = CreateDronePortPrefab();

            AutomationItem minerItem = CreateAutomationItem(
                "AutoMinerItem", "Auto-Miner", minerPrefab,
                "Mines ore automatically when placed over a vein and powered.",
                50f, 8);

            AutomationItem portItem = CreateAutomationItem(
                "DronePortItem", "Drone Port", portPrefab,
                "Sends cargo drones to a linked destination port. Requires power.",
                30f, 4);

            RegisterInDatabase(minerItem);
            RegisterInDatabase(portItem);

            AssetDatabase.SaveAssets();
            Debug.Log("[AutomationSetup82] Vol 8.2 assets created successfully.");
            EditorUtility.DisplayDialog("Vol 8.2 Setup",
                "Auto-Miner and Drone Port assets created.\n\n" +
                "Next: assign DronePort.destination in the Inspector to link two ports.",
                "OK");
        }

        // ── Prefab builders ────────────────────────────────────────────────

        private static GameObject CreateAutoMinerPrefab()
        {
            string path = $"{PrefabFolder}/AutoMiner.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[AutomationSetup82] AutoMiner.prefab already exists — skipping.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            // Root
            var root = new GameObject("AutoMiner");

            // Body — visual placeholder
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale    = new Vector3(1.4f, 1.2f, 1.4f);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            SetColor(body, new Color(0.3f, 0.3f, 0.35f));

            // Drill bit
            var drillGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drillGO.name = "DrillBit";
            drillGO.transform.SetParent(root.transform, false);
            drillGO.transform.localScale    = new Vector3(0.3f, 0.8f, 0.3f);
            drillGO.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            SetColor(drillGO, new Color(0.6f, 0.55f, 0.1f));

            // Status light
            var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = "StatusLight";
            light.transform.SetParent(root.transform, false);
            light.transform.localScale    = Vector3.one * 0.15f;
            light.transform.localPosition = new Vector3(0.5f, 1.3f, 0f);
            SetColor(light, Color.green);

            // AutomationConnector — output port for hopper to connect to
            var portGO = new GameObject("buffer_output_port");
            portGO.transform.SetParent(root.transform, false);
            portGO.transform.localPosition = new Vector3(0.9f, 0.5f, 0f);
            var portIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            portIndicator.name = "Indicator";
            portIndicator.transform.SetParent(portGO.transform, false);
            portIndicator.transform.localScale = Vector3.one * 0.12f;
            var conn = portGO.AddComponent<AutomationConnector>();
            conn.portType  = AutomationConnector.PortType.Output;
            conn.portId    = "output";
            conn.indicator = portIndicator.GetComponent<Renderer>();

            // PowerConsumer
            var powerNode = root.AddComponent<PowerConsumer>();
            powerNode.powerDraw   = 50f;
            powerNode.powerOutput = 0f;
            powerNode.priority    = 5;

            // DrillHead
            var drillHead = root.AddComponent<DrillHead>();
            // Use reflection to set private serialized fields via SerializedObject would be
            // complex here; designer assigns in Inspector after prefab creation.

            // AutoMiner
            root.AddComponent<AutoMiner>();

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateDronePortPrefab()
        {
            string path = $"{PrefabFolder}/DronePort.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[AutomationSetup82] DronePort.prefab already exists — skipping.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            var root = new GameObject("DronePort");

            // Landing pad base
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "LandingPad";
            pad.transform.SetParent(root.transform, false);
            pad.transform.localScale    = new Vector3(2f, 0.1f, 2f);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            SetColor(pad, new Color(0.15f, 0.15f, 0.2f));

            // Launch arm
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "LaunchArm";
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale    = new Vector3(0.1f, 1.2f, 0.1f);
            arm.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            SetColor(arm, new Color(0.4f, 0.4f, 0.45f));

            // Status light
            var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = "StatusLight";
            light.transform.SetParent(root.transform, false);
            light.transform.localScale    = Vector3.one * 0.15f;
            light.transform.localPosition = new Vector3(0.1f, 1.35f, 0f);
            SetColor(light, Color.cyan);

            // AutomationConnector — for hopper to load items into the buffer
            var portGO = new GameObject("buffer_port");
            portGO.transform.SetParent(root.transform, false);
            portGO.transform.localPosition = new Vector3(1.1f, 0.3f, 0f);
            var portIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            portIndicator.name = "Indicator";
            portIndicator.transform.SetParent(portGO.transform, false);
            portIndicator.transform.localScale = Vector3.one * 0.12f;
            var conn = portGO.AddComponent<AutomationConnector>();
            conn.portType  = AutomationConnector.PortType.Bidirectional;
            conn.portId    = "buffer_port";
            conn.indicator = portIndicator.GetComponent<Renderer>();

            // PowerConsumer
            var powerNode = root.AddComponent<PowerConsumer>();
            powerNode.powerDraw   = 30f;
            powerNode.powerOutput = 0f;
            powerNode.priority    = 5;

            // DronePort
            root.AddComponent<DronePort>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ── Item SO builder ────────────────────────────────────────────────

        private static AutomationItem CreateAutomationItem(
            string fileName, string displayName, GameObject prefab,
            string description, float powerWatts, int capacity)
        {
            string path = $"{ItemFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AutomationItem>(path);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<AutomationItem>();
            item.itemId        = fileName;
            item.displayName   = displayName;
            item.description   = description;
            item.devicePrefab  = prefab;
            item.powerDrawWatts = powerWatts;
            item.capacity      = capacity;
            item.maxStackSize  = 1;

            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        // ── Database registration ──────────────────────────────────────────

        private static void RegisterInDatabase(AutomationItem item)
        {
            if (item == null) return;
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length == 0) return;

            string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            if (db == null) return;

            foreach (var existing in db.items)
                if (existing != null && existing.itemId == item.itemId) return;

            db.items.Add(item);
            EditorUtility.SetDirty(db);
        }

        // ── Folder helpers ─────────────────────────────────────────────────

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Automation"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Automation");
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Items"))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Items");
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Items/Automation"))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects/Items", "Automation");
        }

        private static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                   Shader.Find("Standard"));
            mat.color = c;
            r.sharedMaterial = mat;
        }
    }
}
#endif
