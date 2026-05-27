#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Core;
using Voidborne.Enemies.V2;
using Voidborne.Fauna;
using Voidborne.NPCs;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.4 — verifies all six generated registries
    /// load via <see cref="Registries"/>, expose the expected entry counts,
    /// and resolve a handful of canonical IDs.
    ///
    /// Assumes <c>Voidborne/Generate/{Items,Recipes,Machines,Fauna,Enemies,NPCs}</c>
    /// have all been run at least once.
    /// </summary>
    public class RegistriesTests
    {
        // Expected counts from the current JSON extraction (2026-05-26).
        private const int ExpectedItemCount = 1031;
        private const int MinExpectedRecipeCount = 1500;
        private const int ExpectedMachineCount = 43;
        private const int ExpectedFaunaCount = 18;
        private const int ExpectedEnemyCount = 43;  // 30 boss + 2 finale + 11 fodder
        private const int ExpectedNpcCount = 9;     // 6 named + 3 trader

        // ---- All registries resolve via the facade ------------------------

        [Test]
        public void Registries_AllSixLoad()
        {
            Assert.IsNotNull(Registries.Items,    "Registries.Items is null — run Voidborne/Generate/Items.");
            Assert.IsNotNull(Registries.Recipes,  "Registries.Recipes is null — run Voidborne/Generate/Recipes.");
            Assert.IsNotNull(Registries.Machines, "Registries.Machines is null — run Voidborne/Generate/Machines.");
            Assert.IsNotNull(Registries.Fauna,    "Registries.Fauna is null — run Voidborne/Generate/Fauna.");
            Assert.IsNotNull(Registries.Enemies,  "Registries.Enemies is null — run Voidborne/Generate/Enemies.");
            Assert.IsNotNull(Registries.Npcs,     "Registries.Npcs is null — run Voidborne/Generate/NPCs.");
        }

        // ---- Counts -------------------------------------------------------

        [Test]
        public void ItemCount_Matches()
        {
            Assert.GreaterOrEqual(Registries.Items.AllItems.Count, ExpectedItemCount,
                $"Expected at least {ExpectedItemCount} items.");
        }

        [Test]
        public void RecipeCount_MeetsFloor()
        {
            Assert.GreaterOrEqual(Registries.Recipes.AllRecipes.Count, MinExpectedRecipeCount,
                $"Expected at least {MinExpectedRecipeCount} recipes.");
        }

        [Test]
        public void MachineCount_Is43()
        {
            Assert.AreEqual(ExpectedMachineCount, Registries.Machines.AllMachines.Count,
                $"Expected exactly {ExpectedMachineCount} machines.");
        }

        [Test]
        public void FaunaCount_Is18()
        {
            Assert.AreEqual(ExpectedFaunaCount, Registries.Fauna.AllFauna.Count,
                $"Expected exactly {ExpectedFaunaCount} fauna.");
        }

        [Test]
        public void EnemyCount_Is43()
        {
            Assert.AreEqual(ExpectedEnemyCount, Registries.Enemies.AllEnemies.Count,
                $"Expected exactly {ExpectedEnemyCount} enemies (30 boss + 2 finale + 11 fodder).");
        }

        [Test]
        public void NpcCount_Is9()
        {
            Assert.AreEqual(ExpectedNpcCount, Registries.Npcs.AllNpcs.Count,
                $"Expected exactly {ExpectedNpcCount} NPCs (6 named + 3 traders).");
        }

        // ---- Machines: workbench lookup + tier ----------------------------

        [Test]
        public void Machines_Workbench_IsTier1()
        {
            MachineDefinition workbench = Registries.Machines.GetById("workbench");
            Assert.IsNotNull(workbench, "Registries.Machines.GetById(\"workbench\") returned null.");
            Assert.AreEqual(1, workbench.tier,
                $"Workbench role 'T1 — 3x3 crafting (BOOTSTRAP)' should parse to tier 1; got {workbench.tier}.");
            Assert.AreEqual(3, workbench.gridWidth);
            Assert.AreEqual(3, workbench.gridHeight);
            Assert.IsFalse(workbench.needsPower, "Workbench (T1) should not need power.");
            Assert.AreEqual(MachineCategory.Workbench, workbench.category);
        }

        [Test]
        public void Machines_Furnace_IsTier3()
        {
            MachineDefinition furnace = Registries.Machines.GetById("furnace");
            Assert.IsNotNull(furnace, "Registries.Machines.GetById(\"furnace\") returned null.");
            Assert.AreEqual(3, furnace.tier, "Furnace role 'T3 — proper smelting' should parse to tier 3.");
            Assert.AreEqual(MachineCategory.Furnace, furnace.category);
        }

        [Test]
        public void Machines_Assembler_IsT7_AndPowered()
        {
            MachineDefinition assembler = Registries.Machines.GetById("assembler");
            Assert.IsNotNull(assembler);
            Assert.AreEqual(7, assembler.tier);
            Assert.IsTrue(assembler.needsPower);
            Assert.AreEqual(MachineCategory.Assembler, assembler.category);
            Assert.AreEqual(5, assembler.gridWidth, "Assembler should be 5x5 per the spec heuristic.");
            Assert.AreEqual(5, assembler.gridHeight);
        }

        // ---- Enemies ------------------------------------------------------

        [Test]
        public void Enemies_BossCount_Is32()
        {
            // Spec: 30 bosses + 2 finale = 32 entries flagged as boss.
            int bosses = Registries.Enemies.Bosses.Count();
            Assert.AreEqual(32, bosses,
                $"Expected 32 entries flagged isBoss (30 boss + 2 finale); got {bosses}.");
        }

        [Test]
        public void Enemies_FodderCount_Is11()
        {
            int fodder = Registries.Enemies.Fodder.Count();
            Assert.AreEqual(11, fodder, $"Expected 11 fodder enemies; got {fodder}.");
        }

        [Test]
        public void Enemies_VordBroodQueen_IsBoss_BroodFamily_Tier3()
        {
            EnemyDefinition queen = Registries.Enemies.GetById("vord_brood_queen");
            Assert.IsNotNull(queen, "Registries.Enemies.GetById(\"vord_brood_queen\") returned null.");
            Assert.IsTrue(queen.isBoss);
            Assert.IsFalse(queen.isFinale);
            Assert.AreEqual(EnemyArchetype.Brood, queen.family);
            Assert.AreEqual(3, queen.tier,
                "Vord Brood Queen desc says 'Tier 3' — generator should parse that.");
        }

        [Test]
        public void Enemies_TheHollowSource_IsFinale()
        {
            EnemyDefinition finale = Registries.Enemies.GetById("the_hollow_source");
            Assert.IsNotNull(finale, "Registries.Enemies.GetById(\"the_hollow_source\") returned null.");
            Assert.IsTrue(finale.isBoss, "Finale entries should also be flagged isBoss.");
            Assert.IsTrue(finale.isFinale);
            Assert.AreEqual(EnemyArchetype.Finale, finale.family);
        }

        // ---- Fauna --------------------------------------------------------

        [Test]
        public void Fauna_HasAtLeastOneTameable()
        {
            int tameable = Registries.Fauna.Tameable.Count();
            Assert.Greater(tameable, 0,
                "Expected at least one tameable fauna entry — Ash Pup / Cavestalker / Glimmerwing / Driftwing / Thornback / Graze should all qualify.");
        }

        [Test]
        public void Fauna_Graze_Loads()
        {
            FaunaDefinition graze = Registries.Fauna.GetById("graze");
            Assert.IsNotNull(graze, "Registries.Fauna.GetById(\"graze\") returned null.");
            Assert.IsTrue(graze.isPassive, "Graze should be flagged passive (behaviour: 'Flees if attacked').");
        }

        // ---- NPCs ---------------------------------------------------------

        [Test]
        public void Npcs_QuestGivers_Are6_NamedKin()
        {
            int questGivers = Registries.Npcs.QuestGivers.Count();
            Assert.AreEqual(6, questGivers,
                $"Expected 6 quest givers (the 6 named Kin); got {questGivers}.");
        }

        [Test]
        public void Npcs_Traders_Are3()
        {
            int traders = Registries.Npcs.Traders.Count();
            Assert.AreEqual(3, traders,
                $"Expected 3 traders (Travelling Merchants); got {traders}.");
        }

        [Test]
        public void Npcs_AllNonKillable()
        {
            foreach (var n in Registries.Npcs.AllNpcs)
            {
                Assert.IsFalse(n.isKillable,
                    $"NPC '{n.id}' is flagged killable but named Kin + traders must be non-killable.");
            }
        }
    }
}
#endif
