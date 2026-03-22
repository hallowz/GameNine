using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Crafting;

/// <summary>
/// Editor utility that creates the 5 starter CraftingRecipe assets under
/// Assets/ScriptableObjects/Recipes/.
/// Run via: Voidborne > Create Starter Recipes
/// </summary>
public static class CraftingRecipeCreator
{
    private const string RecipesFolder = "Assets/ScriptableObjects/Recipes";

    [MenuItem("Voidborne/Create Starter Recipes")]
    public static void CreateStarterRecipes()
    {
        // Ensure the output folder exists.
        if (!AssetDatabase.IsValidFolder(RecipesFolder))
        {
            string parent = Path.GetDirectoryName(RecipesFolder).Replace('\\', '/');
            string folder = Path.GetFileName(RecipesFolder);
            AssetDatabase.CreateFolder(parent, folder);
        }

        ItemDatabase db = ItemDatabase.GetOrLoad();
        if (db == null)
        {
            Debug.LogError("[CraftingRecipeCreator] ItemDatabase not found. Make sure it exists at Assets/Resources/ItemDatabase.asset.");
            return;
        }

        // Helper to look up an item and warn if missing.
        ItemDefinition Item(string id)
        {
            ItemDefinition def = db.GetItem(id);
            if (def == null)
                Debug.LogWarning($"[CraftingRecipeCreator] Item '{id}' not found in ItemDatabase.");
            return def;
        }

        // Helper to create or overwrite a recipe asset.
        void SaveRecipe(CraftingRecipe recipe, string assetName)
        {
            string path = $"{RecipesFolder}/{assetName}.asset";
            // Remove existing asset if present so we get a clean write.
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(recipe, path);
        }

        // ----------------------------------------------------------------
        //  1. Wood → 4 WoodPlanks  (1×1)
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Wood to Wood Planks";
            r.gridWidth   = 1;
            r.gridHeight  = 1;
            r.ingredients = new ItemDefinition[] { Item("wood") };
            r.result      = new ItemStack(Item("wood_planks"), 4);
            SaveRecipe(r, "WoodToWoodPlanks");
        }

        // ----------------------------------------------------------------
        //  2. WoodPlanks (top) + WoodPlanks (bottom) → 4 Sticks  (1×2)
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Wood Planks to Sticks";
            r.gridWidth   = 1;
            r.gridHeight  = 2;
            r.ingredients = new ItemDefinition[]
            {
                Item("wood_planks"), // (0,0) top
                Item("wood_planks")  // (0,1) bottom
            };
            r.result = new ItemStack(Item("stick"), 4);
            SaveRecipe(r, "WoodPlanksToSticks");
        }

        // ----------------------------------------------------------------
        //  3. Wooden Pickaxe (placeholder: output = Stone)  (3×3)
        //     Row 0: WoodPlanks WoodPlanks WoodPlanks
        //     Row 1: null       Stick      null
        //     Row 2: null       Stick      null
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Wooden Pickaxe";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            ItemDefinition wp   = Item("wood_planks");
            ItemDefinition st   = Item("stick");
            r.ingredients = new ItemDefinition[]
            {
                wp,   wp,   wp,   // row 0
                null, st,   null, // row 1
                null, st,   null  // row 2
            };
            r.result = new ItemStack(Item("stone"), 1); // placeholder until WoodenPickaxe item exists
            SaveRecipe(r, "WoodenPickaxe");
        }

        // ----------------------------------------------------------------
        //  4. Torch: Coal (top) + Stick (bottom) → 4 Torches  (1×2)
        //     Uses gridWidth=2, gridHeight=1 per spec (Coal top, Stick below → 2×1 means width=2 height=1?)
        //     Re-reading spec: "2×1 grid: Coal on top, Stick below" → width=1, height=2 makes more sense.
        //     Using 1 wide × 2 tall to keep "on top / below" semantics.
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Torch";
            r.gridWidth   = 1;
            r.gridHeight  = 2;
            r.ingredients = new ItemDefinition[]
            {
                Item("coal"),  // (0,0) top
                Item("stick")  // (0,1) bottom
            };
            r.result = new ItemStack(Item("torch"), 4);
            SaveRecipe(r, "TorchRecipe");
        }

        // ----------------------------------------------------------------
        //  5. Workbench: all 4 WoodPlanks → 1 Stone (placeholder)  (2×2)
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Workbench";
            r.gridWidth   = 2;
            r.gridHeight  = 2;
            ItemDefinition wp = Item("wood_planks");
            r.ingredients = new ItemDefinition[]
            {
                wp, wp, // row 0
                wp, wp  // row 1
            };
            r.result = new ItemStack(Item("stone"), 1); // placeholder
            SaveRecipe(r, "Workbench");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CraftingRecipeCreator] All 5 starter recipes created successfully in " + RecipesFolder);
        EditorUtility.FocusProjectWindow();
    }
}
