// LEGACY — see RecipeDefinition. Asset wipe in Volume 5.
//
// V2.3 note: this class is the grid-based recipe shape used by the V1
// crafting pipeline (CraftingGrid / CraftingManager / CraftingStation /
// PersonalCraftingGrid). The V2 replacement is the ingredient-list-based
// `RecipeDefinition` ScriptableObject (sibling file, new location). The
// class identifier `CraftingRecipe` is intentionally preserved here so
// the existing consumers continue to compile until Volume 6 rewrites the
// crafting match engine to consume the new RecipeRegistry.
//
// The [CreateAssetMenu] attribute has been removed so authors cannot
// create new legacy recipes through the editor menu. Existing assets
// under Assets/ScriptableObjects/Recipes/*.asset still load against this
// class (same GUID) and will be deleted in Volume 5.

using UnityEngine;

namespace Voidborne.Crafting
{
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Identity")]
        public string recipeName;

        [Header("Grid Size")]
        public int gridWidth = 3;
        public int gridHeight = 3;

        [Header("Ingredients")]
        [Tooltip("Flattened array of size gridWidth × gridHeight. Null = empty slot. Index = y * gridWidth + x.")]
        public ItemDefinition[] ingredients;

        [Header("Result")]
        public ItemStack result;

        /// <summary>Returns the ingredient at column x, row y. Null means empty slot.</summary>
        public ItemDefinition GetIngredient(int x, int y)
        {
            if (ingredients == null) return null;
            int index = y * gridWidth + x;
            if (index < 0 || index >= ingredients.Length) return null;
            return ingredients[index];
        }
    }
}
