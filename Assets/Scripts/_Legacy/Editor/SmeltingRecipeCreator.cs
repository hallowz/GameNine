// LEGACY — Volume 5.3. Editor-only utility that built the 4 pre-Core-60
// SmeltingRecipe assets at Assets/ScriptableObjects/SmeltingRecipes/.
// Those assets were archived to _Archived/Legacy/SmeltingRecipes/ in
// V5.1. The replacement shape is the generic RecipeDefinition + the
// items_core.json -> RecipeSoGenerator pipeline (V2.x). The runtime
// SmeltingRecipe class is still consumed by FurnaceBlock/ElectricFurnace/
// Grinder so it lives at Assets/Scripts/Automation/SmeltingRecipe.cs;
// only this asset-creation helper is preserved here.
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;

/// <summary>
/// Editor utility that creates the four smelting recipe assets under
/// Assets/ScriptableObjects/SmeltingRecipes/.
///
/// Run via: Voidborne > Create Smelting Recipes
///
/// Prerequisites: IronOre, IronIngot, CopperOre, CopperIngot must exist as item assets.
/// RawMeat and Glass items are created by this script if missing.
/// </summary>
public static class SmeltingRecipeCreator
{
    private const string RecipesFolder = "Assets/ScriptableObjects/SmeltingRecipes";
    private const string ItemsFolder   = "Assets/ScriptableObjects/Items";

    [MenuItem("Voidborne/Create Smelting Recipes")]
    public static void CreateSmeltingRecipes()
    {
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(RecipesFolder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "SmeltingRecipes");

        // Ensure item assets for new items (RawMeat, CookedMeat, Sand, Glass) exist
        ItemDefinition rawMeat   = EnsureItem("RawMeat",   "raw_meat",    "Raw Meat",   "Uncooked meat. Can be smelted into Cooked Meat.");
        ItemDefinition cookedMeat = EnsureItem("CookedMeat","cooked_meat", "Cooked Meat","Nutritious cooked meat.");
        ItemDefinition sand      = EnsureItem("Sand",      "sand",        "Sand",       "Fine sand. Can be smelted into Glass.");
        ItemDefinition glass     = EnsureItem("Glass",     "glass",       "Glass",      "Smooth glass created by smelting Sand.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Load existing item assets
        ItemDefinition ironOre     = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/IronOre.asset");
        ItemDefinition ironIngot   = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/IronIngot.asset");
        ItemDefinition copperOre   = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/CopperOre.asset");
        ItemDefinition copperIngot = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/CopperIngot.asset");

        // Log warnings for any missing items
        if (ironOre    == null) Debug.LogWarning("[SmeltingRecipeCreator] IronOre item not found.");
        if (ironIngot  == null) Debug.LogWarning("[SmeltingRecipeCreator] IronIngot item not found.");
        if (copperOre  == null) Debug.LogWarning("[SmeltingRecipeCreator] CopperOre item not found.");
        if (copperIngot == null) Debug.LogWarning("[SmeltingRecipeCreator] CopperIngot item not found.");

        // Create the four recipes
        SmeltingRecipe ironRecipe    = CreateOrLoad("IronOre_to_IronIngot.asset");
        ironRecipe.inputItem  = ironOre;
        ironRecipe.outputItem = ironIngot;
        ironRecipe.smeltTime  = 5f;
        EditorUtility.SetDirty(ironRecipe);

        SmeltingRecipe copperRecipe  = CreateOrLoad("CopperOre_to_CopperIngot.asset");
        copperRecipe.inputItem  = copperOre;
        copperRecipe.outputItem = copperIngot;
        copperRecipe.smeltTime  = 5f;
        EditorUtility.SetDirty(copperRecipe);

        SmeltingRecipe meatRecipe    = CreateOrLoad("RawMeat_to_CookedMeat.asset");
        meatRecipe.inputItem  = rawMeat;
        meatRecipe.outputItem = cookedMeat;
        meatRecipe.smeltTime  = 3f;
        EditorUtility.SetDirty(meatRecipe);

        SmeltingRecipe glassRecipe   = CreateOrLoad("Sand_to_Glass.asset");
        glassRecipe.inputItem  = sand;
        glassRecipe.outputItem = glass;
        glassRecipe.smeltTime  = 8f;
        EditorUtility.SetDirty(glassRecipe);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SmeltingRecipeCreator] Created 4 smelting recipes in " + RecipesFolder);
        EditorUtility.DisplayDialog("Smelting Recipes Created",
            "Created:\n" +
            "  IronOre → IronIngot (5 s)\n" +
            "  CopperOre → CopperIngot (5 s)\n" +
            "  RawMeat → CookedMeat (3 s)\n" +
            "  Sand → Glass (8 s)\n\n" +
            "Assign these assets to a FurnaceBlock's 'recipes' list in the Inspector.",
            "OK");
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static SmeltingRecipe CreateOrLoad(string fileName)
    {
        string assetPath = $"{RecipesFolder}/{fileName}";
        SmeltingRecipe existing = AssetDatabase.LoadAssetAtPath<SmeltingRecipe>(assetPath);
        if (existing != null) return existing;

        SmeltingRecipe instance = ScriptableObject.CreateInstance<SmeltingRecipe>();
        AssetDatabase.CreateAsset(instance, assetPath);
        return instance;
    }

    /// <summary>
    /// Loads an ItemDefinition from the items folder, or creates a minimal placeholder asset
    /// if it does not yet exist.
    /// </summary>
    private static ItemDefinition EnsureItem(string fileName, string itemId, string displayName, string description)
    {
        string assetPath = $"{ItemsFolder}/{fileName}.asset";
        ItemDefinition existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
        if (existing != null) return existing;

        ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.itemId      = itemId;
        item.displayName = displayName;
        item.description = description;
        item.itemType    = ItemType.Resource;
        item.maxStackSize = 64;
        item.weight      = 0.1f;

        AssetDatabase.CreateAsset(item, assetPath);
        Debug.Log($"[SmeltingRecipeCreator] Created missing item asset: {assetPath}");
        return item;
    }
}
