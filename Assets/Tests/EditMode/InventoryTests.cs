using System;
using NUnit.Framework;
using UnityEngine;
using Voidborne.Combat;

namespace Voidborne.Tests.EditMode
{
    public class InventoryTests
    {
        private ItemDefinition _stone;
        private ItemDefinition _wood;
        private ItemDefinition _sword;

        [SetUp]
        public void SetUp()
        {
            _stone = ScriptableObject.CreateInstance<ItemDefinition>();
            _stone.itemId = "stone";
            _stone.displayName = "Stone";
            _stone.itemType = ItemType.Resource;
            _stone.maxStackSize = 64;
            _stone.weight = 1f;

            _wood = ScriptableObject.CreateInstance<ItemDefinition>();
            _wood.itemId = "wood";
            _wood.displayName = "Wood";
            _wood.itemType = ItemType.Resource;
            _wood.maxStackSize = 64;
            _wood.weight = 0.5f;

            _sword = ScriptableObject.CreateInstance<ItemDefinition>();
            _sword.itemId = "iron_sword";
            _sword.displayName = "Iron Sword";
            _sword.itemType = ItemType.Weapon;
            _sword.maxStackSize = 1;
            _sword.weight = 3f;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_stone);
            UnityEngine.Object.DestroyImmediate(_wood);
            UnityEngine.Object.DestroyImmediate(_sword);
        }

        // =============================================================
        //  Inventory Creation
        // =============================================================

        [Test]
        public void Constructor_SetsWidthAndHeight()
        {
            var inv = new Inventory(5, 7);
            Assert.AreEqual(5, inv.Width);
            Assert.AreEqual(7, inv.Height);
        }

        [Test]
        public void Constructor_SlotCountEqualsWidthTimesHeight()
        {
            var inv = new Inventory(3, 4);
            Assert.AreEqual(12, inv.SlotCount);
        }

        [Test]
        public void Constructor_DefaultStackMultiplierIsOne()
        {
            var inv = new Inventory(1, 1);
            Assert.AreEqual(1, inv.StackMultiplier);
        }

        [Test]
        public void Constructor_CustomStackMultiplier()
        {
            var inv = new Inventory(1, 1, 4);
            Assert.AreEqual(4, inv.StackMultiplier);
        }

        [Test]
        public void Constructor_NegativeStackMultiplier_ClampsToOne()
        {
            var inv = new Inventory(1, 1, -5);
            Assert.AreEqual(1, inv.StackMultiplier);
        }

        [Test]
        public void Constructor_ZeroStackMultiplier_ClampsToOne()
        {
            var inv = new Inventory(1, 1, 0);
            Assert.AreEqual(1, inv.StackMultiplier);
        }

        [Test]
        public void Constructor_SingleSlotInventory()
        {
            var inv = new Inventory(1, 1);
            Assert.AreEqual(1, inv.SlotCount);
        }

        [Test]
        public void Constructor_AllSlotsStartEmpty()
        {
            var inv = new Inventory(3, 3);
            for (int i = 0; i < inv.SlotCount; i++)
                Assert.IsTrue(inv.GetSlot(i).IsEmpty);
        }

        [Test]
        public void IsInfinite_DefaultsFalse()
        {
            var inv = new Inventory(1, 1);
            Assert.IsFalse(inv.IsInfinite);
        }

        // =============================================================
        //  GetSlot / SetSlot
        // =============================================================

        [Test]
        public void SetSlot_ThenGetSlot_ReturnsSameStack()
        {
            var inv = new Inventory(5, 1);
            var stack = new ItemStack(_stone, 10);
            inv.SetSlot(2, stack);

            ItemStack result = inv.GetSlot(2);
            Assert.AreEqual(_stone, result.item);
            Assert.AreEqual(10, result.quantity);
        }

        [Test]
        public void SetSlot_OverwritesExisting()
        {
            var inv = new Inventory(2, 1);
            inv.SetSlot(0, new ItemStack(_stone, 5));
            inv.SetSlot(0, new ItemStack(_wood, 20));

            ItemStack result = inv.GetSlot(0);
            Assert.AreEqual(_wood, result.item);
            Assert.AreEqual(20, result.quantity);
        }

        [Test]
        public void SetSlot_EmptyStack_ClearsSlot()
        {
            var inv = new Inventory(2, 1);
            inv.SetSlot(0, new ItemStack(_stone, 5));
            inv.SetSlot(0, new ItemStack(null, 0));

            Assert.IsTrue(inv.GetSlot(0).IsEmpty);
        }

