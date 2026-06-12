#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.Editor.ArtPipeline;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Volume 3.4 — Icon Renderer / Bulk Renderer tests.
    ///
    /// Verifies the renderer produces a PNG + Sprite at the expected path with
    /// the requested resolution and a transparent background, and that the
    /// bulk pass writes Sprite references back onto the ItemDatabase entries.
    /// </summary>
    public class IconRendererTests
    {
        // ----- Renderer (single-prefab) -----

        [Test]
        public void Renderer_ProducesNonNullSprite()
        {
            var workbench = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Items/workbench.prefab");
            Assert.NotNull(workbench,
                "workbench prefab missing — run Voidborne/Generate/Item Prefabs first.");

            string assetPath = "Assets/Textures/Icons/workbench.png";

            Sprite sprite = IconRenderer.RenderIcon(workbench, "workbench");

            Assert.NotNull(sprite, "RenderIcon returned null for workbench.");
            Assert.IsTrue(File.Exists(Path.GetFullPath(assetPath)),
                $"Expected PNG on disk at {assetPath}.");
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.NotNull(loaded, $"Sprite did not load from {assetPath}.");
        }

        [Test]
        public void Renderer_IconResolutionMatchesRequest()
        {
            var workbench = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Items/workbench.prefab");
            Assert.NotNull(workbench,
                "workbench prefab missing — run Voidborne/Generate/Item Prefabs first.");

            // Use a temp id so we don't clobber the canonical workbench icon.
            const string id = "__test_resolution_128";

            try
            {
                Sprite sprite = IconRenderer.RenderIcon(workbench, id, resolution: 128);
                Assert.NotNull(sprite, "RenderIcon returned null.");
                Assert.NotNull(sprite.texture, "Sprite.texture was null.");
                Assert.AreEqual(128, sprite.texture.width,
                    $"Expected 128px texture, got {sprite.texture.width}px.");
                Assert.AreEqual(128, sprite.texture.height,
                    $"Expected 128px texture, got {sprite.texture.height}px.");
            }
            finally
            {
                DeleteIconAsset(id);
            }
        }

        [Test]
        public void Renderer_TransparentBackground()
        {
            // Build a minimal sphere prefab in-memory so we don't depend on a
            // specific Core 60 item: a single MeshRenderer on a primitive
            // sphere occupies the centre of the icon. Corners must be alpha=0.
            GameObject sphereSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereSource.hideFlags = HideFlags.HideAndDontSave;

            const string id = "__test_transparent_bg";

            try
            {
                Sprite sprite = IconRenderer.RenderIcon(sphereSource, id, resolution: 64);
                Assert.NotNull(sprite, "RenderIcon returned null for sphere primitive.");

                // Read the actual on-disk PNG via a fresh Texture2D so we get
                // RAW pixel data (the imported Sprite may compress on some
                // platforms).
                string assetPath = $"Assets/Textures/Icons/{id}.png";
                byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.IsTrue(raw.LoadImage(bytes), "Failed to decode PNG bytes.");

                // Sphere fills the centre at 80% of frame — the four corner
                // pixels are guaranteed background.
                int w = raw.width;
                int h = raw.height;
                Color c00 = raw.GetPixel(0, 0);
                Color cTL = raw.GetPixel(0, h - 1);
                Color cTR = raw.GetPixel(w - 1, h - 1);
                Color cBR = raw.GetPixel(w - 1, 0);
                Object.DestroyImmediate(raw);

                Assert.AreEqual(0f, c00.a, 0.01f, "Bottom-left corner is not transparent.");
                Assert.AreEqual(0f, cTL.a, 0.01f, "Top-left corner is not transparent.");
                Assert.AreEqual(0f, cTR.a, 0.01f, "Top-right corner is not transparent.");
                Assert.AreEqual(0f, cBR.a, 0.01f, "Bottom-right corner is not transparent.");
            }
            finally
            {
                if (sphereSource != null) Object.DestroyImmediate(sphereSource);
                DeleteIconAsset(id);
            }
        }

        // ----- Bulk renderer -----

        [Test]
        public void BulkRenderer_AssignsToItemDatabase()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.NotNull(db, "ItemDatabase missing — run Voidborne/Generate/Items first.");
            Assert.GreaterOrEqual(db.AllItems.Count, 50,
                "Expected Core 60 ItemDatabase to carry ~60 items.");

            int rendered = IconBulkRenderer.Run();
            Assert.Greater(rendered, 0, "Bulk renderer reported zero rendered icons.");

            // Re-fetch via the database so we exercise the lookup path.
            string[] sample = { "workbench", "furnace", "milk", "iron_ore" };
            int sampleWithIcon = 0;
            foreach (var id in sample)
            {
                var item = db.GetItem(id);
                if (item == null) continue;
                if (item.icon != null) sampleWithIcon++;
            }
            Assert.GreaterOrEqual(sampleWithIcon, 3,
                "Expected at least 3 of {workbench, furnace, milk, iron_ore} to have icons after bulk run.");
        }

        // ----- Helpers -----

        private static void DeleteIconAsset(string id)
        {
            string assetPath = $"Assets/Textures/Icons/{id}.png";
            if (File.Exists(Path.GetFullPath(assetPath)))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
#endif
