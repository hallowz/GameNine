using System;
using System.Collections.Generic;
using Voidborne.Data;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.5 — player-facing prose for every <see cref="MaterialProperties"/>
    /// enum value. Surfaced by <see cref="TooltipUI"/> when an item with
    /// material-property tags is hovered.
    ///
    /// Pure static, runtime-visible, no Unity references. Coop note: read-only
    /// content data — same on every client.
    /// </summary>
    public static class PropertyDescriptions
    {
        private static readonly Dictionary<MaterialProperties, string> _descriptions =
            new Dictionary<MaterialProperties, string>
        {
            // Combustibles
            { MaterialProperties.Combustible_Dry,
                "Burns well. Dry combustibles release stable heat in any thermal machine." },
            { MaterialProperties.Combustible_Wet,
                "Burns badly. Wet matter spends most of its heat boiling itself dry first." },
            { MaterialProperties.Combustible_Liquid,
                "Burns hot. Liquid fuels release rapid, intense heat — good for high-temp work." },
            { MaterialProperties.Combustible_Volatile,
                "Explosive. Releases its energy in a sudden flash; useful in arms, dangerous in stockpiles." },

            // Liquids
            { MaterialProperties.Liquid_Aqueous,
                "Water-like fluid. Boils at low energy, holds thermal mass, accepted by any forgiving boiler." },
            { MaterialProperties.Liquid_Oil,
                "Slippery and flammable. Lubricates, refines into fuels, burns when ignited." },
            { MaterialProperties.Liquid_Alchemical,
                "Reactive solvent. Dissolves, etches, and combines into chemistry-grade products." },

            // Organics
            { MaterialProperties.Organic_Fresh,
                "Freshly harvested. Spoils if left alone; dries, ferments, or cooks readily." },
            { MaterialProperties.Organic_Decayed,
                "Rotted or composted matter. Feeds soil, fermenters, and certain alchemical reactions." },
            { MaterialProperties.Organic_Dried,
                "Moisture removed. Keeps for ages and burns cleaner than its fresh state." },
            { MaterialProperties.Organic_Sweet,
                "Sugar-bearing. Ferments into alcohol and feeds bees, beasts, and bakers alike." },

            // Solids
            { MaterialProperties.Solid_Metal,
                "Refined or raw metal. Smelts, casts, and hammers into tools and structure." },
            { MaterialProperties.Solid_Stone,
                "Stone, gravel, or rubble. Crushes into powder; builds walls and furnaces." },
            { MaterialProperties.Solid_Powder,
                "Ground fines. Mixes into compounds; the output of any forgiving crusher." },
            { MaterialProperties.Solid_Fiber,
                "Plant fibre or hide strip. Weaves, twists, and binds; the soft side of structure." },

            // Specialty
            { MaterialProperties.Conducts_Electric,
                "Carries current. Required for wiring, circuits, and any powered device." },
            { MaterialProperties.Crystalline,
                "Structured mineral. Cuts light, focuses energy, and resists ordinary wear." },
            { MaterialProperties.Magical,
                "Void-touched or spirit-bound. Behaves strangely in any machine that isn't built for it." }
        };

        /// <summary>
        /// Returns the player-facing description for <paramref name="prop"/>.
        /// Falls back to the enum name if no description has been authored
        /// (which the V4.5 EditMode tests guard against).
        /// </summary>
        public static string GetDescription(MaterialProperties prop)
        {
            if (_descriptions.TryGetValue(prop, out string desc) && !string.IsNullOrEmpty(desc))
            {
                return desc;
            }
            return prop.ToString();
        }

        /// <summary>True when an authored description exists for <paramref name="prop"/>.</summary>
        public static bool HasDescription(MaterialProperties prop)
        {
            return _descriptions.TryGetValue(prop, out string desc)
                && !string.IsNullOrEmpty(desc);
        }

        /// <summary>Total number of authored property descriptions.</summary>
        public static int Count => _descriptions.Count;

        /// <summary>
        /// Short human-friendly label for the property (drops the enum prefix
        /// and replaces underscores with spaces). Used by tooltip badges.
        /// e.g. <c>Liquid_Aqueous</c> -> <c>"Aqueous"</c>.
        /// </summary>
        public static string GetShortLabel(MaterialProperties prop)
        {
            string raw = prop.ToString();
            int us = raw.IndexOf('_');
            string tail = (us >= 0 && us + 1 < raw.Length) ? raw.Substring(us + 1) : raw;
            return tail.Replace('_', ' ');
        }

        /// <summary>Convenience: enumerate every authored entry.</summary>
        public static IEnumerable<KeyValuePair<MaterialProperties, string>> All => _descriptions;
    }
}
