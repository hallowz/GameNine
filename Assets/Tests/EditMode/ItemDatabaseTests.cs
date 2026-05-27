#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 2.2 — verifies the generated ItemDatabase loads
    /// via Resources and contains the expected items (kind, count, build flag).
    ///
    /// These tests assume <c>Voidborne/Generate/Items</c> has been run at least
    /// once so the Resources/ItemDatabase.asset exists.
    /// </summary>
    public class ItemDatabaseTests
    {
        private const int MinimumExpectedItemCount = 1000;

        [Test]
        public void ItemDatabase_LoadsViaResources()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "Resources.Load<ItemDatabase>(\"ItemDatabase\") returned null. Has Voidborne/Generate/Items been run?");
            Assert.IsNotNull(db.items, "ItemDatabase.items list is null.");
        }

        [Test]
        public void Workbench_IsMachine()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "ItemDatabase not present.");

            var workbench = db.GetItem("workbench");
            Assert.IsNotNull(workbench, "ItemDatabase.GetItem(\"workbench\") returned null.");
            Assert.AreEqual(ItemKind.Machine, workbench.kind,
                $"Expected workbench.kind == Machine, got {workbench.kind}.");
            Assert.AreEqual("workbench", workbench.itemId,
                "workbench.itemId mismatch.");
        }

        [Test]
        public void AllItems_CountAtLeast1000()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "ItemDatabase not present.");

            Assert.GreaterOrEqual(db.items.Count, MinimumExpectedItemCount,
                $"Expected at least {MinimumExpectedItemCount} items in ItemDatabase, found {db.items.Count}.");
        }

        [Test]
        public void BuildBlockFlag_Set_ForStoneCube()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "ItemDatabase not present.");

            // stone_cube is flagged "_build": true in items.json at line 30178.
            var stoneCube = db.GetItem("stone_cube");
            Assert.IsNotNull(stoneCube, "ItemDatabase.GetItem(\"stone_cube\") returned null.");
            Assert.IsTrue(stoneCube.isBuildBlock,
                $"Expected stone_cube.isBuildBlock == true, got false.");
            Assert.AreEqual(ItemKind.Product, stoneCube.kind,
                $"Expected stone_cube.kind == Product, got {stoneCube.kind}.");
        }

        [Test]
        public void Source_FaunaItem_HasFaunaSource()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "ItemDatabase not present.");

            // 'graze' is a kind=source, src=fauna entry at the top of items.json.
            var graze = db.GetItem("graze");
            Assert.IsNotNull(graze, "ItemDatabase.GetItem(\"graze\") returned null.");
            Assert.AreEqual(ItemKind.Source, graze.kind);
            Assert.AreEqual(ItemSource.Fauna, graze.source);
            Assert.AreEqual(16, graze.maxStackSize, "Source items should default to maxStackSize 16.");
        }

        [Test]
        public void Machine_StackSize_IsOne()
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(db, "ItemDatabase not present.");

            var workbench = db.GetItem("workbench");
            Assert.IsNotNull(workbench);
            Assert.AreEqual(1, workbench.maxStackSize, "Machines should have maxStackSize 1.");
        }
    }
}
#endif
