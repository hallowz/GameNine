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
        // V2.9 — loader switched to Core 60 / Core 7 NPC roster. Counts use floor
        // assertions so iteration can grow them; JsonLoaderTests owns the upper bound.
        private const int ExpectedItemCount = 60;
        private const int MinExpectedRecipeCount = 30;
        private const int ExpectedMachineCount = 12;  // 12 machines in items_core.json
        private const int ExpectedFaunaCount = 3;     // Graze + Cluck + Thornback
        private const int ExpectedEnemyCount = 3;     // Brood Mother (boss) + Vord Drone + Vord Raider (fodder)
        private const int ExpectedNpcCount = 1;       // Wren only

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
        public void MachineCount_Core60()
        {
            Assert.GreaterOrEqual(Registries.Machines.AllMachines.Count, ExpectedMachineCount,
                $"Expected at least {ExpectedMachineCount} machines (Core 60 roster).");
        }

        [Test]
        public void FaunaCount_Core7()
        {
            Assert.GreaterOrEqual(Registries.Fauna.AllFauna.Count, ExpectedFaunaCount,
                $"Expected at least {ExpectedFaunaCount} fauna (Graze + Cluck + Thornback).");
        }

        [Test]
        public void EnemyCount_Core7()
        {
            Assert.GreaterOrEqual(Registries.Enemies.AllEnemies.Count, ExpectedEnemyCount,
                $"Expected at least {ExpectedEnemyCount} enemies (Brood Mother + Vord Drone + Vord Raider).");
        }

        [Test]
        public void NpcCount_Core7()
        {
            Assert.GreaterOrEqual(Registries.Npcs.AllNpcs.Count, ExpectedNpcCount,
                $"Expected at least {ExpectedNpcCount} NPC (Wren).");
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
        public void Machines_Furnace_IsTier2()
        {
            // V2.9 — items_core.json reauthors the furnace as 'T2 — contained smelting'.
            MachineDefinition furnace = Registries.Machines.GetById("furnace");
            Assert.IsNotNull(furnace, "Registries.Machines.GetById(\"furnace\") returned null.");
            Assert.AreEqual(2, furnace.tier, "Furnace role 'T2 — contained smelting' should parse to tier 2.");
            Assert.AreEqual(MachineCategory.Furnace, furnace.category);
        }

        [Test]
        public void Machines_SteamBoiler_IsForgivingThermalBoil()
        {
            // The M2 synergy-sandbox anchor.
            MachineDefinition boiler = Registries.Machines.GetById("steam_boiler");
            Assert.IsNotNull(boiler, "Registries.Machines.GetById(\"steam_boiler\") returned null.");
            Assert.AreEqual(MachineProcessType.Forgiving_Thermal_Boil, boiler.processType);
            Assert.IsFalse(string.IsNullOrEmpty(boiler.howItWorks),
                "steam_boiler.howItWorks must be populated for the Machine UI tooltip.");
        }

        // ---- Fauna --------------------------------------------------------

        [Test]
        public void Fauna_Graze_Loads()
        {
            FaunaDefinition graze = Registries.Fauna.GetById("graze");
            Assert.IsNotNull(graze, "Registries.Fauna.GetById(\"graze\") returned null.");
            Assert.IsTrue(graze.isPassive, "Graze should be flagged passive (behaviour: 'Flees if attacked').");
        }

        // ---- NPCs ---------------------------------------------------------

        [Test]
        public void Npcs_AllNonKillable()
        {
            foreach (var n in Registries.Npcs.AllNpcs)
            {
                Assert.IsFalse(n.isKillable,
                    $"NPC '{n.id}' is flagged killable but named Kin must be non-killable.");
            }
        }
    }
}
#endif
