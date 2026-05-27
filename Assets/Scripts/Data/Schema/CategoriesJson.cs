using System.Collections.Generic;

namespace Voidborne.Data.Schema
{
    /// <summary>
    /// Thin wrapper exposing the categories.json shape — a dictionary mapping a category
    /// name (e.g. "food", "power", "weapon", "armor", "build", "deco") to an array of
    /// item IDs that belong to that category.
    /// </summary>
    /// <remarks>
    /// The 17 top-level categories are: food, power, weapon, ammo, armor, beast, robot,
    /// defense, target, spirit, scholar, vehicle, automation, loop, gadget, build, deco.
    ///
    /// An item may appear in multiple categories. <see cref="GameDesignJsonLoader.LoadCategories"/>
    /// returns the underlying dictionary directly; this type exists for symmetry with the
    /// other schema files and to give callers a typed home for category-related helpers
    /// later in Volume 2.
    /// </remarks>
    public class CategoriesJson
    {
        /// <summary>Backing dictionary: category name → list of item IDs.</summary>
        public Dictionary<string, List<string>> map;

        public CategoriesJson() { map = new Dictionary<string, List<string>>(); }
        public CategoriesJson(Dictionary<string, List<string>> source) { map = source ?? new Dictionary<string, List<string>>(); }

        /// <summary>Returns the item IDs in a category, or an empty list if the category is unknown.</summary>
        public IReadOnlyList<string> Get(string category)
        {
            if (map != null && map.TryGetValue(category, out var ids) && ids != null) return ids;
            return System.Array.Empty<string>();
        }
    }
}
