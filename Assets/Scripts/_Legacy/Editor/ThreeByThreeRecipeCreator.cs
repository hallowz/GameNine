// LEGACY — Volume 5.3. Editor-only utility that built the pre-Core-60
// 3x3 workbench recipes at Assets/ScriptableObjects/Recipes/. Those
// assets were archived in V5.1. The replacement crafting flow lives at
// the procedurally-generated Workbench (Volume 3.3) with Core-60-scoped
// recipes from items_core.json. Preserved for historical reference.
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.Crafting;

/// <summary>
/// Editor utility that creates the five 3×3 CraftingRecipe assets under
/// Assets/ScriptableObjects/Recipes/.
/// Run via: Voidborne > Create 3x3 Recipes
/// </summary>
public static class ThreeByThreeRecipeCreator
{
    private const string RecipesFolder = "Assets/ScriptableObjects/Recipes";

    [MenuItem("Voidborne/Create 3x3 Recipes")]
    public static void Create3x3Recipes()
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
            Debug.LogError("[ThreeByThreeRecipeCreator] ItemDatabase not found.");
            return;
        }

        ItemDefinition Item(string id)
        {
            ItemDefinition def = db.GetItem(id);
            if (def == null)
                Debug.LogWarning($"[ThreeByThreeRecipeCreator] Item '{id}' not found in ItemDatabase.");
            return def;
        }

        void SaveRecipe(CraftingRecipe recipe, string assetName)
        {
            string path = $"{RecipesFolder}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(recipe, path);
        }

        ItemDefinition stone     = Item("stone");
        ItemDefinition ironIngot = Item("iron_ingot");
        ItemDefinition stick     = Item("stick");
        ItemDefinition woodPlanks = Item("wood_planks");

        // ----------------------------------------------------------------
        //  1. Stone Furnace — stone ring (8 stones surrounding empty center)
        //     S S S
        //     S _ S
        //     S S S
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Stone Furnace";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            r.ingredients = new ItemDefinition[]
            {
                stone,  stone,  stone,  // row 0
                stone,  null,   stone,  // row 1
                stone,  stone,  stone   // row 2
            };
            r.result = new ItemStack(stone, 1); // placeholder until StoneFurnace item exists
            SaveRecipe(r, "StoneFurnace");
        }

        // ----------------------------------------------------------------
        //  2. Iron Pickaxe
        //     I I I
        //     _ S _
        //     _ S _
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Iron Pickaxe";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            r.ingredients = new ItemDefinition[]
            {
                ironIngot, ironIngot, ironIngot, // row 0
                null,      stick,     null,       // row 1
                null,      stick,     null        // row 2
            };
            r.result = new ItemStack(ironIngot, 1); // placeholder until IronPickaxe item exists
            SaveRecipe(r, "IronPickaxe");
        }

        // ----------------------------------------------------------------
        //  3. Iron Sword
        //     _ I _
        //     _ I _
        //     _ S _
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Iron Sword";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            r.ingredients = new ItemDefinition[]
            {
                null, ironIngot, null, // row 0
                null, ironIngot, null, // row 1
                null, stick,     null  // row 2
            };
            r.result = new ItemStack(ironIngot, 1); // placeholder until IronSword item exists
            SaveRecipe(r, "IronSword");
        }

        // ----------------------------------------------------------------
        //  4. Wooden Door — 2 columns of 3 wood planks (left and right columns)
        //     W _ W
        //     W _ W
        //     W _ W
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Wooden Door";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            r.ingredients = new ItemDefinition[]
            {
                woodPlanks, null, woodPlanks, // row 0
                woodPlanks, null, woodPlanks, // row 1
                woodPlanks, null, woodPlanks  // row 2
            };
            r.result = new ItemStack(woodPlanks, 1); // placeholder
            SaveRecipe(r, "WoodenDoor");
        }

        // ----------------------------------------------------------------
        //  5. Chest — wood_planks ring (8 surrounding empty center)
        //     W W W
        //     W _ W
        //     W W W
        // ----------------------------------------------------------------
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipe>();
            r.recipeName  = "Chest";
            r.gridWidth   = 3;
            r.gridHeight  = 3;
            r.ingredients = new ItemDefinition[]
            {
                woodPlanks, woodPlanks, woodPlanks, // row 0
                woodPlanks, null,       woodPlanks, // row 1
                woodPlanks, woodPlanks, woodPlanks  // row 2
            };
            r.result = new ItemStack(woodPlanks, 1); // placeholder
            SaveRecipe(r, "Chest");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ThreeByThreeRecipeCreator] All 5 3×3 recipes created in " + RecipesFolder);
        EditorUtility.FocusProjectWindow();
    }
}
