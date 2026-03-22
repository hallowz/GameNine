using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that creates vehicle-related inventory items and the DevBackpack.
/// Creates PlaceableItem assets for VehicleWorkbench and all default vehicles,
/// plus a DevBackpackItem that contains infinite of every item.
///
/// Run via menu: Voidborne/Vehicles/Setup Vehicle Items + DevBackpack
/// Safe to re-run — existing assets are skipped.
/// </summary>
public static class VehicleItemSetup
{
    private const string ItemsFolder  = "Assets/ScriptableObjects/Items";
    private const string PrefabFolder = "Assets/Prefabs/Vehicles";
    private const string ResourcesFolder = "Assets/Resources";

    private struct PlaceableSpec
    {
        public string FileName;
        public string ItemId;
        public string DisplayName;
        public string Description;
        public string CategoryLabel;
        public string PrefabName;
        public int    MaxStack;
        public float  Weight;

        public PlaceableSpec(string fileName, string itemId, string displayName,
            string description, string categoryLabel, string prefabName,
            int maxStack, float weight)
        {
            FileName      = fileName;
            ItemId        = itemId;
            DisplayName   = displayName;
            Description   = description;
            CategoryLabel = categoryLabel;
            PrefabName    = prefabName;
            MaxStack      = maxStack;
            Weight        = weight;
        }
    }

    private static readonly PlaceableSpec[] VehicleItems = new PlaceableSpec[]
    {
        new PlaceableSpec(
            "VehicleWorkbench_Item", "vehicle_workbench", "Vehicle Workbench",
            "A reinforced workstation for assembling, repairing, and reconfiguring vehicles.",
            "Station", "VehicleWorkbench", 1, 12f),
        new PlaceableSpec(
            "Pushcart_Item", "pushcart_default", "Pushcart",
            "A player-pushed cargo cart. Silent and slow, but carries 4x inventory. No engine required.",
            "Vehicle", "Pushcart_Default", 1, 8f),
        new PlaceableSpec(
            "Buggy_Item", "buggy_default", "Buggy",
            "A lightweight 4-wheel scout vehicle. Seats 2, fast and nimble.",
            "Vehicle", "Buggy_Default", 1, 15f),
        new PlaceableSpec(
            "Cycle_Item", "cycle_default", "Cycle",
            "A two-wheel motorbike with lean physics. Fast but twitchy at low speed.",
            "Vehicle", "Cycle_Default", 1, 10f),
        new PlaceableSpec(
            "Hauler_Item", "hauler_default", "Hauler",
            "A heavy 6-wheel transport truck. Seats 4, dual engines, massive cargo capacity.",
            "Vehicle", "Hauler_Default", 1, 25f),
        new PlaceableSpec(
            "DrillRig_Item", "drillrig_default", "Drill Rig",
            "A compact 4-wheel vehicle with a front-mounted drill for terrain deformation.",
            "Vehicle", "DrillRig_Default", 1, 14f),
        new PlaceableSpec(
            "Gyrocopter_Item", "gyrocopter_default", "Gyrocopter",
            "A rotor-lift aircraft. Seats 2. Watch your airspeed or the rotors will stall.",
            "Vehicle", "Gyrocopter_Default", 1, 18f),
    };

    [MenuItem("Voidborne/Vehicles/Setup Vehicle Items + DevBackpack")]
    public static void Run()
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(ResourcesFolder);

        List<ItemDefinition> createdDefs = new List<ItemDefinition>();

        // 1. Create PlaceableItem assets for each vehicle / workbench
        foreach (var spec in VehicleItems)
        {
            var def = CreatePlaceableItem(spec);
            if (def != null)
                createdDefs.Add(def);
        }

