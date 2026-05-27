using System;
using UnityEngine;

namespace Voidborne.Crafting
{
    /// <summary>
    /// V2.3 recipe definition — ingredient-list-based content data for a single
    /// recipe in the game. Generated en masse by
    /// <c>Assets/Editor/Data/RecipeSoGenerator.cs</c> from the per-item
    /// <c>recipes[]</c> arrays in
    /// <c>Design Documents/GameDesign/data/items.json</c>.
    ///
    /// Replaces the V1 grid-based <see cref="CraftingRecipe"/> (now under
    /// <c>Assets/Scripts/_Legacy/Crafting/</c>). The new shape is shapeless:
    /// a list of (itemId, qty) ingredients plus an output item, scoped to a
    /// specific machine via <see cref="viaMachineId"/>. A null or empty
    /// <see cref="viaMachineId"/> denotes the personal crafting grid (bootstrap).
    /// </summary>
    /// <remarks>
    /// Coop note: stateless content data. Runtime crafting state lives on
    /// MonoBehaviours / NetworkBehaviours, never on the SO.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewRecipeDefinition", menuName = "Voidborne/Crafting/Recipe Definition (v2)")]
    public class RecipeDefinition : ScriptableObject
    {
        // ---------------------------------------------------------------
        // Output
        // ---------------------------------------------------------------

        [Header("Output")]
        [Tooltip("Item ID produced by this recipe. Matches a key in items.json / Assets/ScriptableObjects/Generated/Items/.")]
        public string outputItemId;

        [Tooltip("Quantity produced per craft. Defaults to 1; the source JSON does not carry qty.")]
        public int outputQty = 1;

        // ---------------------------------------------------------------
        // Inputs
        // ---------------------------------------------------------------

        [Header("Inputs")]
        public Ingredient[] ingredients;

        // ---------------------------------------------------------------
        // Scope
        // ---------------------------------------------------------------

        [Header("Scope")]
        [Tooltip("Machine ID that produces this recipe. Null or empty = personal crafting grid (bootstrap).")]
        public string viaMachineId;

        // ---------------------------------------------------------------
        // Notes / flags
        // ---------------------------------------------------------------

        [Header("Notes")]
        [TextArea(1, 3)]
        public string notes;

        [Tooltip("True if 'notes' contains the marker 'BOOTSTRAP'. Derived by the generator.")]
        public bool isBootstrap;

        [Tooltip("True if 'notes' contains the marker 'SYNERGY'. Derived by the generator.")]
        public bool isSynergy;

        // ---------------------------------------------------------------
        // Convenience accessors
        // ---------------------------------------------------------------

        /// <summary>Convenience alias for <see cref="outputItemId"/>.</summary>
        public string OutputId => outputItemId;

        /// <summary>True when the recipe is unscoped (personal crafting grid).</summary>
        public bool IsPersonalGrid => string.IsNullOrEmpty(viaMachineId);
    }

    /// <summary>
    /// Single ingredient entry inside a <see cref="RecipeDefinition"/>.
    /// Mirrors the JSON shape <c>{ id, qty }</c>.
    /// </summary>
    [Serializable]
    public struct Ingredient
    {
        [Tooltip("Item ID required as input. Matches a key in items.json.")]
        public string itemId;

        [Tooltip("Quantity required per craft.")]
        public int qty;

        public Ingredient(string itemId, int qty)
        {
            this.itemId = itemId;
            this.qty = qty;
        }
    }
}
