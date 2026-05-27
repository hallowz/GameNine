using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.ArtPipeline
{
    /// <summary>
    /// Volume 3.1 — Material Palette.
    ///
    /// Static, stateless lookup that maps a palette key (category / source /
    /// build-material name) to a debug-friendly <see cref="Color"/>. The
    /// MaterialGenerator iterates <see cref="AllEntries"/> and produces one
    /// URP/Lit material per key under <c>Assets/Materials/Generated/</c>.
    ///
    /// Palette values mirror the dark-theme HTML reference described in the
    /// master prompt — fauna teal, flora green, ore pink, etc.
    ///
    /// Build-material keys are prefixed with <c>build_</c> (e.g.
    /// <c>build_stone</c>, <c>build_wood</c>) so the generator can apply
    /// PBR config (smoothness / metallic / transparency) per-material.
    ///
    /// Coop note: pure static — no runtime state, safe to call from any thread
    /// where Color/Dictionary access is permitted.
    /// </summary>
    public static class PaletteRegistry
    {
        // ----- Category / Source / Kind palette ----------------------------
        // These are the named keys callers ask for via GetByKey / GetForItem.
        private static readonly Dictionary<string, Color> _palette = BuildPalette();

        // ----- Build material palette --------------------------------------
        // Keyed by the build-material id (matching build_materials.json).
        // Exposed separately so the generator can detect them and apply the
        // appropriate PBR config.
        private static readonly Dictionary<string, Color> _buildMaterialColors = BuildBuildMaterialColors();

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Look up a palette colour by key. Unknown keys return
        /// <see cref="Color.magenta"/> so missing entries are visually obvious
        /// in the editor / runtime.
        /// </summary>
        public static Color GetByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return Color.magenta;
            if (_palette.TryGetValue(key, out var c)) return c;
            if (_buildMaterialColors.TryGetValue(key, out var bc)) return bc;
            // Allow callers to pass the "build_<name>" prefix used by the generator.
            if (key.StartsWith("build_"))
            {
                var bareKey = key.Substring("build_".Length);
                if (_buildMaterialColors.TryGetValue(bareKey, out var bc2)) return bc2;
            }
            return Color.magenta;
        }

        /// <summary>
        /// Resolve a colour for an item using the priority chain:
        ///  1. <c>isDeco</c> → deco
        ///  2. <c>isBuildBlock</c> → buildColor (per-build-material)
        ///  3. First matching category in priority order
        ///  4. Source-based for ItemKind.Source items
        ///  5. Fallback to <c>build_neutral</c>.
        /// </summary>
        public static Color GetForItem(ItemDefinition item)
        {
            if (item == null) return Color.magenta;

            // 1. Deco wins outright — many deco pieces also carry build categories.
            if (item.isDeco) return GetByKey("deco");

            // 2. Build blocks resolve via per-material colour (e.g. "stone", "wood").
            if (item.isBuildBlock)
            {
                if (!string.IsNullOrEmpty(item.buildColor) &&
                    _buildMaterialColors.TryGetValue(item.buildColor, out var bc))
                {
                    return bc;
                }
                return GetByKey("build_neutral");
            }

            // 3. Category priority (most specific first).
            if (item.HasCategory("weapon")) return GetByKey("weapon");
            if (item.HasCategory("armor")) return GetByKey("armor");
            if (item.HasCategory("food")) return GetByKey("food");
            if (item.HasCategory("vehicle")) return GetByKey("vehicle");
            if (item.HasCategory("automation")) return GetByKey("automation");
            if (item.HasCategory("power")) return GetByKey("power");
            if (item.HasCategory("gadget")) return GetByKey("gadget");
            if (item.HasCategory("deco")) return GetByKey("deco");
            if (item.HasCategory("build")) return GetByKey("build_neutral");

            // 4. Kind / source based.
            if (item.kind == ItemKind.Source)
            {
                switch (item.source)
                {
                    case ItemSource.Fauna: return GetByKey("fauna");
                    case ItemSource.Flora: return GetByKey("flora");
                    case ItemSource.Ore: return GetByKey("ore");
                    case ItemSource.Soil: return GetByKey("soil");
                    case ItemSource.Exotic: return GetByKey("exotic");
                }
            }

            if (item.kind == ItemKind.Machine) return GetByKey("machine");

            // 5. Components and unmatched products → neutral.
            return GetByKey("build_neutral");
        }

        /// <summary>
        /// All palette entries (category palette + build-material variants),
        /// each keyed by the canonical asset key used by the generator.
        /// Build-material keys are emitted with the <c>build_</c> prefix so
        /// the generator can write them as <c>Mat_build_stone.mat</c> etc.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, Color>> AllEntries
        {
            get
            {
                foreach (var kv in _palette) yield return kv;
                foreach (var kv in _buildMaterialColors)
                    yield return new KeyValuePair<string, Color>("build_" + kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Build-material keys (without the <c>build_</c> prefix) and their
        /// colour. Used by the generator to detect build materials and apply
        /// the corresponding PBR config.
        /// </summary>
        public static IReadOnlyDictionary<string, Color> BuildMaterialColors => _buildMaterialColors;

        /// <summary>
        /// True if <paramref name="paletteKey"/> is a build-material entry
        /// (carries the <c>build_</c> prefix). The PBR config branch in the
        /// generator keys off this.
        /// </summary>
        public static bool IsBuildMaterialKey(string paletteKey)
        {
            if (string.IsNullOrEmpty(paletteKey)) return false;
            if (!paletteKey.StartsWith("build_")) return false;
            var bare = paletteKey.Substring("build_".Length);
            // "build_neutral" is the catch-all category, not a build material per se,
            // but it should still get the build PBR config (stone-like).
            return bare == "neutral" || _buildMaterialColors.ContainsKey(bare);
        }

        /// <summary>
        /// Extract the bare build-material name from a "build_*" palette key.
        /// Returns the input unchanged for non-build keys.
        /// </summary>
        public static string StripBuildPrefix(string paletteKey)
        {
            if (string.IsNullOrEmpty(paletteKey)) return paletteKey;
            return paletteKey.StartsWith("build_")
                ? paletteKey.Substring("build_".Length)
                : paletteKey;
        }

        // -------------------------------------------------------------------
        // Construction
        // -------------------------------------------------------------------

        private static Dictionary<string, Color> BuildPalette()
        {
            var d = new Dictionary<string, Color>(System.StringComparer.Ordinal);

            // Sources.
            d["fauna"] = Hex("#56d3ff");        // teal
            d["flora"] = Hex("#b6f73e");        // lime green
            d["ore"] = Hex("#f778ba");          // pink/purple
            d["soil"] = Hex("#a0826d");         // brown
            d["exotic"] = Hex("#c084fc");       // violet

            // Categories.
            d["food"] = Hex("#ffb86b");         // warm orange
            d["power"] = Hex("#ffd23f");        // yellow
            d["weapon"] = Hex("#ff5577");       // red
            d["armor"] = Hex("#9b59ff");        // purple
            d["machine"] = Hex("#56d3ff");      // cyan
            d["build_neutral"] = Hex("#8b8b8b"); // grey
            d["deco"] = Hex("#7ab8ff");         // soft blue
            d["vehicle"] = Hex("#ff9b3a");      // orange
            d["automation"] = Hex("#3aff9b");   // mint
            d["gadget"] = Hex("#3affd5");       // teal/cyan

            return d;
        }

        // Build material colours. The build_materials.json file does NOT
        // carry hex colour values (its "color" field is a palette category
        // like "build" / "exotic" / "beast"), so we hardcode per-material
        // colours informed by the material's real-world appearance.
        private static Dictionary<string, Color> BuildBuildMaterialColors()
        {
            var d = new Dictionary<string, Color>(System.StringComparer.Ordinal);

            d["stone"] = Hex("#7a7a7a");        // stone grey
            d["wood"] = Hex("#8b5a2b");         // wood brown
            d["brick"] = Hex("#a85436");        // brick red-brown
            d["iron"] = Hex("#c0c0c8");         // steel
            d["copper"] = Hex("#b87333");       // copper
            d["titanium"] = Hex("#e8e8ee");     // bright steel
            d["glass"] = Hex("#a7e0ff");        // cyan-tinted
            d["obsidian"] = Hex("#1f1a26");     // black volcanic
            d["bone"] = Hex("#e8e0c8");         // off-white
            d["chitin"] = Hex("#6e3a2a");       // dark chitin red-brown
            d["frost"] = Hex("#9fd6ff");        // pale icy blue
            d["ash"] = Hex("#4a4a4a");          // dark ash grey
            d["marsh"] = Hex("#5a6a3a");        // murky green-brown
            d["cinder"] = Hex("#b04020");       // hot ember red
            d["vord"] = Hex("#c084fc");         // exotic violet (vord-plated)
            d["kin"] = Hex("#d4b070");          // golden ruin
            d["void"] = Hex("#5a2db0");         // void-etched purple

            // Extra build-material colours used by gold / silver / fabric.
            // These don't appear in build_materials.json but may show up via
            // ItemDefinition.buildColor — keep them as fallbacks.
            d["gold"] = Hex("#ffd34a");
            d["silver"] = Hex("#d8d8e0");
            d["fabric"] = Hex("#c89968");

            return d;
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            // Fallback so initialization never crashes on a typo.
            return Color.magenta;
        }
    }
}
