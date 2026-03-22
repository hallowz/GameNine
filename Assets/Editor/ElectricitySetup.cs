#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Editor
{
    /// <summary>
    /// Editor setup for Volume 7.2 — Electricity & Wiring.
    /// Menu: Voidborne > Setup Electricity Vol 7.2
    ///
    /// Creates:
    ///   • Device prefabs in Assets/Prefabs/Electricity/
    ///   • ElectricityItem / WireItem / PowerProbeItem SOs in Assets/ScriptableObjects/Electricity/
    ///   • PowerNetworkManager singleton in scene
    ///   • ElectricityItemHandler + ElectricityPlacementController + WireController on Player
    ///   • Registers all items in ItemDatabase
    /// </summary>
    public static class ElectricitySetup
    {
        private const string PrefabDir = "Assets/Prefabs/Electricity";
        private const string ItemDir   = "Assets/ScriptableObjects/Electricity";

        [MenuItem("Voidborne/Setup Electricity Vol 7.2")]
        public static void SetupElectricity()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(ItemDir);

            // 1. Scene singleton.
            EnsurePowerNetworkManager();

            // 2. Create / refresh all device prefabs and return their paths.
            string burnPrefab     = CreateBurnGeneratorPrefab();
            string thermalPrefab  = CreateThermalTapPrefab();
            string windPrefab     = CreateWindRotorPrefab();
            string batteryPrefab  = CreateBatteryBankPrefab();
            string jboxPrefab     = CreateJunctionBoxPrefab();
            string lightPrefab    = CreatePoweredLightPrefab();
            string doorPrefab     = CreatePoweredDoorPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3. Create item SOs.
            var burnItem    = MakeElectricityItem("burn_generator",  "Burn Generator",
                "Consumes wood or coal to generate 150W. Loud — attracts enemies. Produces toxic fumes when enclosed.",
                burnPrefab, 150f, 0f);

            var thermalItem = MakeElectricityItem("thermal_tap",     "Thermal Tap",
                "Tap into geothermal heat below Y -256 for 500W of silent, permanent power. Expensive to craft.",
                thermalPrefab, 500f, 0f);

            var windItem    = MakeElectricityItem("wind_rotor",      "Wind Rotor",
                "Generates 50–200W depending on altitude. Higher placement = more output. Pair with batteries to smooth intermittency.",
                windPrefab, 125f, 0f);  // display midpoint

            var batteryItem = MakeElectricityItem("battery_bank",    "Battery Bank",
                "Stores 5,000 Ws of energy. Absorbs surplus power and releases it during deficit. Degrades over charge cycles.",
                batteryPrefab, 0f, 0f);

            var jboxItem    = MakeElectricityItem("junction_box",    "Junction Box",
                "Relay point for long cable runs. Halves transmission loss on its segment. Inspect for live generation/draw readout.",
                jboxPrefab, 0f, 0f);

            var lightDevItem= MakeElectricityItem("powered_light",   "Powered Light",
                "Draws 10W. Emits a warm point light when connected to a powered network.",
                lightPrefab, 0f, 10f);

            var doorDevItem = MakeElectricityItem("powered_door",    "Powered Door",
                "Draws 5W. Interact to open/close when powered. Retains its last state if power is lost.",
                doorPrefab, 0f, 5f);

            var wireItem    = MakeWireItem("wire", "Wire",
                "Click one power device, then another to connect them. Max 20m. Use Power Cable for long runs. Right-click: cancel. X: cut.");

            var cableItem   = MakeElectricityItem("power_cable",     "Power Cable",
                "Building-piece wire for long-distance power runs. 2% energy loss per segment.",
                null, 0f, 0f);

            var probeItem   = MakePowerProbeItem("power_probe", "Power Probe",
                "Scan range: 10m. Hold to view a full live overview of any power network you aim at.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Register in ItemDatabase.
            RegisterItems(burnItem, thermalItem, windItem, batteryItem, jboxItem,
                          lightDevItem, doorDevItem, wireItem, cableItem, probeItem);

            // 5. Add player components.
            SetupPlayerComponents();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ElectricitySetup] Vol 7.2 item setup complete.");
        }

        // ── Scene setup ────────────────────────────────────────────────────

        private static void EnsurePowerNetworkManager()
        {
            if (Object.FindObjectOfType<PowerNetworkManager>() != null) return;
            var go = new GameObject("PowerNetworkManager");
            go.AddComponent<PowerNetworkManager>();
            Debug.Log("[ElectricitySetup] Created PowerNetworkManager.");
        }

        private static void SetupPlayerComponents()
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null)
            {
                // Try finding by PlayerInventory component.
                var pi = Object.FindObjectOfType<PlayerInventory>();
                if (pi != null) playerGO = pi.gameObject;
            }
            if (playerGO == null)
            {
                Debug.LogWarning("[ElectricitySetup] No Player found. Add ElectricityItemHandler, " +
                                 "ElectricityPlacementController, and WireController to Player manually.");
                return;
            }

            if (playerGO.GetComponent<ElectricityPlacementController>() == null)
                playerGO.AddComponent<ElectricityPlacementController>();

            if (playerGO.GetComponent<WireController>() == null)
            {
                var wc = playerGO.AddComponent<WireController>();
                wc.enabled = false; // Disabled until Wire item is selected.
            }

            if (playerGO.GetComponent<ElectricityItemHandler>() == null)
                playerGO.AddComponent<ElectricityItemHandler>();

            Debug.Log("[ElectricitySetup] Added electricity components to Player.");
        }

        // ── Item creation helpers ──────────────────────────────────────────

        private static ElectricityItem MakeElectricityItem(
            string id, string displayName, string description,
            string prefabPath, float outputW, float drawW)
        {
            string assetPath = $"{ItemDir}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ElectricityItem>(assetPath);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<ElectricityItem>();
            item.itemId      = id;
            item.displayName = displayName;
            item.description = description;
            item.itemType    = ItemType.Machine;
            item.maxStackSize= 10;
            item.weight      = 2f;
            item.outputWatts = outputW;
            item.drawWatts   = drawW;

            if (!string.IsNullOrEmpty(prefabPath))
                item.devicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            AssetDatabase.CreateAsset(item, assetPath);
            return item;
        }

        private static WireItem MakeWireItem(string id, string displayName, string description)
        {
            string assetPath = $"{ItemDir}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WireItem>(assetPath);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<WireItem>();
            item.itemId      = id;
            item.displayName = displayName;
            item.description = description;
            item.itemType    = ItemType.Tool;
            item.maxStackSize= 20;
            item.weight      = 0.5f;
            item.maxLength   = 20f;

            AssetDatabase.CreateAsset(item, assetPath);
            return item;
        }

        private static PowerProbeItem MakePowerProbeItem(string id, string displayName, string description)
        {
            string assetPath = $"{ItemDir}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PowerProbeItem>(assetPath);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<PowerProbeItem>();
            item.itemId      = id;
            item.displayName = displayName;
            item.description = description;
            item.itemType    = ItemType.Tool;
            item.maxStackSize= 1;
            item.weight      = 0.3f;
            item.scanRange   = 10f;

            AssetDatabase.CreateAsset(item, assetPath);
            return item;
        }

        private static void RegisterItems(params ItemDefinition[] newItems)
        {
            var db = ItemDatabase.GetOrLoad();
            if (db == null)
            {
                Debug.LogWarning("[ElectricitySetup] ItemDatabase not found. Items not registered.");
                return;
            }

            bool changed = false;
            foreach (var item in newItems)
            {
                if (item == null) continue;
                bool found = false;
                foreach (var existing in db.items)
                    if (existing != null && existing.itemId == item.itemId) { found = true; break; }

                if (!found)
                {
                    db.items.Add(item);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(db);
                Debug.Log("[ElectricitySetup] Registered electricity items in ItemDatabase.");
            }
        }

        // ── Prefab creation ────────────────────────────────────────────────

        private static string CreateBurnGeneratorPrefab()
        {
            string path = $"{PrefabDir}/BurnGenerator.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/")))
                return path;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BurnGenerator";
            go.transform.localScale = new Vector3(1f, 1.5f, 1f);
            SetColor(go, new Color(0.35f, 0.22f, 0.10f));

            var comp = go.AddComponent<BurnGenerator>();
            comp.outputWatts = 150f;
            comp.noiseRadius = 15f;

            var smoke = new GameObject("SmokeParticles");
            smoke.transform.SetParent(go.transform);
            smoke.transform.localPosition = new Vector3(0, 0.9f, 0);
            var ps = smoke.AddComponent<ParticleSystem>();
            comp.smokeParticles = ps;

            var src = go.AddComponent<AudioSource>();
            src.loop = true; src.playOnAwake = false;
            comp.engineSound = src;

            return SavePrefab(go, path);
        }

        private static string CreateThermalTapPrefab()
        {
            string path = $"{PrefabDir}/ThermalTap.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ThermalTap";
            go.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
            SetColor(go, new Color(0.80f, 0.20f, 0.00f));

            var comp = go.AddComponent<ThermalTap>();

            var lightGO = new GameObject("GlowLight");
            lightGO.transform.SetParent(go.transform);
            lightGO.transform.localPosition = Vector3.up * 0.6f;
            var lt = lightGO.AddComponent<Light>();
            lt.color = new Color(1f, 0.4f, 0f); lt.intensity = 2f; lt.range = 6f; lt.enabled = false;
            comp.glowLight = lt;

            return SavePrefab(go, path);
        }

        private static string CreateWindRotorPrefab()
        {
            string path = $"{PrefabDir}/WindRotor.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = new GameObject("WindRotor");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(go.transform);
            body.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            SetColor(body, new Color(0.7f, 0.7f, 0.7f));

            var blades = new GameObject("Blades");
            blades.transform.SetParent(go.transform);
            blades.transform.localPosition = new Vector3(0, 2.2f, 0);
            for (int i = 0; i < 3; i++)
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.transform.SetParent(blades.transform);
                blade.transform.localPosition = new Vector3(
                    Mathf.Cos(i * 120 * Mathf.Deg2Rad) * 1.5f, 0,
                    Mathf.Sin(i * 120 * Mathf.Deg2Rad) * 1.5f);
                blade.transform.localScale = new Vector3(3f, 0.1f, 0.3f);
                blade.transform.LookAt(blades.transform.position);
                SetColor(blade, Color.white);
            }

            var comp = go.AddComponent<WindRotor>();
            comp.bladesTransform = blades.transform;

            return SavePrefab(go, path);
        }

        private static string CreateBatteryBankPrefab()
        {
            string path = $"{PrefabDir}/BatteryBank.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BatteryBank";
            go.transform.localScale = new Vector3(1f, 1f, 0.5f);
            SetColor(go, new Color(0.10f, 0.50f, 0.20f));

            var comp = go.AddComponent<BatteryBank>();
            comp.chargeIndicator = go.GetComponent<Renderer>();
            comp.fullColor  = Color.green;
            comp.emptyColor = Color.red;

            return SavePrefab(go, path);
        }

        private static string CreateJunctionBoxPrefab()
        {
            string path = $"{PrefabDir}/JunctionBox.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "JunctionBox";
            go.transform.localScale = Vector3.one * 0.5f;
            SetColor(go, new Color(0.80f, 0.70f, 0.10f));

            var comp = go.AddComponent<JunctionBox>();

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "StatusLight";
            indicator.transform.SetParent(go.transform);
            indicator.transform.localPosition = new Vector3(0, 0.7f, 0);
            indicator.transform.localScale    = Vector3.one * 0.25f;
            comp.statusLight = indicator.GetComponent<Renderer>();

            return SavePrefab(go, path);
        }

        private static string CreatePoweredLightPrefab()
        {
            string path = $"{PrefabDir}/PoweredLight.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PoweredLight";
            go.transform.localScale = Vector3.one * 0.3f;
            SetColor(go, Color.white);

            var lightGO = new GameObject("PointLight");
            lightGO.transform.SetParent(go.transform);
            var lt = lightGO.AddComponent<Light>();
            lt.type = LightType.Point; lt.intensity = 1.5f; lt.range = 10f;
            lt.color = new Color(1f, 0.95f, 0.8f); lt.enabled = false;

            var comp = go.AddComponent<PoweredLight>();
            comp.pointLight = lt;

            return SavePrefab(go, path);
        }

        private static string CreatePoweredDoorPrefab()
        {
            string path = $"{PrefabDir}/PoweredDoor.prefab";
            if (File.Exists(Path.Combine(Application.dataPath, "../", path).Replace("\\", "/"))) return path;

            var go = new GameObject("PoweredDoor");

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame"; frame.transform.SetParent(go.transform);
            frame.transform.localScale = new Vector3(1.2f, 2.5f, 0.15f);
            SetColor(frame, new Color(0.3f, 0.3f, 0.3f));
            Object.DestroyImmediate(frame.GetComponent<BoxCollider>());

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel"; panel.transform.SetParent(go.transform);
            panel.transform.localPosition = new Vector3(0, 0, 0.05f);
            panel.transform.localScale    = new Vector3(1f, 2.2f, 0.1f);
            SetColor(panel, new Color(0.15f, 0.40f, 0.60f));

            var anim = panel.AddComponent<Animator>();
            var comp = go.AddComponent<PoweredDoor>();
            comp.doorAnimator = anim;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 2.5f, 0.3f);

            return SavePrefab(go, path);
        }

        // ── Utilities ──────────────────────────────────────────────────────

        private static void SetColor(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = new Material(Shader.Find("Standard")) { color = color };
            r.sharedMaterial = mat;
        }

        private static string SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }
    }
}
#endif
