using UnityEngine;

namespace Voidborne.Crafting
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Voidborne/Crafting Recipe")]
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
