using System.Collections.Generic;
using Newtonsoft.Json;

namespace Voidborne.Data.Schema
{
    /// <summary>
    /// POCO mirroring a single entry in <c>Design Documents/GameDesign/data/items.json</c>.
    /// The top-level JSON is a <c>Dictionary&lt;string, ItemJson&gt;</c> keyed by item ID;
    /// the key is the canonical item ID and is NOT stored inside this object.
    /// </summary>
    /// <remarks>
    /// Coop note: this is a pure data POCO with no Unity references and no runtime state.
    /// It is consumed during editor-time SO generation only (Volume 2 pipeline).
    /// </remarks>
    [System.Serializable]
    public class ItemJson
    {
        /// <summary>One of: "source", "machine", "component", "product".</summary>
        public string kind;

        /// <summary>Nullable. For kind=="source": "fauna", "flora", "ore", "soil", "exotic".</summary>
        public string src;

        /// <summary>Display name (e.g. "Workbench").</summary>
        public string name;

        /// <summary>Nullable. Free-form description / role string (e.g. "T1 — 3×3 crafting (BOOTSTRAP)").</summary>
        public string role;

        /// <summary>Nullable. List of recipes that produce this item. Items with no recipe (raw sources) omit the field.</summary>
        public List<RecipeJson> recipes;

        /// <summary>Nullable. True if this product is a placeable build block.</summary>
        [JsonProperty("_build")]
        public bool? build;

        /// <summary>Nullable. Build material color key (e.g. "build", "stone", "iron").</summary>
        [JsonProperty("_buildColor")]
        public string buildColor;

        /// <summary>Nullable. True if this product is a decorative block.</summary>
        [JsonProperty("_deco")]
        public bool? deco;
    }
}
