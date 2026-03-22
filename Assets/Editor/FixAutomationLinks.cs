#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;

namespace Voidborne.Editor
{
    public static class FixAutomationLinks
    {
        private static readonly (string itemId, string prefabPath)[] Map =
        {
            ("conveyor_belt",      "Assets/Prefabs/Automation/ConveyorBelt.prefab"),
            ("fast_conveyor_belt", "Assets/Prefabs/Automation/FastConveyorBelt.prefab"),
            ("slope_belt",         "Assets/Prefabs/Automation/SlopeConveyorBelt.prefab"),
            ("split_belt",         "Assets/Prefabs/Automation/SplitConveyorBelt.prefab"),
            ("pneumatic_tube",     "Assets/Prefabs/Automation/PneumaticTube.prefab"),
            ("hopper",             "Assets/Prefabs/Automation/Hopper.prefab"),
            ("filter_hopper",      "Assets/Prefabs/Automation/FilterHopper.prefab"),
            ("belt_sorter",        "Assets/Prefabs/Automation/BeltSorter.prefab"),
            ("overflow_valve",     "Assets/Prefabs/Automation/OverflowValve.prefab"),
        };

        [MenuItem("Voidborne/Fix Automation Prefab Links")]
        public static void Fix()
        {
            int fixed_ = 0;
            foreach (var (itemId, prefabPath) in Map)
            {
                string soPath = $"Assets/ScriptableObjects/Automation/{itemId}.asset";
                var item = AssetDatabase.LoadAssetAtPath<AutomationItem>(soPath);
                if (item == null) { Debug.LogWarning($"[FixAutomationLinks] SO not found: {soPath}"); continue; }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) { Debug.LogWarning($"[FixAutomationLinks] Prefab not found: {prefabPath}"); continue; }

                item.devicePrefab = prefab;
                EditorUtility.SetDirty(item);
                fixed_++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FixAutomationLinks] Re-linked {fixed_}/{Map.Length} automation items.");
        }
    }
}
#endif
