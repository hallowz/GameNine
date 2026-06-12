#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.Editor.ArtPipeline;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Volume 3.6 — One-Click Generate All Visuals tests.
    ///
    /// Drives <see cref="GenerateAllVisuals.RunPipeline"/> end-to-end and
    /// verifies the full Volume 3 visual asset set lands on disk, and that
    /// re-running produces the same counts (idempotence).
    ///
    /// Tests call <c>RunPipeline()</c> directly to bypass the menu's
    /// confirmation dialog.
    /// </summary>
    public class GenerateAllVisualsTests
    {
        private const string MaterialsFolder = "Assets/Materials/Generated";
        private const string PrimitivesFolder = "Assets/Models/Generated/Primitives";
        private const string ItemPrefabsFolder = "Assets/Prefabs/Items";
        private const string FaunaPrefabsFolder = "Assets/Prefabs/Fauna";
        private const string EnemyPrefabsFolder = "Assets/Prefabs/Enemies";
        private const string NpcPrefabsFolder = "Assets/Prefabs/NPCs";
        private const string IconsFolder = "Assets/Textures/Icons";

        [Test]
        public void Pipeline_CompletesAllStages()
        {
            Assert.DoesNotThrow(
                () => GenerateAllVisuals.RunPipeline(),
                "GenerateAllVisuals.RunPipeline threw — full Volume 3 pipeline failed.");

            // Stage 1: Materials. At least 30 under Assets/Materials/Generated/.
            int materials = CountFiles(MaterialsFolder, "*.mat");
            Assert.GreaterOrEqual(materials, 30,
                $"Expected >= 30 materials under {MaterialsFolder}/, found {materials}.");

            // Stage 2: Primitive meshes. Exactly 14 (one per PrimitiveShape).
            int primitives = CountFiles(PrimitivesFolder, "*.asset");
            Assert.AreEqual(14, primitives,
                $"Expected 14 primitive meshes under {PrimitivesFolder}/, found {primitives}.");

            // Stage 3: Item prefabs. 60 world prefabs (filename does not end
            // with "_placed.prefab"). Placed variants are a bonus, not required
            // by the count.
            int itemPrefabs = CountItemWorldPrefabs(ItemPrefabsFolder);
            Assert.AreEqual(60, itemPrefabs,
                $"Expected 60 world item prefabs under {ItemPrefabsFolder}/, found {itemPrefabs}.");

            // Stage 4: Creature prefabs. 3 fauna + 3 enemies + 1 NPC. Other
            // .prefab files in those folders are legacy V15/V16 sentinels and
            // are out of scope — assert the specific expected names.
            AssertCreaturePrefabExists(FaunaPrefabsFolder, "cluck");
            AssertCreaturePrefabExists(FaunaPrefabsFolder, "graze");
            AssertCreaturePrefabExists(FaunaPrefabsFolder, "thornback");
            AssertCreaturePrefabExists(EnemyPrefabsFolder, "fungal_brood_mother");
            AssertCreaturePrefabExists(EnemyPrefabsFolder, "vord_drone");
            AssertCreaturePrefabExists(EnemyPrefabsFolder, "vord_raider");
            AssertCreaturePrefabExists(NpcPrefabsFolder, "wren");

            // Stages 4+5: Icons. At least 60 item icons + 7 creature icons in
            // a single Assets/Textures/Icons/ folder. Creature icons are named
            // creature_{id}.png; item icons are {item_id}.png. Total >= 67.
            int totalIcons = CountFiles(IconsFolder, "*.png");
            int creatureIcons = CountFiles(IconsFolder, "creature_*.png");
            int itemIcons = totalIcons - creatureIcons;
            Assert.GreaterOrEqual(itemIcons, 60,
                $"Expected >= 60 item icons under {IconsFolder}/, found {itemIcons}.");
            Assert.GreaterOrEqual(creatureIcons, 7,
                $"Expected >= 7 creature icons under {IconsFolder}/, found {creatureIcons}.");
        }

        [Test]
        public void Pipeline_Idempotent()
        {
            // First pass establishes a baseline.
            GenerateAllVisuals.RunPipeline();
            var before = SnapshotCounts();

            // Second pass should overwrite-in-place and not change counts.
            GenerateAllVisuals.RunPipeline();
            var after = SnapshotCounts();

            Assert.AreEqual(before.materials, after.materials,
                $"Material count changed across runs: {before.materials} -> {after.materials}.");
            Assert.AreEqual(before.primitives, after.primitives,
                $"Primitive mesh count changed: {before.primitives} -> {after.primitives}.");
            Assert.AreEqual(before.itemPrefabs, after.itemPrefabs,
                $"Item world prefab count changed: {before.itemPrefabs} -> {after.itemPrefabs}.");
            Assert.AreEqual(before.itemIcons, after.itemIcons,
                $"Item icon count changed: {before.itemIcons} -> {after.itemIcons}.");
            Assert.AreEqual(before.creatureIcons, after.creatureIcons,
                $"Creature icon count changed: {before.creatureIcons} -> {after.creatureIcons}.");
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private struct Counts
        {
            public int materials;
            public int primitives;
            public int itemPrefabs;
            public int itemIcons;
            public int creatureIcons;
        }

        private static Counts SnapshotCounts()
        {
            int totalIcons = CountFiles(IconsFolder, "*.png");
            int creatureIcons = CountFiles(IconsFolder, "creature_*.png");
            return new Counts
            {
                materials = CountFiles(MaterialsFolder, "*.mat"),
                primitives = CountFiles(PrimitivesFolder, "*.asset"),
                itemPrefabs = CountItemWorldPrefabs(ItemPrefabsFolder),
                itemIcons = totalIcons - creatureIcons,
                creatureIcons = creatureIcons,
            };
        }

        private static int CountFiles(string assetFolder, string searchPattern)
        {
            string abs = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(abs)) return 0;
            return Directory.GetFiles(abs, searchPattern, SearchOption.TopDirectoryOnly).Length;
        }

        private static int CountItemWorldPrefabs(string assetFolder)
        {
            string abs = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(abs)) return 0;
            return Directory.GetFiles(abs, "*.prefab", SearchOption.TopDirectoryOnly)
                .Count(p => !Path.GetFileNameWithoutExtension(p).EndsWith("_placed"));
        }

        private static void AssertCreaturePrefabExists(string folder, string id)
        {
            string path = $"{folder}/{id}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.NotNull(prefab,
                $"Creature prefab missing at {path} after pipeline run.");
        }
    }
}
#endif
