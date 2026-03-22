using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility that ensures every item in the project is:
///   1. Created with correct itemId, displayName, description, and itemType
///   2. Registered in the ItemDatabase
///
/// Also creates Furnace and Workbench ItemDefinition assets so the player
/// can hold them in their inventory (placement comes later).
///
/// Run via: Voidborne > Full Item Setup
/// Safe to re-run — existing assets are patched in place, not recreated.
/// </summary>
public static class FullItemSetup
{
    private const string ItemsFolder   = "Assets/ScriptableObjects/Items";
    private const string ResourcesFolder = "Assets/Resources";

    // -------------------------------------------------------------------
    //  All items that should exist, with their canonical data
    // -------------------------------------------------------------------
    private static readonly ItemSpec[] AllItems = new ItemSpec[]
    {
        // Resources
        new ItemSpec("Wood",         "wood",          "Wood",             "Raw lumber harvested from trees.",                              ItemType.Resource, 64, 1.0f),
        new ItemSpec("Stone",        "stone",          "Stone",            "A chunk of rough stone.",                                       ItemType.Resource, 64, 2.0f),
        new ItemSpec("Coal",         "coal",           "Coal",             "Combustible mineral used as fuel in furnaces.",                  ItemType.Resource, 64, 1.0f),
        new ItemSpec("IronOre",      "iron_ore",       "Iron Ore",         "Unrefined iron ore extracted from rock veins.",                 ItemType.Resource, 64, 2.0f),
        new ItemSpec("IronIngot",    "iron_ingot",     "Iron Ingot",       "Refined iron bar, smelted from iron ore.",                      ItemType.Resource, 64, 2.0f),
        new ItemSpec("CopperOre",    "copper_ore",     "Copper Ore",       "Unrefined copper ore with a greenish tinge.",                   ItemType.Resource, 64, 2.0f),
        new ItemSpec("CopperIngot",  "copper_ingot",   "Copper Ingot",     "Refined copper bar with a warm orange sheen.",                  ItemType.Resource, 64, 2.0f),
        new ItemSpec("Stick",        "stick",          "Stick",            "A short wooden stick, useful for crafting handles.",            ItemType.Resource, 64, 0.2f),
        new ItemSpec("WoodPlanks",   "wood_planks",    "Wood Planks",      "Smooth planks cut from raw wood.",                              ItemType.Resource, 64, 0.5f),
        new ItemSpec("Sand",         "sand",           "Sand",             "Fine granular sand. Can be smelted into glass.",                ItemType.Resource, 64, 1.0f),
        new ItemSpec("Glass",        "glass",          "Glass",            "Smooth transparent glass, made by smelting sand.",             ItemType.Resource, 64, 1.0f),
        new ItemSpec("RawMeat",      "raw_meat",       "Raw Meat",         "Uncooked meat. Smelt in a furnace to make Cooked Meat.",        ItemType.Consumable, 16, 0.5f),
        new ItemSpec("CookedMeat",   "cooked_meat",    "Cooked Meat",      "Nutritious cooked meat that restores health.",                  ItemType.Consumable, 16, 0.5f),
        new ItemSpec("Torch",        "torch",          "Torch",            "A lit torch that provides light when placed.",                  ItemType.Block,     64, 0.1f),

        // Placeable machines / blocks (held in inventory, placed later)
        new ItemSpec("Furnace",      "furnace",        "Furnace",          "A stone furnace for smelting ores and cooking food.",           ItemType.Machine, 1, 5.0f),
        new ItemSpec("Workbench",    "workbench",      "Workbench",        "A crafting table that unlocks 3×3 crafting recipes.",           ItemType.Block,   1, 8.0f),
    };

    // Tools are handled separately (ToolDefinition subclass, different folder)
    private static readonly string ToolsFolder = "Assets/ScriptableObjects/Items/Tools";

    // -------------------------------------------------------------------
    //  Menu entry
    // -------------------------------------------------------------------