        // 2. Create DevBackpack
        var devBp = CreateDevBackpack();
        if (devBp != null)
            createdDefs.Add(devBp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Register all in ItemDatabase
        int added = SyncToDatabase(createdDefs);

        // 4. Add PlaceableItemHandler + PlaceablePlacementController to Player
        AddPlayerComponents();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Vehicle Items + DevBackpack setup complete.\n" +
                     $"Items processed: {createdDefs.Count}\n" +
                     $"New entries added to ItemDatabase: {added}";
        Debug.Log("[VehicleItemSetup] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Vehicle Item Setup", msg, "OK");
    }

    // ─── PlaceableItem creation ──────────────────────────────────────

    private static PlaceableItem CreatePlaceableItem(PlaceableSpec spec)
    {
        string path = $"{ItemsFolder}/{spec.FileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<PlaceableItem>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<PlaceableItem>();
        item.itemId        = spec.ItemId;
        item.displayName   = spec.DisplayName;
        item.description   = spec.Description;
        item.itemType      = ItemType.Machine;
        item.maxStackSize  = spec.MaxStack;
        item.weight        = spec.Weight;
        item.categoryLabel = spec.CategoryLabel;

        // Link prefab
        string prefabPath = $"{PrefabFolder}/{spec.PrefabName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
            item.prefabToPlace = prefab;
        else
            Debug.LogWarning($"[VehicleItemSetup] Prefab not found at {prefabPath} for '{spec.ItemId}'.");

        AssetDatabase.CreateAsset(item, path);
        Debug.Log($"[VehicleItemSetup] Created {path}");
        return item;
    }

    // ─── DevBackpack creation ────────────────────────────────────────

    private static DevBackpackItem CreateDevBackpack()
    {
        string path = $"{ItemsFolder}/DevBackpack.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DevBackpackItem>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<DevBackpackItem>();
        item.itemId        = "dev_backpack";
        item.displayName   = "Dev Backpack";
        item.description   = "Contains an infinite supply of every item in the game. Items never deplete. For testing and creative play.";
        item.itemType      = ItemType.Backpack;
        item.maxStackSize  = 1;
        item.weight        = 0f;
        item.extraRows     = 12;  // 12 rows × 9 cols = 108 slots (enough for all items)
        item.extraColumns  = 9;

        AssetDatabase.CreateAsset(item, path);
        Debug.Log($"[VehicleItemSetup] Created {path}");
        return item;
    }

    // ─── Database sync ───────────────────────────────────────────────

    private static int SyncToDatabase(List<ItemDefinition> defs)
    {
        ItemDatabase db = null;
        string dbPath = $"{ResourcesFolder}/ItemDatabase.asset";

        db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            }
        }

        if (db == null)
        {
            Debug.LogError("[VehicleItemSetup] No ItemDatabase found.");
            return 0;
        }

        SerializedObject so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("items");

        HashSet<Object> registered = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (existing != null) registered.Add(existing);
        }

        int added = 0;
        foreach (var def in defs)
        {
            if (def == null || registered.Contains(def)) continue;
            int idx = list.arraySize;
            list.InsertArrayElementAtIndex(idx);
            list.GetArrayElementAtIndex(idx).objectReferenceValue = def;
            registered.Add(def);
            added++;
            Debug.Log($"[VehicleItemSetup] Added '{def.itemId}' to ItemDatabase.");
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
        }

        return added;
    }

    // ─── Player component wiring ────────────────────────────────────

    private static void AddPlayerComponents()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            playerGO = GameObject.Find("Player");
        }
        if (playerGO == null)
        {
            Debug.LogWarning("[VehicleItemSetup] No Player found. Add PlaceableItemHandler " +
                             "and PlaceablePlacementController to Player manually.");
            return;
        }

        if (playerGO.GetComponent<PlaceablePlacementController>() == null)
            playerGO.AddComponent<PlaceablePlacementController>();

        if (playerGO.GetComponent<PlaceableItemHandler>() == null)
            playerGO.AddComponent<PlaceableItemHandler>();

        Debug.Log("[VehicleItemSetup] Added PlaceableItemHandler + PlaceablePlacementController to Player.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string child  = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }
}
