#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.6 — One-Click Generate All Visuals.
    ///
    /// Single menu entry that drives the full Volume 3 visual pipeline end-to-end
    /// in the correct order:
    ///   1. Materials                  (V3.1 — MaterialGenerator)
    ///   2. Primitive Meshes           (V3.2 — PrimitiveMeshFactory)
    ///   3. Item Prefabs               (V3.3 — BulkPrefabGenerator)
    ///   4. Creature Prefabs + Icons   (V3.5 — CreaturePrefabGenerator;
    ///                                  renders its own creature icons inline)
    ///   5. Item Icons                 (V3.4 — IconBulkRenderer)
    ///
    /// <para>
    /// Critical: this orchestrator does NOT wrap the call in an outer
    /// <see cref="AssetDatabase.StartAssetEditing"/> /
    /// <see cref="AssetDatabase.StopAssetEditing"/> batch. V3.4 and V3.5 both
    /// rely on an internal two-phase Sprite reload that runs AFTER their inner
    /// StopAssetEditing closes — wrapping them would defer the import finalisation
    /// past phase 2 and break the Sprite reload contract. Each stage manages
    /// its own batching.
    /// </para>
    ///
    /// <para>
    /// The "⟳" character in the menu name is a Unicode arrow (U+27F3),
    /// matching the V2.5 RegenerateAllMenu convention — not an emoji.
    /// </para>
    ///
    /// Idempotent: re-runs overwrite assets in place.
    /// </summary>
    public static class GenerateAllVisuals
    {
        [MenuItem("Voidborne/Generate/⟳ All Visuals")]
        private static void RunMenu()
        {
            const string title = "Generate All Visuals";
            const string message =
                "Run full Volume 3 visual pipeline? This regenerates materials, " +
                "primitive meshes, 60 item prefabs, 7 creature prefabs, and 67 icons.";
            if (!EditorUtility.DisplayDialog(title, message, "OK", "Cancel"))
            {
                return;
            }
            RunPipeline();
        }

        /// <summary>
        /// Test-callable entry point. Bypasses the confirmation dialog and runs
        /// every stage in dependency order. Always emits a final summary log,
        /// even if a stage throws (try/finally on the stage scoreboard).
        /// </summary>
        public static void RunPipeline()
        {
            Debug.Log("[GenerateAllVisuals] Running full Volume 3 pipeline...");

            int materialCount = 0;
            int meshCount = 0;
            int itemPrefabCount = 0;
            int itemPlacedCount = 0;
            int creaturePrefabCount = 0;
            int itemIconCount = 0;

            try
            {
                // -------------------------------------------------------------
                // Stage 1: Materials (V3.1).
                // -------------------------------------------------------------
                materialCount = MaterialGenerator.Run();
                Debug.Log(
                    $"[GenerateAllVisuals] Stage 1/5 Materials: {materialCount} materials.");

                // -------------------------------------------------------------
                // Stage 2: Primitive Meshes (V3.2). Count is fixed by the
                // PrimitiveShape enum size, but GenerateAll is idempotent so a
                // re-run only ensures every shape is on disk.
                // -------------------------------------------------------------
                PrimitiveMeshFactory.GenerateAll();
                meshCount = Enum.GetValues(typeof(PrimitiveShape)).Length;
                Debug.Log(
                    $"[GenerateAllVisuals] Stage 2/5 Primitives: {meshCount} meshes.");

                // -------------------------------------------------------------
                // Stage 3: Item Prefabs (V3.3). 60 world prefabs + 19 placed
                // variants for the Core 60 set.
                // -------------------------------------------------------------
                var (worldCount, placedCount) = BulkPrefabGenerator.Run();
                itemPrefabCount = worldCount;
                itemPlacedCount = placedCount;
                Debug.Log(
                    $"[GenerateAllVisuals] Stage 3/5 Item Prefabs: " +
                    $"{itemPrefabCount} world + {itemPlacedCount} placed variants.");

                // -------------------------------------------------------------
                // Stage 4: Creature Prefabs (V3.5). Renders 7 creature prefabs
                // AND their 7 PNG icons internally via its own two-phase
                // StartAssetEditing → StopAssetEditing → Sprite reload flow.
                // Do NOT wrap this call in an outer StartAssetEditing — per the
                // V3.5 implementer's heads-up — or the inner reload phase will
                // straddle two StopAssetEditing closes and the Sprites won't
                // load back.
                // -------------------------------------------------------------
                creaturePrefabCount = CreaturePrefabGenerator.Run();
                Debug.Log(
                    $"[GenerateAllVisuals] Stage 4/5 Creature Prefabs: " +
                    $"{creaturePrefabCount} creature prefabs (with inline icons).");

                // -------------------------------------------------------------
                // Stage 5: Item Icons (V3.4). Same two-phase pattern as Stage 4
                // for the same reason. Item prefabs from Stage 3 must already
                // exist on disk for IconBulkRenderer to read modelPrefab.
                // -------------------------------------------------------------
                itemIconCount = IconBulkRenderer.Run();
                Debug.Log(
                    $"[GenerateAllVisuals] Stage 5/5 Item Icons: {itemIconCount} icons.");
            }
            finally
            {
                // Always emit the summary so a stage-level throw still tells
                // the user how far the pipeline got.
                int totalIcons = itemIconCount + creaturePrefabCount;
                Debug.Log(
                    "[GenerateAllVisuals] Pipeline complete. Summary:\n" +
                    $"  Materials:        {materialCount}\n" +
                    $"  Primitive meshes: {meshCount}\n" +
                    $"  Item prefabs:     {itemPrefabCount} (+{itemPlacedCount} placed variants)\n" +
                    $"  Creature prefabs: {creaturePrefabCount}\n" +
                    $"  Item icons:       {itemIconCount}\n" +
                    $"  Creature icons:   {creaturePrefabCount}\n" +
                    $"  Total icons:      {totalIcons}");
            }
        }
    }
}
#endif