    [MenuItem("Voidborne/Full Item Setup")]
    public static void Run()
    {
        EnsureFolders();

        // 1 — Ensure all plain ItemDefinition assets exist and have correct data
        List<ItemDefinition> allDefs = new List<ItemDefinition>();

        foreach (ItemSpec spec in AllItems)
        {
            ItemDefinition def = EnsureItemAsset(spec);
            if (def != null)
                allDefs.Add(def);
        }

        // 2 — Also collect tool definitions (they already exist, just need registering)
        string[] toolGuids = AssetDatabase.FindAssets("t:ToolDefinition", new[] { ToolsFolder });
        foreach (string guid in toolGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ToolDefinition tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
            if (tool != null) allDefs.Add(tool);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3 — Sync everything to the ItemDatabase
        int added = SyncToDatabase(allDefs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Full Item Setup complete.\n\n" +
                     $"Items processed: {allDefs.Count}\n" +
                     $"New entries added to ItemDatabase: {added}\n\n" +
                     $"Press Play — the player will start with one of each item.";
        Debug.Log("[FullItemSetup] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Full Item Setup", msg, "OK");
    }

    // -------------------------------------------------------------------
    //  Ensure a single ItemDefinition asset exists and has correct data
    // -------------------------------------------------------------------

    private static ItemDefinition EnsureItemAsset(ItemSpec spec)
    {
        string assetPath = $"{ItemsFolder}/{spec.FileName}.asset";

        // Load existing or create new
        ItemDefinition def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(def, assetPath);
            Debug.Log($"[FullItemSetup] Created {assetPath}");
        }

        // Patch fields using SerializedObject so the asset is properly dirtied
        SerializedObject so = new SerializedObject(def);
        bool changed = false;

        changed |= SetStringIfEmpty(so, "itemId",      spec.ItemId);
        changed |= SetStringIfEmpty(so, "displayName", spec.DisplayName);
        changed |= SetStringIfEmpty(so, "description", spec.Description);

        // Always enforce itemType (safe — tools use ToolDefinition which overrides this)
        SerializedProperty typeProp = so.FindProperty("itemType");
        if (typeProp != null && typeProp.enumValueIndex != (int)spec.ItemType)
        {
            typeProp.enumValueIndex = (int)spec.ItemType;
            changed = true;
        }

        // Enforce maxStackSize and weight only if still at defaults
        SerializedProperty stackProp = so.FindProperty("maxStackSize");
        if (stackProp != null && stackProp.intValue == 0)
        {
            stackProp.intValue = spec.MaxStack;
            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
        }

        return def;
    }

    // -------------------------------------------------------------------
    //  Sync all defs to the ItemDatabase
    // -------------------------------------------------------------------

    private static int SyncToDatabase(List<ItemDefinition> defs)
    {
        // Find the ItemDatabase — prefer Resources folder so runtime load works
        ItemDatabase db = null;
        string dbPath   = $"{ResourcesFolder}/ItemDatabase.asset";

        db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null)
        {
            // Fall back to any ItemDatabase in the project
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            }
        }

        if (db == null)
        {
            Debug.LogError("[FullItemSetup] No ItemDatabase found. Create one at Assets/Resources/ItemDatabase.asset first.");
            return 0;
        }

        SerializedObject so    = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("items");

        // Build a set of already-registered object references
        HashSet<Object> registered = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (existing != null) registered.Add(existing);
        }

        int added = 0;
        foreach (ItemDefinition def in defs)
        {
            if (def == null || registered.Contains(def)) continue;

            int idx = list.arraySize;
            list.InsertArrayElementAtIndex(idx);
            list.GetArrayElementAtIndex(idx).objectReferenceValue = def;
            registered.Add(def);
            added++;
            Debug.Log($"[FullItemSetup] Added '{def.itemId}' to ItemDatabase.");
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
        }

        return added;
    }

    // -------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------

    private static bool SetStringIfEmpty(SerializedObject so, string propName, string value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null || !string.IsNullOrEmpty(prop.stringValue)) return false;
        prop.stringValue = value;
        return true;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(ItemsFolder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Items");
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }

    // -------------------------------------------------------------------
    //  Data
    // -------------------------------------------------------------------

    private struct ItemSpec
    {
        public readonly string   FileName;
        public readonly string   ItemId;
        public readonly string   DisplayName;
        public readonly string   Description;
        public readonly ItemType ItemType;
        public readonly int      MaxStack;
        public readonly float    Weight;

        public ItemSpec(string fileName, string itemId, string displayName, string description,
                        ItemType itemType, int maxStack, float weight)
        {
            FileName    = fileName;
            ItemId      = itemId;
            DisplayName = displayName;
            Description = description;
            ItemType    = itemType;
            MaxStack    = maxStack;
            Weight      = weight;
        }
    }
}
