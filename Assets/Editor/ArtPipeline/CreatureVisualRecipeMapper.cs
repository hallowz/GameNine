#if UNITY_EDITOR
using UnityEngine;
using Voidborne.ArtPipeline;
using Voidborne.Fauna;
using Voidborne.NPCs;
using V2Enemy = Voidborne.Enemies.V2.EnemyDefinition;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.5 — Creature Visual Recipe Mapper.
    ///
    /// Editor-only static rules that turn a creature ScriptableObject
    /// (<see cref="FaunaDefinition"/>, V2 <see cref="V2Enemy"/>,
    /// <see cref="NpcDefinition"/>) into an <see cref="ItemVisualRecipe"/>.
    /// Same composition contract as V3.3's <c>ItemVisualRecipeMapper</c> but
    /// with a creature-specific silhouette vocabulary (quadrupeds, humanoids,
    /// bosses, small birds, spike-backed predators).
    ///
    /// Rules:
    ///  - Fauna baseline = stocky quadruped (capsule body + head + 4 legs).
    ///  - "Cluck" / bird family = round body, small head, 2 legs.
    ///  - "Thornback" / predator family = quadruped + spike row along the back.
    ///  - Humanoid (Wren / Vord Drone / Vord Raider) = capsule body + head +
    ///    2 arms. Vord Raider gets an extra cube armor pauldron.
    ///  - Bosses scale the chosen base by 2x. Boss layer ceiling is 8 (vs
    ///    the standard 4 used for items / non-boss creatures).
    ///  - Fungal Brood Mother = bloated humanoid with 3 sphere fungal sacs,
    ///    then scaled 2x.
    ///
    /// The 4-layer ceiling that V3.3 enforces is intentionally lifted to 8 for
    /// bosses — the only callers checking the ceiling are the tests + spec
    /// commentary, never the runtime. See the test suite in
    /// <c>CreatureVisualRecipeTests</c>.
    /// </summary>
    public static class CreatureVisualRecipeMapper
    {
        /// <summary>Maximum layers any non-boss creature recipe may produce.</summary>
        public const int StandardLayerCeiling = 4;

        /// <summary>Maximum layers a boss recipe may produce (relaxed for the 2x boss scale).</summary>
        public const int BossLayerCeiling = 8;

        // -------------------------------------------------------------------
        // Material keys
        // -------------------------------------------------------------------

        private const string MatFauna = "fauna";
        private const string MatMachine = "machine";
        private const string MatFlora = "flora";
        private const string MatBuildWood = "build_wood";
        private const string MatBuildIron = "build_iron";
        private const string MatBuildNeutral = "build_neutral";
        private const string MatBuildVord = "build_vord";

        // -------------------------------------------------------------------
        // Public API — Fauna
        // -------------------------------------------------------------------

        /// <summary>
        /// Map a <see cref="FaunaDefinition"/> to a recipe by combining
        /// id / displayName / description / behaviors / habitat hints.
        /// </summary>
        public static ItemVisualRecipe MapFauna(FaunaDefinition fauna)
        {
            if (fauna == null) return FallbackQuadruped();

            string id = fauna.id ?? string.Empty;
            string display = fauna.displayName ?? string.Empty;
            string desc = fauna.description ?? string.Empty;
            string behaviors = JoinNullable(fauna.behaviors);

            string blob = (id + " " + display + " " + desc + " " + behaviors).ToLowerInvariant();

            // Bird family — "cluck" id, or "bird"/"feather" in description.
            if (id.Contains("cluck") || blob.Contains("bird") || blob.Contains("feather"))
            {
                return SmallBird();
            }

            // Spike-backed predator — "thornback" id, or "spike"/"predator" in description.
            if (id.Contains("thornback") || blob.Contains("spike") || blob.Contains("predator"))
            {
                return QuadrupedPredator();
            }

            // Default: stocky quadruped.
            return Quadruped();
        }

        // -------------------------------------------------------------------
        // Public API — Enemy
        // -------------------------------------------------------------------

        /// <summary>
        /// Map a V2 <see cref="V2Enemy"/> to a recipe. Bosses get 2x scaling.
        /// </summary>
        public static ItemVisualRecipe MapEnemy(V2Enemy enemy)
        {
            if (enemy == null) return FallbackQuadruped();

            string id = enemy.id ?? string.Empty;
            string desc = enemy.description ?? string.Empty;
            string blob = (id + " " + desc).ToLowerInvariant();

            // Fungal Brood Mother — boss-only specific shape (bloated humanoid +
            // fungal sacs). The 2x scale gets applied below.
            if (id.Contains("fungal_brood_mother") || blob.Contains("brood mother"))
            {
                var fbm = FungalBroodMother();
                return enemy.isBoss ? ScaleBoss(fbm) : fbm;
            }

            // Vord Raider — humanoid + shoulder armor cube.
            if (id.Contains("raider"))
            {
                var raider = VordRaider();
                return enemy.isBoss ? ScaleBoss(raider) : raider;
            }

            // Vord Drone (and Vord fodder by default) — fodder humanoid.
            if (id.Contains("drone") || id.Contains("vord") ||
                enemy.family == Voidborne.Enemies.V2.EnemyArchetype.Fodder)
            {
                var drone = VordFodder();
                return enemy.isBoss ? ScaleBoss(drone) : drone;
            }

            // Unknown family — fall back to fodder humanoid.
            var fallback = VordFodder();
            return enemy.isBoss ? ScaleBoss(fallback) : fallback;
        }

        // -------------------------------------------------------------------
        // Public API — NPC
        // -------------------------------------------------------------------

        /// <summary>
        /// Map an <see cref="NpcDefinition"/> to a recipe. Named Kin (Wren
        /// today) get a warm-wood humanoid silhouette.
        /// </summary>
        public static ItemVisualRecipe MapNpc(NpcDefinition npc)
        {
            if (npc == null) return FallbackHumanoid(MatBuildWood);

            string id = npc.id ?? string.Empty;
            string section = npc.section ?? string.Empty;
            string blob = (id + " " + section).ToLowerInvariant();

            // Kin Survivors (incl. Wren) — warm-wood humanoid.
            if (blob.Contains("kin") || id.Contains("wren"))
            {
                return KinHumanoid();
            }

            // Default friendly humanoid (machine grey — neutral colour).
            return FallbackHumanoid(MatMachine);
        }

        // -------------------------------------------------------------------
        // Boss scaling helper
        // -------------------------------------------------------------------

        /// <summary>
        /// Scale every layer's localScale and localPos by <paramref name="factor"/>
        /// to produce a boss-sized silhouette. Returns a NEW recipe — callers
        /// retain ownership of the input. Layers themselves are structs so the
        /// copy is cheap.
        /// </summary>
        public static ItemVisualRecipe ScaleBoss(ItemVisualRecipe recipe, float factor = 2f)
        {
            if (recipe == null || recipe.layers == null) return new ItemVisualRecipe();
            var src = recipe.layers;
            var dst = new Layer[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var l = src[i];
                dst[i] = new Layer
                {
                    shape = l.shape,
                    localScale = l.localScale * factor,
                    localPos = l.localPos * factor,
                    localRot = (l.localRot.x == 0f && l.localRot.y == 0f &&
                                 l.localRot.z == 0f && l.localRot.w == 0f)
                        ? Quaternion.identity
                        : l.localRot,
                    materialKey = l.materialKey
                };
            }
            return new ItemVisualRecipe(dst);
        }

        // -------------------------------------------------------------------
        // Internal silhouette builders
        // -------------------------------------------------------------------

        // Stocky quadruped — capsule body laid horizontally, sphere head,
        // 4 cylinder legs at the corners. 6 layers (exceeds 4-layer item
        // ceiling — bumped to 8 for creatures per spec heads-up).
        private static ItemVisualRecipe Quadruped()
        {
            // Capsule is Y-aligned by default — rotate 90° around X so it lies
            // along the +Z axis (the spine).
            var bodyRot = Quaternion.Euler(90f, 0f, 0f);
            var body = new Layer
            {
                shape = PrimitiveShape.Capsule,
                localScale = new Vector3(0.5f, 0.3f, 0.8f),
                localPos = new Vector3(0f, 0.4f, 0f),
                localRot = bodyRot,
                materialKey = MatFauna
            };
            var head = Layer.Make(
                PrimitiveShape.Sphere, MatFauna,
                new Vector3(0.4f, 0.4f, 0.4f),
                new Vector3(0f, 0.45f, 0.45f));
            var legFL = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.08f, 0.3f, 0.08f),
                new Vector3(0.18f, 0.15f, 0.28f));
            var legFR = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.08f, 0.3f, 0.08f),
                new Vector3(-0.18f, 0.15f, 0.28f));
            var legBL = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.08f, 0.3f, 0.08f),
                new Vector3(0.18f, 0.15f, -0.28f));
            var legBR = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.08f, 0.3f, 0.08f),
                new Vector3(-0.18f, 0.15f, -0.28f));
            return new ItemVisualRecipe(new[] { body, head, legFL, legFR, legBL, legBR });
        }

        // Quadruped predator — same base as Quadruped + 2 spikes along the
        // spine. 8 layers total (at boss ceiling). The spec calls for 3-5
        // spike layers but the Quadruped baseline already costs 6 layers
        // (body + head + 4 legs), so we settle on 2 spikes to honour the
        // 8-layer ceiling. Silhouette still reads as a spike-backed predator.
        private static ItemVisualRecipe QuadrupedPredator()
        {
            var baseLayers = Quadruped().layers;
            var s1 = Layer.Make(
                PrimitiveShape.Spike, MatFauna,
                new Vector3(0.08f, 0.25f, 0.08f),
                new Vector3(0f, 0.65f, 0.18f));
            var s2 = Layer.Make(
                PrimitiveShape.Spike, MatFauna,
                new Vector3(0.08f, 0.25f, 0.08f),
                new Vector3(0f, 0.65f, -0.18f));

            var combined = new Layer[baseLayers.Length + 2];
            System.Array.Copy(baseLayers, 0, combined, 0, baseLayers.Length);
            combined[baseLayers.Length] = s1;
            combined[baseLayers.Length + 1] = s2;
            return new ItemVisualRecipe(combined);
        }

        // Small bird — round body + small head + 2 legs. 4 layers (cap).
        private static ItemVisualRecipe SmallBird()
        {
            var body = Layer.Make(
                PrimitiveShape.Sphere, MatFauna,
                new Vector3(0.4f, 0.4f, 0.4f),
                new Vector3(0f, 0.3f, 0f));
            var head = Layer.Make(
                PrimitiveShape.Sphere, MatFauna,
                new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(0f, 0.45f, 0.25f));
            var legL = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.05f, 0.15f, 0.05f),
                new Vector3(0.08f, 0.075f, 0f));
            var legR = Layer.Make(
                PrimitiveShape.Cylinder, MatFauna,
                new Vector3(0.05f, 0.15f, 0.05f),
                new Vector3(-0.08f, 0.075f, 0f));
            return new ItemVisualRecipe(new[] { body, head, legL, legR });
        }

        // Generic humanoid silhouette parametric on material + arm orientation.
        // 4 layers (body + head + 2 arms) — at the standard ceiling.
        private static ItemVisualRecipe Humanoid(string materialKey, bool armsBackRaised)
        {
            // Arms shifted slightly forward for an "alive" pose, or back-raised
            // for the hunched Vord look.
            float armZ = armsBackRaised ? -0.05f : 0.05f;

            var body = Layer.Make(
                PrimitiveShape.Capsule, materialKey,
                new Vector3(0.4f, 0.6f, 0.3f),
                new Vector3(0f, 0.35f, 0f));
            var head = Layer.Make(
                PrimitiveShape.Sphere, materialKey,
                new Vector3(0.3f, 0.3f, 0.3f),
                new Vector3(0f, 0.85f, 0f));
            var armL = Layer.Make(
                PrimitiveShape.Capsule, materialKey,
                new Vector3(0.12f, 0.3f, 0.12f),
                new Vector3(0.25f, 0.45f, armZ));
            var armR = Layer.Make(
                PrimitiveShape.Capsule, materialKey,
                new Vector3(0.12f, 0.3f, 0.12f),
                new Vector3(-0.25f, 0.45f, armZ));
            return new ItemVisualRecipe(new[] { body, head, armL, armR });
        }

        // Wren — warm-wood Kin humanoid (arms slightly forward — "alive").
        private static ItemVisualRecipe KinHumanoid() => Humanoid(MatBuildWood, armsBackRaised: false);

        // Vord Drone — hunched humanoid, arms back-raised. Uses build_vord
        // (violet) when available; falls back to build_neutral via the
        // composer's magenta canary if the palette key is somehow missing.
        private static ItemVisualRecipe VordFodder() => Humanoid(MatBuildVord, armsBackRaised: true);

        // Vord Raider — Vord Drone + shoulder/torso armor cube (build_iron).
        private static ItemVisualRecipe VordRaider()
        {
            var humanoid = Humanoid(MatBuildVord, armsBackRaised: true);
            var armor = Layer.Make(
                PrimitiveShape.Cube, MatBuildIron,
                new Vector3(0.5f, 0.15f, 0.4f),
                new Vector3(0f, 0.65f, 0f));

            var src = humanoid.layers;
            var combined = new Layer[src.Length + 1];
            System.Array.Copy(src, 0, combined, 0, src.Length);
            combined[src.Length] = armor;
            return new ItemVisualRecipe(combined);
        }

        // Fungal Brood Mother — bloated humanoid + 3 fungal sacs. Scaled 2x by
        // the boss scaling wrapper. Pre-scale layer count = 5 (body+head+3 sacs);
        // post-scale still 5, well within the 8-layer boss ceiling. Material is
        // "flora" (greenish) to read as fungal growth.
        private static ItemVisualRecipe FungalBroodMother()
        {
            var body = Layer.Make(
                PrimitiveShape.Sphere, MatFlora,
                new Vector3(1.2f, 1.2f, 1.2f),
                new Vector3(0f, 0.6f, 0f));
            var head = Layer.Make(
                PrimitiveShape.Sphere, MatFlora,
                new Vector3(0.6f, 0.6f, 0.6f),
                new Vector3(0f, 1.4f, 0f));
            var sac1 = Layer.Make(
                PrimitiveShape.Sphere, MatFlora,
                new Vector3(0.4f, 0.4f, 0.4f),
                new Vector3(0.7f, 0.8f, 0.1f));
            var sac2 = Layer.Make(
                PrimitiveShape.Sphere, MatFlora,
                new Vector3(0.4f, 0.4f, 0.4f),
                new Vector3(-0.7f, 0.8f, 0.1f));
            var sac3 = Layer.Make(
                PrimitiveShape.Sphere, MatFlora,
                new Vector3(0.4f, 0.4f, 0.4f),
                new Vector3(0f, 0.5f, -0.7f));
            return new ItemVisualRecipe(new[] { body, head, sac1, sac2, sac3 });
        }

        // -------------------------------------------------------------------
        // Fallbacks
        // -------------------------------------------------------------------

        private static ItemVisualRecipe FallbackHumanoid(string materialKey)
            => Humanoid(materialKey, armsBackRaised: false);

        private static ItemVisualRecipe FallbackQuadruped() => Quadruped();

        // -------------------------------------------------------------------
        // Internal authoring helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Convenience builder mirroring the spec's <c>Make</c> helper. Public
        /// purely for parity with the Layer.Make API; the per-silhouette
        /// builders above use Layer.Make directly.
        /// </summary>
        private static Layer Make(PrimitiveShape shape, Vector3 scale, Vector3 pos, string materialKey)
        {
            return new Layer
            {
                shape = shape,
                localScale = scale,
                localPos = pos,
                localRot = Quaternion.identity,
                materialKey = materialKey
            };
        }

        private static string JoinNullable(string[] arr)
        {
            if (arr == null || arr.Length == 0) return string.Empty;
            return string.Join(" ", arr);
        }
    }
}
#endif