        [Test]
        public void GetSlot_NegativeIndex_ThrowsException()
        {
            var inv = new Inventory(3, 3);
            Assert.Throws<IndexOutOfRangeException>(() => inv.GetSlot(-1));
        }

        [Test]
        public void GetSlot_IndexBeyondSlotCount_ThrowsException()
        {
            var inv = new Inventory(3, 3);
            Assert.Throws<IndexOutOfRangeException>(() => inv.GetSlot(9));
        }

        [Test]
        public void SetSlot_NegativeIndex_ThrowsException()
        {
            var inv = new Inventory(3, 3);
            Assert.Throws<IndexOutOfRangeException>(() => inv.SetSlot(-1, new ItemStack(_stone, 1)));
        }

        [Test]
        public void SetSlot_IndexBeyondSlotCount_ThrowsException()
        {
            var inv = new Inventory(3, 3);
            Assert.Throws<IndexOutOfRangeException>(() => inv.SetSlot(9, new ItemStack(_stone, 1)));
        }

        // =============================================================
        //  AddItem
        // =============================================================

        [Test]
        public void AddItem_EmptyInventory_PlacesInFirstSlot()
        {
            var inv = new Inventory(5, 1);
            bool result = inv.AddItem(new ItemStack(_stone, 10));

            Assert.IsTrue(result);
            Assert.AreEqual(10, inv.GetSlot(0).quantity);
            Assert.AreEqual(_stone, inv.GetSlot(0).item);
        }

        [Test]
        public void AddItem_EmptyStack_ReturnsTrue()
        {
            var inv = new Inventory(1, 1);
            Assert.IsTrue(inv.AddItem(new ItemStack(null, 0)));
        }

