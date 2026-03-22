#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Voidborne.Automation.Editor
{
    /// <summary>
    /// Editor tool to create colored materials for every automation prefab and
    /// assign them to all renderers, making devices look distinct when placed.
    ///
    /// Also fixes missing component references (beltRenderer, etc.) so devices
    /// behave correctly at runtime.
    ///
    /// Menu: Voidborne > Fix Automation Prefab Materials
    /// </summary>
    public static class AutomationPrefabFixer
    {
        private const string MatFolder    = "Assets/Materials/Automation";
        private const string PrefabFolder = "Assets/Prefabs/Automation";

        // ── Material definitions ───────────────────────────────────────────

        // Each entry: (filename, base color, optional emissive)
        private static readonly (string name, Color color, Color emissive)[] MaterialDefs =
        {
            ("BeltBody",         new Color(0.28f, 0.28f, 0.30f), Color.black),
            ("BeltSurface",      new Color(0.62f, 0.51f, 0.08f), Color.black),
            ("BeltSurfaceFast",  new Color(0.08f, 0.42f, 0.72f), Color.black),
            ("BeltSurfaceSlope", new Color(0.50f, 0.38f, 0.10f), Color.black),
            ("BeltSurfaceSplit", new Color(0.55f, 0.35f, 0.10f), Color.black),
            ("TubeBody",         new Color(0.20f, 0.24f, 0.28f), Color.black),
            ("TubeGlow",         new Color(0.10f, 0.80f, 0.90f), new Color(0f, 0.6f, 0.7f)),
            ("HopperBody",       new Color(0.50f, 0.35f, 0.18f), Color.black),
            ("HopperAccent",     new Color(0.65f, 0.45f, 0.20f), Color.black),
            ("SorterBody",       new Color(0.22f, 0.35f, 0.20f), Color.black),
            ("SorterAccent",     new Color(0.30f, 0.55f, 0.25f), Color.black),
            ("ValveBody",        new Color(0.38f, 0.20f, 0.20f), Color.black),
            ("ValveGate",        new Color(0.20f, 0.70f, 0.25f), new Color(0f, 0.4f, 0.1f)),
            ("MachineBody",      new Color(0.22f, 0.28f, 0.32f), Color.black),
            ("MachineAccent",    new Color(0.30f, 0.38f, 0.42f), Color.black),
            ("IndicatorFree",    new Color(1.00f, 0.55f, 0.00f), new Color(0.6f, 0.3f, 0f)),
        };

        // ── Prefab patches ─────────────────────────────────────────────────

        // (prefabName, (childNameFragment -> materialName)[])
        // Renderers whose child name CONTAINS the fragment get that material.
        // If no fragment matches, the last entry (catch-all with empty fragment) is used.
        private static readonly Dictionary<string, (string frag, string mat)[]> PrefabMats =
            new Dictionary<string, (string, string)[]>
        {
            ["ConveyorBelt"] = new[]
            {
                ("BeltSurface", "BeltSurface"),
                ("",            "BeltBody"),        // fallback
            },
            ["FastConveyorBelt"] = new[]
            {
                ("BeltSurface", "BeltSurfaceFast"),
                ("",            "BeltBody"),
            },
            ["SlopeConveyorBelt"] = new[]
            {
                ("BeltSurface", "BeltSurfaceSlope"),
                ("",            "BeltBody"),
            },
            ["SplitConveyorBelt"] = new[]
            {
                ("BeltSurface", "BeltSurfaceSplit"),
                ("",            "BeltBody"),
            },
            ["PneumaticTube"] = new[]
            {
                ("TubeGlass",  "TubeGlow"),
                ("",           "TubeBody"),
            },
            ["Hopper"] = new[]
            {
                ("Funnel",  "HopperAccent"),
                ("Spout",   "HopperAccent"),
                ("",        "HopperBody"),
            },
            ["FilterHopper"] = new[]
            {
                ("Funnel",   "HopperAccent"),
                ("Filter",   "SorterAccent"),
                ("",         "HopperBody"),
            },
            ["BeltSorter"] = new[]
            {
                ("Arm",    "SorterAccent"),
                ("Divert", "SorterAccent"),
                ("",       "SorterBody"),
            },
            ["OverflowValve"] = new[]
            {
                ("Gate",   "ValveGate"),
                ("",       "ValveBody"),
            },
            ["AutoMiner"]   = new[] { ("",  "MachineBody") },
            ["DronePort"]   = new[] { ("",  "MachineBody") },
            ["ElectricFurnace"]  = new[] { ("",  "MachineBody") },
            ["Grinder"]          = new[] { ("",  "MachineBody") },
            ["Press"]            = new[] { ("",  "MachineBody") },
            ["Assembler"]        = new[] { ("",  "MachineBody") },
            ["CircuitEtcher"]    = new[] { ("",  "MachineBody") },
            ["ComponentPress"]   = new[] { ("",  "MachineBody") },
            ["BatteryFabricator"]= new[] { ("",  "MachineBody") },
            ["ComputerTerminal"] = new[] { ("",  "MachineBody") },
            ["DriveRack"]        = new[] { ("",  "MachineBody") },
        };

        // ── Entry point ────────────────────────────────────────────────────

        [MenuItem("Voidborne/Fix Automation Prefab Materials")]
        public static void Fix()
        {
            EnsureFolder(MatFolder);

            // Create all materials.
            var mats = new Dictionary<string, Material>();
            foreach (var (name, color, emissive) in MaterialDefs)
                mats[name] = GetOrCreateMaterial(name, color, emissive);

            AssetDatabase.SaveAssets();

            // Indicator material used for connector spheres.
            var indicatorMat = mats["IndicatorFree"];

            int patched = 0;
            foreach (var kvp in PrefabMats)
            {
                string prefabPath = $"{PrefabFolder}/{kvp.Key}.prefab";
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[AutomationPrefabFixer] Prefab not found: {prefabPath}");
                    continue;
                }

                // Edit the prefab in-place.
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    PatchPrefabRenderers(contents, kvp.Value, mats, indicatorMat);
                    FixBeltRenderer(contents, kvp.Key);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    patched++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Patched {patched} prefabs with proper materials.";
            Debug.Log($"[AutomationPrefabFixer] {msg}");
            EditorUtility.DisplayDialog("Automation Prefab Fixer", msg, "OK");
        }

        // ── Renderer patching ──────────────────────────────────────────────

        private static void PatchPrefabRenderers(
            GameObject root,
            (string frag, string mat)[] rules,
            Dictionary<string, Material> mats,
            Material indicatorMat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // Connector indicator spheres get the orange indicator material.
                if (r.GetComponentInParent<AutomationConnector>() != null)
                {
                    r.sharedMaterial = indicatorMat;
                    continue;
                }

                // Match the first rule whose fragment appears in the GO name.
                string matName = null;
                foreach (var (frag, mat) in rules)
                {
                    if (frag == "" || r.gameObject.name.Contains(frag))
                    {
                        matName = mat;
                        break;
                    }
                }

                if (matName != null && mats.TryGetValue(matName, out var m))
                    r.sharedMaterial = m;
            }
        }

        // ── Belt-renderer fix ──────────────────────────────────────────────

        private static void FixBeltRenderer(GameObject root, string prefabName)
        {
            var belt = root.GetComponent<ConveyorBelt>();
            if (belt == null) return;

            // Find a child renderer that represents the moving belt surface.
            Renderer surfRenderer = null;
            var t = root.transform.Find("BeltSurface");
            if (t != null) surfRenderer = t.GetComponent<Renderer>();

            // Fallback: first non-connector renderer.
            if (surfRenderer == null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponentInParent<AutomationConnector>() != null) continue;
                    surfRenderer = r;
                    break;
                }
            }

            if (surfRenderer != null)
            {
                var so = new SerializedObject(belt);
                var prop = so.FindProperty("beltRenderer");
                if (prop != null)
                {
                    prop.objectReferenceValue = surfRenderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // ── Material factory ───────────────────────────────────────────────

        private static Material GetOrCreateMaterial(string name, Color color, Color emissive)
        {
            string path = $"{MatFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Update color in case it changed.
                ApplyColorToMaterial(existing, color, emissive);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            var mat = new Material(sh ?? Shader.Find("Diffuse")) { name = name };
            ApplyColorToMaterial(mat, color, emissive);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void ApplyColorToMaterial(Material mat, Color color, Color emissive)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);

            if (emissive != Color.black)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", emissive);
                    mat.EnableKeyword("_EMISSION");
                }
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
        }

        // ── Folder helpers ─────────────────────────────────────────────────

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
