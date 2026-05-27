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

        // -------------------------------------------------------------------
        // V3 schema additions (Volume 2.6 / 2.7) - items_core.json only.
        // The legacy items_backlog.json never carries these fields.
        // -------------------------------------------------------------------

        /// <summary>
        /// Nullable. Material property tags (V2.6). String values that resolve to
        /// <see cref="Voidborne.Data.MaterialProperties"/> via <c>Enum.TryParse</c>.
        /// Most items declare 1-4 entries; machines typically declare none (their
        /// behaviour is governed by <see cref="processType"/>).
        /// </summary>
        public string[] properties;

        /// <summary>
        /// Nullable (machines only, V2.7). Process-type vocabulary entry that resolves to
        /// <see cref="Voidborne.Automation.MachineProcessType"/> via <c>Enum.TryParse</c>.
        /// Forgiving/picky/hybrid distinction drives the crafting match engine in Volume 6.1.
        /// Unknown values default to <c>Picky_Specialty</c> with a generator warning.
        /// </summary>
        public string processType;

        /// <summary>
        /// Nullable (machines only, V2.7). Multi-line prose description shown in the Machine UI
        /// tooltip (Volume 4.4). Copied verbatim from JSON to <c>MachineDefinition.howItWorks</c>.
        /// </summary>
        public string howItWorks;
    }
}