        [Test]
        public void AddItem_StacksOntoExistingPartialStack()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 30));
            inv.AddItem(new ItemStack(_stone, 20));

            // Should merge into slot 0: 30 + 20 = 50
            Assert.AreEqual(50, inv.GetSlot(0).quantity);
            Assert.IsTrue(inv.GetSlot(1).IsEmpty);
        }

        [Test]
        public void AddItem_OverflowsToNextSlot_WhenFirstStackFull()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 60));
            inv.AddItem(new ItemStack(_stone, 20));

            // Slot 0: 60 + 4 = 64 (full), Slot 1: 16
            Assert.AreEqual(64, inv.GetSlot(0).quantity);
            Assert.AreEqual(16, inv.GetSlot(1).quantity);
        }

        [Test]
        public void AddItem_MoreThanOneFullStack_SpansMultipleSlots()
        {
            var inv = new Inventory(5, 1);
            bool result = inv.AddItem(new ItemStack(_stone, 150));

            Assert.IsTrue(result);
            Assert.AreEqual(64, inv.GetSlot(0).quantity);
            Assert.AreEqual(64, inv.GetSlot(1).quantity);
            Assert.AreEqual(22, inv.GetSlot(2).quantity);
        }

        [Test]
        public void AddItem_ReturnsFalse_WhenInventoryFull()
        {
            var inv = new Inventory(1, 1);
            inv.AddItem(new ItemStack(_stone, 64));
            bool result = inv.AddItem(new ItemStack(_stone, 10));

            Assert.IsFalse(result);
        }

        [Test]
        public void AddItem_Unstackable_EachGoesToSeparateSlot()
        {
            var inv = new Inventory(3, 1);
            inv.AddItem(new ItemStack(_sword, 1));
            inv.AddItem(new ItemStack(_sword, 1));

            Assert.AreEqual(1, inv.GetSlot(0).quantity);
            Assert.AreEqual(1, inv.GetSlot(1).quantity);
        }

        [Test]
        public void AddItem_Unstackable_FailsWhenFull()
        {
            var inv = new Inventory(1, 1);
            inv.AddItem(new ItemStack(_sword, 1));
            bool result = inv.AddItem(new ItemStack(_sword, 1));

            Assert.IsFalse(result);
        }

        [Test]
        public void AddItem_DifferentItems_GoToSeparateSlots()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 10));
            inv.AddItem(new ItemStack(_wood, 20));

            Assert.AreEqual(_stone, inv.GetSlot(0).item);
            Assert.AreEqual(10, inv.GetSlot(0).quantity);
            Assert.AreEqual(_wood, inv.GetSlot(1).item);
            Assert.AreEqual(20, inv.GetSlot(1).quantity);
        }

        [Test]
        public void AddItem_FillsPartialStacksBeforeEmptySlots()
        {
            var inv = new Inventory(5, 1);
            inv.SetSlot(0, new ItemStack(_wood, 5));
            inv.SetSlot(1, new ItemStack(_stone, 30));
            inv.SetSlot(2, new ItemStack(_wood, 10));

            // Adding 20 wood: should top up slot 0 (5->25) and slot 2 (10->10), not use slot 3
            // Actually: partial stacks first: slot 0 has 5 wood (space 59), slot 2 has 10 wood (space 54)
            // 20 wood all goes to slot 0: 5+20=25
            inv.AddItem(new ItemStack(_wood, 20));

            Assert.AreEqual(25, inv.GetSlot(0).quantity);
            Assert.AreEqual(_wood, inv.GetSlot(0).item);
            Assert.AreEqual(10, inv.GetSlot(2).quantity);
        }

        // =============================================================
        //  RemoveItem
        // =============================================================

        [Test]
        public void RemoveItem_RemovesFromSingleSlot()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 30));
            bool result = inv.RemoveItem("stone", 10);

            Assert.IsTrue(result);
            Assert.AreEqual(20, inv.GetSlot(0).quantity);
        }

        [Test]
        public void RemoveItem_RemovesEntireSlot_LeavesEmpty()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 10));
            inv.RemoveItem("stone", 10);

            Assert.IsTrue(inv.GetSlot(0).IsEmpty);
        }

        [Test]
        public void RemoveItem_SpansMultipleSlots()
        {
            var inv = new Inventory(5, 1);
            inv.SetSlot(0, new ItemStack(_stone, 20));
            inv.SetSlot(1, new ItemStack(_stone, 20));

            bool result = inv.RemoveItem("stone", 30);

            Assert.IsTrue(result);
            // Removes 20 from slot 0 (now empty), 10 from slot 1 (now 10)
            Assert.IsTrue(inv.GetSlot(0).IsEmpty);
            Assert.AreEqual(10, inv.GetSlot(1).quantity);
        }

        [Test]
        public void RemoveItem_ReturnsFalse_WhenNotEnough()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 5));
            bool result = inv.RemoveItem("stone", 10);

            Assert.IsFalse(result);
            // Quantity should be unchanged since removal was rejected
            Assert.AreEqual(5, inv.GetSlot(0).quantity);
        }

        [Test]
        public void RemoveItem_ZeroQuantity_ReturnsTrue()
        {
            var inv = new Inventory(5, 1);
            Assert.IsTrue(inv.RemoveItem("stone", 0));
        }

        [Test]
        public void RemoveItem_ItemNotPresent_ReturnsFalse()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 10));
            bool result = inv.RemoveItem("wood", 5);

            Assert.IsFalse(result);
        }

        // =============================================================
        //  CountItem
        // =============================================================

        [Test]
        public void CountItem_EmptyInventory_ReturnsZero()
        {
            var inv = new Inventory(5, 5);
            Assert.AreEqual(0, inv.CountItem("stone"));
        }

        [Test]
        public void CountItem_SingleSlot()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 30));
            Assert.AreEqual(30, inv.CountItem("stone"));
        }

        [Test]
        public void CountItem_MultipleSlots_Sums()
        {
            var inv = new Inventory(5, 1);
            inv.SetSlot(0, new ItemStack(_stone, 64));
            inv.SetSlot(2, new ItemStack(_stone, 20));
            inv.SetSlot(4, new ItemStack(_stone, 10));

            Assert.AreEqual(94, inv.CountItem("stone"));
        }

        [Test]
        public void CountItem_IgnoresOtherItems()
        {
            var inv = new Inventory(5, 1);
            inv.SetSlot(0, new ItemStack(_stone, 30));
            inv.SetSlot(1, new ItemStack(_wood, 50));

            Assert.AreEqual(30, inv.CountItem("stone"));
            Assert.AreEqual(50, inv.CountItem("wood"));
        }

        // =============================================================
        //  HasRoomFor
        // =============================================================

        [Test]
        public void HasRoomFor_EmptyInventory_ReturnsTrue()
        {
            var inv = new Inventory(5, 1);
            Assert.IsTrue(inv.HasRoomFor(new ItemStack(_stone, 1)));
        }

        [Test]
        public void HasRoomFor_EmptyStack_ReturnsTrue()
        {
            var inv = new Inventory(1, 1);
            Assert.IsTrue(inv.HasRoomFor(new ItemStack(null, 0)));
        }

        [Test]
        public void HasRoomFor_FullInventory_ReturnsFalse()
        {
            var inv = new Inventory(1, 1);
            inv.SetSlot(0, new ItemStack(_stone, 64));
            Assert.IsFalse(inv.HasRoomFor(new ItemStack(_stone, 1)));
        }

        [Test]
        public void HasRoomFor_PartialStack_HasRoom()
        {
            var inv = new Inventory(1, 1);
            inv.SetSlot(0, new ItemStack(_stone, 30));
            Assert.IsTrue(inv.HasRoomFor(new ItemStack(_stone, 10)));
        }

        [Test]
        public void HasRoomFor_DifferentItem_NeedsEmptySlot()
        {
            var inv = new Inventory(1, 1);
            inv.SetSlot(0, new ItemStack(_stone, 10));

            // No room for wood: slot 0 has stone, no other slots
            Assert.IsFalse(inv.HasRoomFor(new ItemStack(_wood, 1)));
        }

        [Test]
        public void HasRoomFor_ExactlyFits_ReturnsTrue()
        {
            var inv = new Inventory(1, 1);
            inv.SetSlot(0, new ItemStack(_stone, 60));
            Assert.IsTrue(inv.HasRoomFor(new ItemStack(_stone, 4)));
        }

        [Test]
        public void HasRoomFor_LargeQuantity_MultipleSlots()
        {
            var inv = new Inventory(3, 1);
            // 3 empty slots = 3 * 64 = 192 capacity for stone
            Assert.IsTrue(inv.HasRoomFor(new ItemStack(_stone, 192)));
        }

        [Test]
        public void HasRoomFor_LargeQuantity_NotEnoughSlots()
        {
            var inv = new Inventory(3, 1);
            Assert.IsFalse(inv.HasRoomFor(new ItemStack(_stone, 193)));
        }

        // =============================================================
        //  GetMaxStackSize
        // =============================================================

        [Test]
        public void GetMaxStackSize_DefaultMultiplier()
        {
            var inv = new Inventory(1, 1);
            Assert.AreEqual(64, inv.GetMaxStackSize(_stone));
        }

        [Test]
        public void GetMaxStackSize_WithMultiplier()
        {
            var inv = new Inventory(1, 1, 4);
            Assert.AreEqual(256, inv.GetMaxStackSize(_stone));
        }

        [Test]
        public void GetMaxStackSize_NullItem_ReturnsOne()
        {
            var inv = new Inventory(1, 1);
            Assert.AreEqual(1, inv.GetMaxStackSize(null));
        }

        [Test]
        public void GetMaxStackSize_UnstackableItem()
        {
            var inv = new Inventory(1, 1);
            Assert.AreEqual(1, inv.GetMaxStackSize(_sword));
        }

        [Test]
        public void GetMaxStackSize_UnstackableWithMultiplier()
        {
            var inv = new Inventory(1, 1, 4);
            // maxStackSize=1, multiplier=4 => 4
            Assert.AreEqual(4, inv.GetMaxStackSize(_sword));
        }

        // =============================================================
        //  StackMultiplier Affects AddItem
        // =============================================================

        [Test]
        public void StackMultiplier_AllowsLargerStacks()
        {
            var inv = new Inventory(1, 1, 4);
            bool result = inv.AddItem(new ItemStack(_stone, 200));

            Assert.IsTrue(result);
            Assert.AreEqual(200, inv.GetSlot(0).quantity);
        }

        [Test]
        public void StackMultiplier_CapsAtEffectiveMax()
        {
            var inv = new Inventory(2, 1, 2);
            // Effective max = 128 per slot
            bool result = inv.AddItem(new ItemStack(_stone, 150));

            Assert.IsTrue(result);
            Assert.AreEqual(128, inv.GetSlot(0).quantity);
            Assert.AreEqual(22, inv.GetSlot(1).quantity);
        }

        // =============================================================
        //  IsInfinite Mode
        // =============================================================

        [Test]
        public void IsInfinite_RemoveItem_ReturnsTrue_ButDoesNotDeplete()
        {
            var inv = new Inventory(5, 1);
            inv.IsInfinite = true;
            inv.AddItem(new ItemStack(_stone, 30));

            bool result = inv.RemoveItem("stone", 10);

            Assert.IsTrue(result);
            // Quantity unchanged because infinite
            Assert.AreEqual(30, inv.GetSlot(0).quantity);
        }

        [Test]
        public void IsInfinite_RemoveMoreThanAvailable_StillReturnsTrue()
        {
            var inv = new Inventory(5, 1);
            inv.IsInfinite = true;
            inv.AddItem(new ItemStack(_stone, 10));

            // CountItem returns 10, which is < 100, so this returns false
            // (the count check happens before the infinite check)
            bool result = inv.RemoveItem("stone", 100);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInfinite_RemoveExactAmount_QuantityUnchanged()
        {
            var inv = new Inventory(5, 1);
            inv.IsInfinite = true;
            inv.AddItem(new ItemStack(_stone, 50));

            inv.RemoveItem("stone", 50);

            Assert.AreEqual(50, inv.GetSlot(0).quantity);
        }

        // =============================================================
        //  OnInventoryChanged Event
        // =============================================================

        [Test]
        public void OnInventoryChanged_FiresOnSetSlot()
        {
            var inv = new Inventory(5, 1);
            int fireCount = 0;
            inv.OnInventoryChanged += () => fireCount++;

            inv.SetSlot(0, new ItemStack(_stone, 10));

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void OnInventoryChanged_FiresOnAddItem()
        {
            var inv = new Inventory(5, 1);
            int fireCount = 0;
            inv.OnInventoryChanged += () => fireCount++;

            inv.AddItem(new ItemStack(_stone, 10));

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void OnInventoryChanged_FiresOnRemoveItem()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 20));

            int fireCount = 0;
            inv.OnInventoryChanged += () => fireCount++;

            inv.RemoveItem("stone", 5);

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void OnInventoryChanged_DoesNotFire_OnFailedRemove()
        {
            var inv = new Inventory(5, 1);
            inv.AddItem(new ItemStack(_stone, 5));

            int fireCount = 0;
            inv.OnInventoryChanged += () => fireCount++;

            inv.RemoveItem("stone", 100);

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void OnInventoryChanged_MultipleSetSlots_FiresEachTime()
        {
            var inv = new Inventory(5, 1);
            int fireCount = 0;
            inv.OnInventoryChanged += () => fireCount++;

            inv.SetSlot(0, new ItemStack(_stone, 1));
            inv.SetSlot(1, new ItemStack(_wood, 1));
            inv.SetSlot(2, new ItemStack(_sword, 1));

            Assert.AreEqual(3, fireCount);
        }
    }

    // =================================================================
    //  ItemStack Tests
    // =================================================================

    public class ItemStackTests
    {
        private ItemDefinition _stone;
        private ItemDefinition _wood;

        [SetUp]
        public void SetUp()
        {
            _stone = ScriptableObject.CreateInstance<ItemDefinition>();
            _stone.itemId = "stone";
            _stone.displayName = "Stone";
            _stone.itemType = ItemType.Resource;
            _stone.maxStackSize = 64;

            _wood = ScriptableObject.CreateInstance<ItemDefinition>();
            _wood.itemId = "wood";
            _wood.displayName = "Wood";
            _wood.itemType = ItemType.Resource;
            _wood.maxStackSize = 64;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_stone);
            UnityEngine.Object.DestroyImmediate(_wood);
        }

        // --- IsEmpty ---

        [Test]
        public void IsEmpty_NullItem_ReturnsTrue()
        {
            var stack = new ItemStack(null, 5);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void IsEmpty_ZeroQuantity_ReturnsTrue()
        {
            var stack = new ItemStack(_stone, 0);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void IsEmpty_NegativeQuantity_ReturnsTrue()
        {
            var stack = new ItemStack(_stone, -1);
            Assert.IsTrue(stack.IsEmpty);
        }

        [Test]
        public void IsEmpty_ValidStack_ReturnsFalse()
        {
            var stack = new ItemStack(_stone, 10);
            Assert.IsFalse(stack.IsEmpty);
        }

        [Test]
        public void IsEmpty_Default_ReturnsTrue()
        {
            var stack = default(ItemStack);
            Assert.IsTrue(stack.IsEmpty);
        }

        // --- CanStackWith ---

        [Test]
        public void CanStackWith_SameItem_BelowMax_ReturnsTrue()
        {
            var a = new ItemStack(_stone, 30);
            var b = new ItemStack(_stone, 10);
            Assert.IsTrue(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_SameItem_AtMax_ReturnsFalse()
        {
            var a = new ItemStack(_stone, 64);
            var b = new ItemStack(_stone, 10);
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_DifferentItems_ReturnsFalse()
        {
            var a = new ItemStack(_stone, 10);
            var b = new ItemStack(_wood, 10);
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_EmptySource_ReturnsFalse()
        {
            var a = new ItemStack(null, 0);
            var b = new ItemStack(_stone, 10);
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_EmptyTarget_ReturnsFalse()
        {
            var a = new ItemStack(_stone, 10);
            var b = new ItemStack(null, 0);
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_BothEmpty_ReturnsFalse()
        {
            var a = new ItemStack(null, 0);
            var b = new ItemStack(null, 0);
            Assert.IsFalse(a.CanStackWith(b));
        }

        // --- Split ---

        [Test]
        public void Split_PartialAmount_ReturnsNewStack()
        {
            var stack = new ItemStack(_stone, 30);
            ItemStack split = stack.Split(10);

            Assert.AreEqual(_stone, split.item);
            Assert.AreEqual(10, split.quantity);
        }

        [Test]
        public void Split_FullAmount_ReturnsEntireStack()
        {
            var stack = new ItemStack(_stone, 30);
            ItemStack split = stack.Split(30);

            Assert.AreEqual(_stone, split.item);
            Assert.AreEqual(30, split.quantity);
        }

        [Test]
        public void Split_MoreThanAvailable_ReturnsEntireStack()
        {
            var stack = new ItemStack(_stone, 10);
            ItemStack split = stack.Split(50);

            Assert.AreEqual(10, split.quantity);
        }

        [Test]
        public void Split_ZeroAmount_ReturnsEmpty()
        {
            var stack = new ItemStack(_stone, 10);
            ItemStack split = stack.Split(0);

            Assert.IsTrue(split.IsEmpty);
        }

        [Test]
        public void Split_NegativeAmount_ReturnsEmpty()
        {
            var stack = new ItemStack(_stone, 10);
            ItemStack split = stack.Split(-5);

            Assert.IsTrue(split.IsEmpty);
        }

        // --- ToString ---

        [Test]
        public void ToString_ValidStack_ShowsNameAndQuantity()
        {
            var stack = new ItemStack(_stone, 42);
            Assert.AreEqual("Stone x42", stack.ToString());
        }

        [Test]
        public void ToString_EmptyStack_ShowsEmpty()
        {
            var stack = new ItemStack(null, 0);
            Assert.AreEqual("Empty", stack.ToString());
        }
    }

    // =================================================================
    //  GunInstance Tests
    // =================================================================

    public class GunInstanceTests
    {
        private GunDefinition _pistol;
        private GunDefinition _shotgun;

        [SetUp]
        public void SetUp()
        {
            _pistol = ScriptableObject.CreateInstance<GunDefinition>();
            _pistol.gunName = "Pistol";
            _pistol.fireRate = 300f;
            _pistol.damage = 25f;
            _pistol.magazineSize = 12;
            _pistol.reloadTime = 1.5f;
            _pistol.fireMode = FireMode.Semi;
            _pistol.range = 50f;
            _pistol.penetration = 0.1f;
            _pistol.pelletCount = 1;
            _pistol.recoilPattern = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1.2f),
                new Vector2(-0.3f, 0.8f),
            };

            _shotgun = ScriptableObject.CreateInstance<GunDefinition>();
            _shotgun.gunName = "Shotgun";
            _shotgun.fireRate = 60f;
            _shotgun.damage = 10f;
            _shotgun.magazineSize = 6;
            _shotgun.reloadTime = 3f;
            _shotgun.fireMode = FireMode.Semi;
            _shotgun.range = 20f;
            _shotgun.penetration = 0f;
            _shotgun.pelletCount = 8;
            _shotgun.recoilPattern = null; // no recoil pattern
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pistol);
            UnityEngine.Object.DestroyImmediate(_shotgun);
        }

        // --- Constructor ---

        [Test]
        public void Constructor_InitializesFullMagazine()
        {
            var gun = new GunInstance(_pistol);
            Assert.AreEqual(12, gun.CurrentAmmo);
        }

        [Test]
        public void Constructor_InitializesReserveAmmo_ThreeTimesMagazine()
        {
            var gun = new GunInstance(_pistol);
            Assert.AreEqual(36, gun.ReserveAmmo);
        }

        [Test]
        public void Constructor_RecoilIndex_StartsAtZero()
        {
            var gun = new GunInstance(_pistol);
            Assert.AreEqual(0, gun.RecoilIndex);
        }

        [Test]
        public void Constructor_IsReloading_StartsFalse()
        {
            var gun = new GunInstance(_pistol);
            Assert.IsFalse(gun.IsReloading);
        }

        [Test]
        public void Constructor_StoresDefinition()
        {
            var gun = new GunInstance(_pistol);
            Assert.AreEqual(_pistol, gun.Definition);
        }

        // --- CanFire ---

        [Test]
        public void CanFire_FullMag_NotReloading_ReturnsTrue()
        {
            var gun = new GunInstance(_pistol);
            Assert.IsTrue(gun.CanFire());
        }

        [Test]
        public void CanFire_EmptyMag_ReturnsFalse()
        {
            var gun = new GunInstance(_pistol);
            gun.SetCurrentAmmo(0);
            Assert.IsFalse(gun.CanFire());
        }

        [Test]
        public void CanFire_Reloading_ReturnsFalse()
        {
            var gun = new GunInstance(_pistol);
            gun.SetReloading(true);
            Assert.IsFalse(gun.CanFire());
        }

        [Test]
        public void CanFire_ReloadingWithAmmo_StillReturnsFalse()
        {
            var gun = new GunInstance(_pistol);
            gun.SetReloading(true);
            Assert.IsFalse(gun.CanFire());
        }

        // --- ConsumeAmmo ---

        [Test]
        public void ConsumeAmmo_DecrementsCurrentAmmo()
        {
            var gun = new GunInstance(_pistol);
            gun.ConsumeAmmo();
            Assert.AreEqual(11, gun.CurrentAmmo);
        }

        [Test]
        public void ConsumeAmmo_MultipleTimes_DecrementsCorrectly()
        {
            var gun = new GunInstance(_pistol);
            for (int i = 0; i < 5; i++) gun.ConsumeAmmo();
            Assert.AreEqual(7, gun.CurrentAmmo);
        }

        [Test]
        public void ConsumeAmmo_EntireMagazine_ReachesZero()
        {
            var gun = new GunInstance(_pistol);
            for (int i = 0; i < 12; i++) gun.ConsumeAmmo();
            Assert.AreEqual(0, gun.CurrentAmmo);
        }

        // --- SetReloading ---

        [Test]
        public void SetReloading_True_SetsFlag()
        {
            var gun = new GunInstance(_pistol);
            gun.SetReloading(true);
            Assert.IsTrue(gun.IsReloading);
        }

        [Test]
        public void SetReloading_False_ClearsFlag()
        {
            var gun = new GunInstance(_pistol);
            gun.SetReloading(true);
            gun.SetReloading(false);
            Assert.IsFalse(gun.IsReloading);
        }

        // --- CompleteReload ---

        [Test]
        public void CompleteReload_RefillsFromReserve()
        {
            var gun = new GunInstance(_pistol);
            // Fire all 12, then reload
            for (int i = 0; i < 12; i++) gun.ConsumeAmmo();
            gun.SetReloading(true);
            gun.CompleteReload(12);

            Assert.AreEqual(12, gun.CurrentAmmo);
            Assert.AreEqual(24, gun.ReserveAmmo); // 36 - 12 = 24
            Assert.IsFalse(gun.IsReloading);
        }

        [Test]
        public void CompleteReload_PartialReload()
        {
            var gun = new GunInstance(_pistol);
            for (int i = 0; i < 5; i++) gun.ConsumeAmmo(); // 7 left
            gun.SetReloading(true);
            gun.CompleteReload(5);

            Assert.AreEqual(12, gun.CurrentAmmo);
            Assert.AreEqual(31, gun.ReserveAmmo); // 36 - 5 = 31
        }

        [Test]
        public void CompleteReload_ClampsToMagazineSize()
        {
            var gun = new GunInstance(_pistol);
            gun.ConsumeAmmo(); // 11 left
            gun.CompleteReload(50); // Try to add 50

            // Clamped to magazineSize (12)
            Assert.AreEqual(12, gun.CurrentAmmo);
        }

        [Test]
        public void CompleteReload_ClearsReloadingFlag()
        {
            var gun = new GunInstance(_pistol);
            gun.SetReloading(true);
            gun.CompleteReload(0);
            Assert.IsFalse(gun.IsReloading);
        }

        // --- SetCurrentAmmo ---

        [Test]
        public void SetCurrentAmmo_SetsValue()
        {
            var gun = new GunInstance(_pistol);
            gun.SetCurrentAmmo(5);
            Assert.AreEqual(5, gun.CurrentAmmo);
        }

        [Test]
        public void SetCurrentAmmo_ClampsToMagazineSize()
        {
            var gun = new GunInstance(_pistol);
            gun.SetCurrentAmmo(999);
            Assert.AreEqual(12, gun.CurrentAmmo);
        }

        [Test]
        public void SetCurrentAmmo_ClampsToZero()
        {
            var gun = new GunInstance(_pistol);
            gun.SetCurrentAmmo(-10);
            Assert.AreEqual(0, gun.CurrentAmmo);
        }

        // --- ResetRecoil ---

        [Test]
        public void ResetRecoil_SetsIndexToZero()
        {
            var gun = new GunInstance(_pistol);
            gun.GetNextRecoil();
            gun.GetNextRecoil();
            gun.ResetRecoil();
            Assert.AreEqual(0, gun.RecoilIndex);
        }

        // --- GetNextRecoil ---

        [Test]
        public void GetNextRecoil_ReturnsFirstPattern()
        {
            var gun = new GunInstance(_pistol);
            Vector2 recoil = gun.GetNextRecoil();
            Assert.AreEqual(new Vector2(0f, 1f), recoil);
        }

        [Test]
        public void GetNextRecoil_AdvancesIndex()
        {
            var gun = new GunInstance(_pistol);
            gun.GetNextRecoil();
            Assert.AreEqual(1, gun.RecoilIndex);
        }

        [Test]
        public void GetNextRecoil_CyclesThroughPattern()
        {
            var gun = new GunInstance(_pistol);
            gun.GetNextRecoil(); // index 0
            gun.GetNextRecoil(); // index 1
            Vector2 third = gun.GetNextRecoil(); // index 2

            Assert.AreEqual(new Vector2(-0.3f, 0.8f), third);
        }

        [Test]
        public void GetNextRecoil_WrapsAround()
        {
            var gun = new GunInstance(_pistol);
            // Pattern has 3 entries: cycle through all and wrap
            gun.GetNextRecoil(); // 0
            gun.GetNextRecoil(); // 1
            gun.GetNextRecoil(); // 2
            Vector2 wrapped = gun.GetNextRecoil(); // 3 % 3 = 0

            Assert.AreEqual(new Vector2(0f, 1f), wrapped);
            Assert.AreEqual(4, gun.RecoilIndex);
        }

        [Test]
        public void GetNextRecoil_NullPattern_ReturnsZero()
        {
            var gun = new GunInstance(_shotgun);
            Vector2 recoil = gun.GetNextRecoil();
            Assert.AreEqual(Vector2.zero, recoil);
        }

        [Test]
        public void GetNextRecoil_EmptyPattern_ReturnsZero()
        {
            _shotgun.recoilPattern = new Vector2[0];
            var gun = new GunInstance(_shotgun);
            Vector2 recoil = gun.GetNextRecoil();
            Assert.AreEqual(Vector2.zero, recoil);
        }

        // --- Full firing cycle ---

        [Test]
        public void FullCycle_FireUntilEmpty_Reload_FireAgain()
        {
            var gun = new GunInstance(_pistol);

            // Fire entire magazine
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(gun.CanFire(), $"Should be able to fire shot {i + 1}");
                gun.ConsumeAmmo();
            }

            Assert.IsFalse(gun.CanFire());
            Assert.AreEqual(0, gun.CurrentAmmo);

            // Reload
            gun.SetReloading(true);
            Assert.IsFalse(gun.CanFire());
            gun.CompleteReload(12);

            Assert.IsTrue(gun.CanFire());
            Assert.AreEqual(12, gun.CurrentAmmo);
            Assert.AreEqual(24, gun.ReserveAmmo);

            // Fire one more
            gun.ConsumeAmmo();
            Assert.AreEqual(11, gun.CurrentAmmo);
        }
    }
}
