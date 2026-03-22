#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Editor setup script for Vol 8.1 — Conveyor Belts, Tubes & Routing.
    /// Menu: Voidborne > Setup Automation Vol 8.1
    ///
    /// Creates AutomationItem SOs for all 9 Vol 8.1 devices and registers
    /// them in the ItemDatabase.  Prefabs are expected to already exist at
    /// Assets/Prefabs/Automation/ (created when the belt/tube MonoBehaviours
    /// were first implemented).
    /// </summary>
    public static class AutomationSetup81
    {
        private const string ItemFolder = "Assets/ScriptableObjects/Items/Automation";

        private static readonly (string itemId, string displayName, string prefabName, string description, float tickInterval, string speedLabel, int capacity)[] Entries =
        {
            ("conveyor_belt",      "Conveyor Belt",      "ConveyorBelt",
             "Moves items in one direction at a steady pace. Connect hoppers or other belts.",
             0.5f, "Basic", 4),

            ("fast_conveyor_belt", "Fast Conveyor Belt", "FastConveyorBelt",
             "Twice the speed of a basic belt. Uses a blue indicator stripe.",
             0.25f, "Fast", 4),

            ("slope_belt",         "Slope Belt",         "SlopeConveyorBelt",
             "Carries items up or down a slope between two height levels.",
             0.5f, "Basic", 4),

            ("split_belt",         "Split Belt",         "SplitConveyorBelt",
             "Splits one input into two outputs, alternating items between forks.",
             0.5f, "Basic", 2),

            ("pneumatic_tube",     "Pneumatic Tube",     "PneumaticTube",
             "Ultra-fast item transit. Items pulse along at 0.15 s/tile with a cyan glow.",
             0.15f, "Ultra-fast", 2),

            ("hopper",             "Hopper",             "Hopper",
             "Feeds items from a chest or container onto a belt or tube below.",
             0.5f, "Basic", 8),

            ("filter_hopper",      "Filter Hopper",      "FilterHopper",
             "Like a hopper, but only passes items matching the configured filter.",
             0.5f, "Basic", 8),

            ("belt_sorter",        "Belt Sorter",        "BeltSorter",
             "Routes items to specific output belts based on configurable sort rules.",
             0.5f, "Basic", 5),

            ("overflow_valve",     "Overflow Valve",     "OverflowValve",
             "Blocks output when downstream is full; re-opens automatically when space is available.",
             0.5f, "Basic", 1),
        };

        [MenuItem("Voidborne/Setup Automation Vol 8.1 (Items)")]
        public static void Setup()
        {
            EnsureDirectory(ItemFolder);

            string[] dbGuids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (dbGuids.Length == 0)
            {
                Debug.LogError("[AutomationSetup81] ItemDatabase not found — aborting.");
                return;
            }
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuids[0]);
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            if (db == null)
            {
                Debug.LogError("[AutomationSetup81] Could not load ItemDatabase — aborting.");
                return;
            }

            int created = 0, registered = 0;
            foreach (var e in Entries)
            {
                string assetPath = $"{ItemFolder}/{e.itemId}.asset";
                var item = AssetDatabase.LoadAssetAtPath<AutomationItem>(assetPath);

                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<AutomationItem>();
                    AssetDatabase.CreateAsset(item, assetPath);
                    created++;
                }

                // Always refresh fields so re-running is idempotent
                item.itemId       = e.itemId;
                item.displayName  = e.displayName;
                item.description  = e.description;
                item.itemType     = ItemType.Machine;
                item.maxStackSize = 1;
                item.tickInterval = e.tickInterval;
                item.speedLabel   = e.speedLabel;
                item.capacity     = e.capacity;

                // Link prefab if it exists
                string prefabPath = $"Assets/Prefabs/Automation/{e.prefabName}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                    item.devicePrefab = prefab;
                else
                    Debug.LogWarning($"[AutomationSetup81] Prefab not found at {prefabPath} — devicePrefab left unassigned.");

                EditorUtility.SetDirty(item);

                // Register in database if not already present
                bool alreadyInDb = false;
                foreach (var existing in db.items)
                    if (existing != null && existing.itemId == e.itemId) { alreadyInDb = true; break; }

                if (!alreadyInDb)
                {
                    db.items.Add(item);
                    EditorUtility.SetDirty(db);
                    registered++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AutomationSetup81] Done. {created} items created, {registered} registered in ItemDatabase.");
            EditorUtility.DisplayDialog("Vol 8.1 Setup",
                $"{created} AutomationItem SOs created and {registered} registered.\n" +
                "Conveyor belts, tubes, hoppers, sorters and overflow valves are now in the database.",
                "OK");
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
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
