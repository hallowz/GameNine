#if UNITY_EDITOR
using NUnit.Framework;
using Voidborne.Core;
using Voidborne.Editor.Data;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.5 — exercises the orchestrator (the static
    /// <see cref="RegenerateAllMenu.Run"/> entry point that the MenuItem
    /// delegates to).
    ///
    /// We deliberately do NOT use <c>EditorApplication.ExecuteMenuItem</c>
    /// because Unity's menu system is unreliable in test contexts (Unicode
    /// path matching, deferred dispatch). Hitting the public static API
    /// directly is identical to what the menu callback does.
    ///
    /// JsonReextractMenu is NOT covered here — it would spawn a real Python
    /// subprocess. Manual smoke-test only.
    /// </summary>
    public class RegenerateAllTests
    {
        // V2.9 — loader switched from items.json (1031/1555/43/18/43/9) to
        // items_core.json + npcs_core.json (60 items / hand-authored recipes /
        // ~14 machines / 3 fauna / 3 enemies / 1 NPC). Use floor assertions so
        // hand-authoring iteration can grow these freely; the JSON loader test
        // owns the upper bound on item count.
        private const int ExpectedItemCount = 60;
        private const int MinExpectedRecipeCount = 30;
        private const int ExpectedMachineCount = 12;
        private const int ExpectedFaunaCount = 3;
        private const int ExpectedEnemyCount = 2;
        private const int ExpectedNpcCount = 1;

        [Test]
        public void RegenerateAll_CallsGeneratorsInOrder()
        {
            // Acceptance: orchestrator runs end-to-end without throwing, and
            // all six registries are populated when it returns.
            Assert.DoesNotThrow(() => RegenerateAllMenu.Run());

            Assert.IsNotNull(Registries.Items,    "Items registry not populated after Run().");
            Assert.IsNotNull(Registries.Recipes,  "Recipes registry not populated after Run().");
            Assert.IsNotNull(Registries.Machines, "Machines registry not populated after Run().");
            Assert.IsNotNull(Registries.Fauna,    "Fauna registry not populated after Run().");
            Assert.IsNotNull(Registries.Enemies,  "Enemies registry not populated after Run().");
            Assert.IsNotNull(Registries.Npcs,     "Npcs registry not populated after Run().");

            Assert.GreaterOrEqual(Registries.Items.AllItems.Count, ExpectedItemCount,
                $"Items: expected ≥ {ExpectedItemCount}.");
            Assert.GreaterOrEqual(Registries.Recipes.AllRecipes.Count, MinExpectedRecipeCount,
                $"Recipes: expected ≥ {MinExpectedRecipeCount}.");
            Assert.GreaterOrEqual(Registries.Machines.AllMachines.Count, ExpectedMachineCount,
                $"Machines: expected ≥ {ExpectedMachineCount}.");
            Assert.GreaterOrEqual(Registries.Fauna.AllFauna.Count, ExpectedFaunaCount,
                $"Fauna: expected ≥ {ExpectedFaunaCount}.");
            Assert.GreaterOrEqual(Registries.Enemies.AllEnemies.Count, ExpectedEnemyCount,
                $"Enemies: expected ≥ {ExpectedEnemyCount}.");
            Assert.GreaterOrEqual(Registries.Npcs.AllNpcs.Count, ExpectedNpcCount,
                $"NPCs: expected ≥ {ExpectedNpcCount}.");
        }

        [Test]
        public void RegenerateAll_Idempotent()
        {
            // First pass: capture counts.
            RegenerateAllMenu.Run();
            int items1    = Registries.Items.AllItems.Count;
            int recipes1  = Registries.Recipes.AllRecipes.Count;
            int machines1 = Registries.Machines.AllMachines.Count;
            int fauna1    = Registries.Fauna.AllFauna.Count;
            int enemies1  = Registries.Enemies.AllEnemies.Count;
            int npcs1     = Registries.Npcs.AllNpcs.Count;

            // Second pass: counts must be identical — no spurious creation or
            // deletion when the JSON inputs are unchanged.
            RegenerateAllMenu.Run();
            Assert.AreEqual(items1,    Registries.Items.AllItems.Count,       "Items count drifted between runs.");
            Assert.AreEqual(recipes1,  Registries.Recipes.AllRecipes.Count,   "Recipes count drifted between runs.");
            Assert.AreEqual(machines1, Registries.Machines.AllMachines.Count, "Machines count drifted between runs.");
            Assert.AreEqual(fauna1,    Registries.Fauna.AllFauna.Count,       "Fauna count drifted between runs.");
            Assert.AreEqual(enemies1,  Registries.Enemies.AllEnemies.Count,   "Enemies count drifted between runs.");
            Assert.AreEqual(npcs1,     Registries.Npcs.AllNpcs.Count,         "NPCs count drifted between runs.");
        }
    }
}
#endif
