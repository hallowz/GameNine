using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Crafting
{
    /// <summary>
    /// Singleton MonoBehaviour that holds all known CraftingRecipes.
    /// Assign recipes via the Inspector.
    /// </summary>
    public class CraftingManager : MonoBehaviour
    {
        public static CraftingManager Instance { get; private set; }

        [SerializeField]
        private List<CraftingRecipe> recipes = new List<CraftingRecipe>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Checks the given CraftingGrid against all known recipes.
        /// Returns the first matching CraftingRecipe, or null.
        /// </summary>
        public CraftingRecipe FindMatch(CraftingGrid grid)
        {
            if (grid == null) return null;
            return grid.CheckRecipes(recipes);
        }

        /// <summary>Returns a copy of the full recipe list.</summary>
        public List<CraftingRecipe> GetAllRecipes()
        {
            return new List<CraftingRecipe>(recipes);
        }
    }
}
