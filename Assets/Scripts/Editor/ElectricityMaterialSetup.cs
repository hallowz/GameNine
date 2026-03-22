using UnityEditor;
using UnityEngine;

namespace Voidborne.Editor
{
    public static class ElectricityMaterialSetup
    {
        private const string MatFolder = "Assets/Materials/Electricity";

        [MenuItem("Voidborne/Setup Electricity Materials")]
        public static void SetupMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Electricity");

            var entries = new (string prefabPath, string matName, Color color, float metallic, float smoothness)[]
            {
                ("Assets/Prefabs/Electricity/BurnGenerator.prefab",                    "Elec_BurnGenerator",        new Color(0.55f, 0.27f, 0.10f), 0.4f, 0.40f),
                ("Assets/Prefabs/Electricity/ThermalTap.prefab",                       "Elec_ThermalTap",           new Color(0.65f, 0.30f, 0.08f), 0.3f, 0.45f),
                ("Assets/Prefabs/Electricity/WindRotor.prefab",                        "Elec_WindRotor",            new Color(0.55f, 0.60f, 0.65f), 0.5f, 0.55f),
                ("Assets/Prefabs/Electricity/BatteryBank.prefab",                      "Elec_BatteryBank",          new Color(0.12f, 0.18f, 0.32f), 0.6f, 0.50f),
                ("Assets/Prefabs/Electricity/JunctionBox.prefab",                      "Elec_JunctionBox",          new Color(0.28f, 0.30f, 0.33f), 0.5f, 0.45f),
                ("Assets/Prefabs/Electricity/PoweredLight.prefab",                     "Elec_PoweredLight",         new Color(0.80f, 0.80f, 0.72f), 0.3f, 0.60f),
                ("Assets/Prefabs/Electricity/PoweredDoor.prefab",                      "Elec_PoweredDoor",          new Color(0.22f, 0.28f, 0.38f), 0.6f, 0.50f),
                ("Assets/Prefabs/Building/Electricity/SolarPanel.prefab",              "Elec_SolarPanel",           new Color(0.05f, 0.08f, 0.28f), 0.5f, 0.65f),
                ("Assets/Prefabs/Building/Electricity/VoidFilamentGenerator.prefab",   "Elec_VoidFilament",         new Color(0.15f, 0.05f, 0.30f), 0.7f, 0.60f),
                ("Assets/Prefabs/Building/Electricity/DayNightCycle.prefab",           "Elec_DayNightCycle",        new Color(0.75f, 0.55f, 0.08f), 0.3f, 0.50f),
            };

            int assignedCount = 0;
            foreach (var (prefabPath, matName, color, metallic, smoothness) in entries)
            {
                Material mat = GetOrCreateMaterial(matName, color, metallic, smoothness);
                if (AssignMaterialToPrefab(prefabPath, mat))
                    assignedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ElectricityMaterialSetup] Done. {assignedCount}/{entries.Length} prefabs updated.");
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{MatFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color",     color);
            mat.SetFloat("_Metallic",   metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static bool AssignMaterialToPrefab(string prefabPath, Material mat)
        {
            if (!System.IO.File.Exists(prefabPath))
            {
                Debug.LogWarning($"[ElectricityMaterialSetup] Prefab not found: {prefabPath}");
                return false;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root = scope.prefabContentsRoot;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bool changed = false;

            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        mats[i] = mat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            return changed;
        }
    }
}
