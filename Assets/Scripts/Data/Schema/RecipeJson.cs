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

        /// <summary>Required. Input ingredients (specific item IDs - the V2 path, still supported).</summary>
        public List<IngredientJson> inputs;

        /// <summary>Nullable. Free-form designer notes (may include "BOOTSTRAP" / "SYNERGY" tags).</summary>
        public string notes;

        // -------------------------------------------------------------------
        // V3 schema additions (Volume 2.8) - recipes in items_core.json may carry these.
        // The legacy items_backlog.json never carries these fields; the generator defaults
        // them to empty array / 1.0 to keep round-tripping backwards-compatible.
        // -------------------------------------------------------------------

        /// <summary>
        /// Nullable (V2.8). Property-based input requirements. Each entry declares a
        /// <see cref="MaterialProperties"/> tag, a required quantity, and a per-entry
        /// efficiency multiplier. Used by forgiving and hybrid machines to allow
        /// 'improvised' substitutions; picky machines ignore this list.
        /// </summary>
        public InputPropertyJson[] inputProperties;

        /// <summary>
        /// (V2.8) Recipe-wide efficiency multiplier. Scales crafting duration on
        /// forgiving machines and dampens the property-matched yield path on hybrids.
        /// Default 1.0.
        /// </summary>
        public float efficiency = 1.0f;

        /// <summary>
        /// (V2.8) Recipe-wide output modifier. Multiplied into the output quantity /
        /// quality. Hybrid machines cap this at 0.7x when matching via the property
        /// fallback path (the 'improvised quality penalty').
        /// </summary>
        public float outputModifier = 1.0f;
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

    /// <summary>
    /// V3 property-based input requirement (Volume 2.8). Each entry declares a
    /// material-property tag the machine must satisfy from the player's inputs.
    /// Forgiving and hybrid machines use these as a fallback after specific
    /// <see cref="RecipeJson.inputs"/> recipes fail to match; picky machines
    /// ignore this list entirely.
    /// </summary>
    [System.Serializable]
    public class InputPropertyJson
    {
        /// <summary>
        /// Material property tag string. Resolves to <see cref="MaterialProperties"/>
        /// via <c>Enum.TryParse</c> at generation time.
        /// </summary>
        public string property;

        /// <summary>Required quantity (sum of item counts whose properties[] contains this tag).</summary>
        public int qty;

        /// <summary>Per-entry efficiency multiplier. Default 1.0.</summary>
        public float efficiency = 1.0f;
    }
}
