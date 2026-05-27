using System.Collections.Generic;

namespace Voidborne.Data.Schema
{
    /// <summary>
    /// POCO mirroring a single recipe entry inside an <see cref="ItemJson"/>.
    /// </summary>
    /// <remarks>
    /// Coop note: pure data, no runtime state. Consumed by editor SO generators.
    /// </remarks>
    [System.Serializable]
    public class RecipeJson
    {
        /// <summary>
        /// Nullable. The machine ID that produces this recipe, or <c>null</c> for the
        /// personal crafting grid / hand-built bootstrap recipes.
        /// </summary>
        public string via;

        /// <summary>Required. Input ingredients.</summary>
        public List<IngredientJson> inputs;

        /// <summary>Nullable. Free-form designer notes (may include "BOOTSTRAP" / "SYNERGY" tags).</summary>
        public string notes;
    }

    /// <summary>
    /// Single ingredient entry inside a <see cref="RecipeJson"/>.
    /// </summary>
    [System.Serializable]
    public class IngredientJson
    {
        /// <summary>Canonical item ID (matches a key in items.json).</summary>
        public string id;

        /// <summary>Quantity required.</summary>
        public int qty;
    }
}
