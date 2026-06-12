#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Voidborne.ArtPipeline;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.3 — Item Visual Recipe Mapper.
    ///
    /// Editor-only static rules that turn an <see cref="ItemDefinition"/>
    /// into an <see cref="ItemVisualRecipe"/>. The rules are intentionally
    /// shallow — every Core 60 item resolves to a 1–4 layer composition of
    /// primitives + palette materials. Visual polish is M8, not now.
    ///
    /// Rule priority (first match wins):
    ///   1. Source items by <c>ItemSource</c> (Ore/Flora/Fauna/Soil/None).
    ///   2. Machine items by id substring (furnace, campfire, boiler, …).
    ///   3. Component items by id substring (ingot, powder, plank, wire, …)
    ///      and category (weapon, armor, ammo).
    ///   4. Product items by isBuildBlock + id suffix (cube/slab/panel/door)
    ///      or by isDeco.
    ///   5. Fallback: small cube with build_neutral.
    /// </summary>
    public static class ItemVisualRecipeMapper
    {
        // -------------------------------------------------------------------
        // Material keys — canonical palette / build material identifiers.
        // -------------------------------------------------------------------

        private const string MatFlora = "flora";
        private const string MatOre = "ore";
        private const string MatFauna = "fauna";
        private const string MatSoil = "soil";
        private const string MatMachine = "machine";
        private const string MatWeapon = "weapon";
        private const string MatArmor = "armor";
        private const string MatBuildNeutral = "build_neutral";
        private const string MatBuildWood = "build_wood";
        private const string MatBuildIron = "build_iron";
        private const string MatBuildStone = "build_stone";
        private const string MatBuildGlass = "build_glass";
        private const string MatBuildCopper = "build_copper";
        private const string MatDeco = "deco";

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Build an <see cref="ItemVisualRecipe"/> for <paramref name="item"/>
        /// per the documented rule priority. Returns a fallback recipe (small
        /// cube, build_neutral) for unmatched items rather than null — the
        /// composer never has to special-case missing recipes.
        /// </summary>
        public static ItemVisualRecipe MapItem(ItemDefinition item)
        {
            if (item == null)
            {
                return new ItemVisualRecipe(new[] { Layer.Make(PrimitiveShape.Cube, MatBuildNeutral) });
            }

            // Special-case the two "liquid puddle" sources before the generic
            // Source branch — water carries src=Soil and oil_seep carries
            // src=Ore in the live data, so the spec's "src==None -> disc"
            // intent never fires from that branch alone. Match by id so the
            // intended flat disc still lands.
            string id = item.itemId ?? string.Empty;
            if (id == "water")
            {
                var disc = Layer.Make(
                    PrimitiveShape.Disc,
                    MatFlora, // placeholder blue-ish — palette has no dedicated water key yet.
                    new Vector3(0.7f, 0.05f, 0.7f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { disc });
            }
            if (id == "oil_seep")
            {
                var disc = Layer.Make(
                    PrimitiveShape.Disc,
                    MatOre,
                    new Vector3(0.7f, 0.05f, 0.7f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { disc });
            }

            // 1. Source items — keyed by ItemSource.
            if (item.kind == ItemKind.Source)
            {
                return MapSource(item);
            }

            // 2. Machine items — keyed by id substring.
            if (item.kind == ItemKind.Machine)
            {
                return MapMachine(item);
            }

            // 3. Component items.
            if (item.kind == ItemKind.Component)
            {
                return MapComponent(item);
            }

            // 4. Product items — build block or deco.
            if (item.kind == ItemKind.Product)
            {
                return MapProduct(item);
            }

            // 5. Fallback.
            return new ItemVisualRecipe(new[]
            {
                Layer.Make(PrimitiveShape.Cube, MatBuildNeutral, new Vector3(0.4f, 0.4f, 0.4f), Vector3.zero)
            });
        }

        // -------------------------------------------------------------------
        // Source rules
        // -------------------------------------------------------------------

        private static ItemVisualRecipe MapSource(ItemDefinition item)
        {
            switch (item.source)
            {
                case ItemSource.Ore:
                {
                    var layer = Layer.Make(
                        PrimitiveShape.Sphere,
                        MatOre,
                        new Vector3(0.7f, 0.7f, 0.7f),
                        Vector3.zero);
                    return new ItemVisualRecipe(new[] { layer });
                }

                case ItemSource.Flora:
                {
                    // Cylinder stem + sphere foliage.
                    var stem = Layer.Make(
                        PrimitiveShape.Cylinder,
                        MatFlora,
                        new Vector3(0.2f, 0.6f, 0.2f),
                        new Vector3(0f, 0.3f, 0f));
                    var foliage = Layer.Make(
                        PrimitiveShape.Sphere,
                        MatFlora,
                        new Vector3(0.5f, 0.5f, 0.5f),
                        new Vector3(0f, 0.7f, 0f));
                    return new ItemVisualRecipe(new[] { stem, foliage });
                }

                case ItemSource.Fauna:
                {
                    // Capsule body + sphere head. Core 60 fauna sources are
                    // milk / raw_meat / raw_fish / egg — no predator-vs-prey
                    // distinction needed yet.
                    var body = Layer.Make(
                        PrimitiveShape.Capsule,
                        MatFauna,
                        new Vector3(0.5f, 0.5f, 0.5f),
                        new Vector3(0f, 0.3f, 0f));
                    var head = Layer.Make(
                        PrimitiveShape.Sphere,
                        MatFauna,
                        new Vector3(0.35f, 0.35f, 0.35f),
                        new Vector3(0f, 0.75f, 0f));
                    return new ItemVisualRecipe(new[] { body, head });
                }

                case ItemSource.Soil:
                {
                    // Soil sources: stone, clay, sand, water. Water is "None"
                    // (None branch); stone/clay/sand carry buildColor sometimes
                    // but not always — fall back to soil-tinted cube. If the
                    // item carries a buildColor pointing at a known build
                    // material, prefer that material key.
                    string mat = PreferredBuildKeyOrFallback(item, MatBuildNeutral);
                    var layer = Layer.Make(
                        PrimitiveShape.Cube,
                        mat,
                        new Vector3(0.6f, 0.6f, 0.6f),
                        Vector3.zero);
                    return new ItemVisualRecipe(new[] { layer });
                }

                case ItemSource.None:
                default:
                {
                    // water, oil_seep — flat disc.
                    string mat = item.itemId != null && item.itemId.Contains("oil")
                        ? MatOre
                        : MatFlora; // Use flora (greenish) as a faux-water fallback until a water palette key exists.
                    var disc = Layer.Make(
                        PrimitiveShape.Disc,
                        mat,
                        new Vector3(0.7f, 0.05f, 0.7f),
                        Vector3.zero);
                    return new ItemVisualRecipe(new[] { disc });
                }
            }
        }

        // -------------------------------------------------------------------
        // Machine rules
        // -------------------------------------------------------------------

        private static ItemVisualRecipe MapMachine(ItemDefinition item)
        {
            string id = item.itemId ?? string.Empty;

            // Boiler is checked BEFORE the generic furnace rule because
            // "steam_boiler" doesn't contain "furnace" but does need its own
            // silhouette. Order matters here — favour the more specific id
            // substring first.
            if (id.Contains("boiler"))
            {
                var body = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatMachine,
                    new Vector3(0.7f, 0.9f, 0.7f),
                    Vector3.zero);
                var top = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.4f, 0.2f, 0.4f),
                    new Vector3(0f, 0.6f, 0f));
                return new ItemVisualRecipe(new[] { body, top });
            }

            if (id.Contains("furnace"))
            {
                var body = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.8f, 0.8f, 0.8f),
                    Vector3.zero);
                var chimney = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.2f, 0.5f, 0.2f),
                    new Vector3(0f, 0.7f, 0f));
                var glow = Layer.Make(
                    PrimitiveShape.Disc,
                    MatMachine,
                    new Vector3(0.3f, 0.05f, 0.3f),
                    new Vector3(0f, 1.0f, 0f));
                return new ItemVisualRecipe(new[] { body, chimney, glow });
            }

            if (id.Contains("campfire"))
            {
                var cone = Layer.Make(
                    PrimitiveShape.Cone,
                    MatMachine,
                    new Vector3(0.6f, 0.4f, 0.6f),
                    new Vector3(0f, 0.05f, 0f));
                var baseDisc = Layer.Make(
                    PrimitiveShape.Disc,
                    MatMachine,
                    new Vector3(0.7f, 0.05f, 0.7f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { baseDisc, cone });
            }

            if (id.Contains("chest"))
            {
                var cube = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildWood,
                    new Vector3(0.8f, 0.5f, 0.6f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { cube });
            }

            if (id.Contains("conveyor"))
            {
                var panel = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(1f, 0.1f, 0.4f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { panel });
            }

            if (id.Contains("press"))
            {
                var baseCube = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.8f, 0.4f, 0.8f),
                    Vector3.zero);
                var ram = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.6f, 0.3f, 0.6f),
                    new Vector3(0f, 0.7f, 0f));
                return new ItemVisualRecipe(new[] { baseCube, ram });
            }

            if (id.Contains("crusher"))
            {
                var box = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.7f, 0.7f, 0.7f),
                    Vector3.zero);
                var pestle = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatMachine,
                    new Vector3(0.3f, 0.4f, 0.3f),
                    new Vector3(0f, 0.6f, 0f));
                return new ItemVisualRecipe(new[] { box, pestle });
            }

            if (id.Contains("composter"))
            {
                var box = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.6f, 0.7f, 0.6f),
                    Vector3.zero);
                var rim = Layer.Make(
                    PrimitiveShape.Torus,
                    MatMachine,
                    new Vector3(0.7f, 0.1f, 0.7f),
                    new Vector3(0f, 0.4f, 0f));
                return new ItemVisualRecipe(new[] { box, rim });
            }

            if (id.Contains("drying_rack"))
            {
                var baseSlab = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildWood,
                    new Vector3(0.6f, 0.1f, 0.6f),
                    Vector3.zero);
                var p1 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.05f, 0.5f, 0.05f),
                    new Vector3(0.25f, 0.3f, 0.25f));
                var p2 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.05f, 0.5f, 0.05f),
                    new Vector3(-0.25f, 0.3f, 0.25f));
                var p3 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.05f, 0.5f, 0.05f),
                    new Vector3(0.25f, 0.3f, -0.25f));
                // 4 layers is the recipe ceiling — base + 3 posts. Drop the
                // 4th post to stay within bounds; silhouette still reads as
                // a quad-post rack.
                return new ItemVisualRecipe(new[] { baseSlab, p1, p2, p3 });
            }

            if (id.Contains("workbench"))
            {
                // 1 top + 3 legs (4 layers, recipe ceiling).
                var top = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildWood,
                    new Vector3(0.9f, 0.1f, 0.6f),
                    new Vector3(0f, 0.5f, 0f));
                var leg1 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.08f, 0.5f, 0.08f),
                    new Vector3(0.35f, 0.25f, 0.2f));
                var leg2 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.08f, 0.5f, 0.08f),
                    new Vector3(-0.35f, 0.25f, 0.2f));
                var leg3 = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.08f, 0.5f, 0.08f),
                    new Vector3(0.35f, 0.25f, -0.2f));
                return new ItemVisualRecipe(new[] { top, leg1, leg2, leg3 });
            }

            if (id.Contains("generator"))
            {
                var body = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.8f, 0.6f, 0.8f),
                    Vector3.zero);
                var control = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.3f, 0.2f, 0.3f),
                    new Vector3(0f, 0.4f, 0f));
                return new ItemVisualRecipe(new[] { body, control });
            }

            if (id.Contains("turret"))
            {
                var baseCyl = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatMachine,
                    new Vector3(0.5f, 0.3f, 0.5f),
                    Vector3.zero);
                var head = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.4f, 0.3f, 0.4f),
                    new Vector3(0f, 0.45f, 0f));
                var barrel = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatMachine,
                    new Vector3(0.1f, 0.4f, 0.1f),
                    new Vector3(0f, 0.45f, 0.3f));
                return new ItemVisualRecipe(new[] { baseCyl, head, barrel });
            }

            if (id.Contains("inserter"))
            {
                var baseCube = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.5f, 0.3f, 0.5f),
                    Vector3.zero);
                var arm = Layer.Make(
                    PrimitiveShape.Capsule,
                    MatMachine,
                    new Vector3(0.15f, 0.3f, 0.15f),
                    new Vector3(0f, 0.4f, 0f));
                return new ItemVisualRecipe(new[] { baseCube, arm });
            }

            // Default machine fallback.
            var fallback = Layer.Make(
                PrimitiveShape.Cube,
                MatMachine,
                new Vector3(0.8f, 0.8f, 0.8f),
                Vector3.zero);
            return new ItemVisualRecipe(new[] { fallback });
        }

        // -------------------------------------------------------------------
        // Component rules
        // -------------------------------------------------------------------

        private static ItemVisualRecipe MapComponent(ItemDefinition item)
        {
            string id = item.itemId ?? string.Empty;

            // Categories — checked first so e.g. an iron sword (id includes
            // "sword" not "ingot") routes to the weapon branch. The Core 60
            // weapons lack an explicit "weapon" category in the data, so we
            // also accept weapon-shaped ids as a heads-up safety net (sword/
            // spear/pistol/rifle/bow/knife/axe).
            bool meleeId = id.Contains("sword") || id.Contains("spear") || id.Contains("knife") || id.Contains("axe");
            bool rangedId = id.Contains("pistol") || id.Contains("rifle");
            if (item.HasCategory("weapon") || meleeId || rangedId)
            {
                var shape = meleeId ? PrimitiveShape.Wedge : PrimitiveShape.Capsule;
                var layer = Layer.Make(
                    shape,
                    MatWeapon,
                    new Vector3(0.4f, 0.6f, 0.4f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (item.HasCategory("armor"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Wedge,
                    MatArmor,
                    new Vector3(0.6f, 0.5f, 0.4f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (item.HasCategory("ammo") || id.EndsWith("bullet") || id.Contains("casing"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatWeapon,
                    new Vector3(0.15f, 0.3f, 0.15f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            // ID-keyed component shapes.
            if (id.Contains("cable") || id.Contains("wire"))
            {
                // Conductive cable -> copper tint when item carries the
                // electric property tag; otherwise machine grey.
                string mat = item.HasProperty(Voidborne.Data.MaterialProperties.Conducts_Electric)
                    ? MatBuildCopper
                    : MatMachine;
                var layer = Layer.Make(
                    PrimitiveShape.Cylinder,
                    mat,
                    new Vector3(0.1f, 0.7f, 0.1f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("ingot"))
            {
                string mat = PreferredBuildKeyOrFallback(item, ResolveIngotMaterial(id));
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    mat,
                    new Vector3(0.6f, 0.3f, 0.3f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("powder"))
            {
                string mat = PreferredBuildKeyOrFallback(item, ResolveIngotMaterial(id));
                var layer = Layer.Make(
                    PrimitiveShape.Disc,
                    mat,
                    new Vector3(0.5f, 0.1f, 0.5f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("plank"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildWood,
                    new Vector3(0.9f, 0.1f, 0.4f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("wheel"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Torus,
                    MatBuildWood,
                    new Vector3(0.6f, 0.6f, 0.6f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("chassis"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildIron,
                    new Vector3(1.0f, 0.3f, 0.6f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("nail"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Spike,
                    MatBuildIron,
                    new Vector3(0.25f, 1.0f, 0.25f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("glass"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatBuildGlass,
                    new Vector3(0.5f, 0.6f, 0.05f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("battery"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.3f, 0.4f, 0.3f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("junction") || id.Contains("sink"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatMachine,
                    new Vector3(0.3f, 0.3f, 0.3f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            // Hunting bow / arrow_shaft etc. fall through here.
            if (id.Contains("bow"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Capsule,
                    MatBuildWood,
                    new Vector3(0.15f, 0.7f, 0.15f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (id.Contains("shaft") || id.Contains("arrow"))
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cylinder,
                    MatBuildWood,
                    new Vector3(0.08f, 0.6f, 0.08f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            // Default component fallback — small cube with category-derived
            // material.
            string fallbackMat = PaletteRegistry.GetForItem(item) == Color.magenta
                ? MatBuildNeutral
                : ResolveFirstCategoryKey(item, MatBuildNeutral);
            var fallback = Layer.Make(
                PrimitiveShape.Cube,
                fallbackMat,
                new Vector3(0.4f, 0.4f, 0.4f),
                Vector3.zero);
            return new ItemVisualRecipe(new[] { fallback });
        }

        // -------------------------------------------------------------------
        // Product rules
        // -------------------------------------------------------------------

        private static ItemVisualRecipe MapProduct(ItemDefinition item)
        {
            string id = item.itemId ?? string.Empty;

            if (item.isBuildBlock)
            {
                PrimitiveShape shape;
                if (id.EndsWith("_cube")) shape = PrimitiveShape.Cube;
                else if (id.EndsWith("_slab")) shape = PrimitiveShape.Slab;
                else if (id.EndsWith("_panel")) shape = PrimitiveShape.Panel;
                else if (id.EndsWith("_door")) shape = PrimitiveShape.Door;
                else shape = PrimitiveShape.Cube;

                string mat = !string.IsNullOrEmpty(item.buildColor)
                    ? "build_" + item.buildColor
                    : MatBuildNeutral;

                var layer = Layer.Make(shape, mat, Vector3.one, Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            if (item.isDeco)
            {
                var layer = Layer.Make(
                    PrimitiveShape.Cube,
                    MatDeco,
                    new Vector3(0.6f, 0.6f, 0.6f),
                    Vector3.zero);
                return new ItemVisualRecipe(new[] { layer });
            }

            // Plain product fallback.
            var fallback = Layer.Make(
                PrimitiveShape.Cube,
                MatBuildNeutral,
                new Vector3(0.4f, 0.4f, 0.4f),
                Vector3.zero);
            return new ItemVisualRecipe(new[] { fallback });
        }

        // -------------------------------------------------------------------
        // Material key helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Return a "build_*" key derived from <see cref="ItemDefinition.buildColor"/>
        /// when it points at a known build material; otherwise the supplied
        /// fallback key.
        /// </summary>
        private static string PreferredBuildKeyOrFallback(ItemDefinition item, string fallback)
        {
            if (item == null || string.IsNullOrEmpty(item.buildColor)) return fallback;
            string candidate = "build_" + item.buildColor;
            // Check the build-material colour table — IsBuildMaterialKey
            // accepts any "build_*" key whose bare name is registered.
            if (PaletteRegistry.IsBuildMaterialKey(candidate)) return candidate;
            return fallback;
        }

        /// <summary>
        /// Map an ingot / powder id to a build-material key. Used as the
        /// fallback when the item carries no <see cref="ItemDefinition.buildColor"/>.
        /// </summary>
        private static string ResolveIngotMaterial(string id)
        {
            if (string.IsNullOrEmpty(id)) return MatBuildIron;
            if (id.Contains("copper")) return MatBuildCopper;
            if (id.Contains("iron")) return MatBuildIron;
            if (id.Contains("gold")) return "build_gold";
            if (id.Contains("silver")) return "build_silver";
            if (id.Contains("titanium")) return "build_titanium";
            return MatBuildIron;
        }

        /// <summary>
        /// First category that maps to a registered palette material; used
        /// for the component fallback recipe so e.g. a "power" component
        /// gets its power-yellow material.
        /// </summary>
        private static string ResolveFirstCategoryKey(ItemDefinition item, string fallback)
        {
            if (item == null || item.categories == null) return fallback;
            var known = new HashSet<string>(System.StringComparer.Ordinal)
            {
                "weapon", "armor", "food", "vehicle", "automation", "power",
                "gadget", "deco", "build"
            };
            foreach (var cat in item.categories)
            {
                if (string.IsNullOrEmpty(cat)) continue;
                if (known.Contains(cat))
                {
                    return cat == "build" ? MatBuildNeutral : cat;
                }
            }
            return fallback;
        }
    }
}
#endif
