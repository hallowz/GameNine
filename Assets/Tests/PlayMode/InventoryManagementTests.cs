using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Voidborne.Tests.PlayMode
{
    [TestFixture]
    public class InventoryManagementTests
    {
        private GameObject _playerGO;
        private PlayerInventory _inventory;

        private ItemDefinition _resourceA;
        private ItemDefinition _resourceB;

        [SetUp]
        public void SetUp()
        {
            _playerGO = new GameObject("TestPlayer");
            _inventory = _playerGO.AddComponent<PlayerInventory>();

            _resourceA = ScriptableObject.CreateInstance<ItemDefinition>();
            _resourceA.itemId = "test_ore";
            _resourceA.displayName = "Test Ore";
            _resourceA.itemType = ItemType.Resource;
            _resourceA.maxStackSize = 64;

            _resourceB = ScriptableObject.CreateInstance<ItemDefinition>();
            _resourceB.itemId = "test_wood";
            _resourceB.displayName = "Test Wood";
            _resourceB.itemType = ItemType.Resource;
            _resourceB.maxStackSize = 64;
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGO != null)
                Object.Destroy(_playerGO);

            if (_resourceA != null)
                Object.Destroy(_resourceA);
            if (_resourceB != null)
                Object.Destroy(_resourceB);

            // Clear static event subscribers to avoid leaking between tests.
            ClearStaticEvent(typeof(PlayerInventory), "OnItemAdded");
        }

        private static void ClearStaticEvent(System.Type type, string eventName)
        {
            var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) field.SetValue(null, null);
        }

        // ---------------------------------------------------------------
        //  1. Initialization
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Hotbar_InitializesWithCorrectSize()
        {
            yield return null; // Allow Awake to run.

            Assert.IsNotNull(_inventory.Hotbar, "Hotbar should not be null after Awake.");
            Assert.AreEqual(5, _inventory.Hotbar.Width, "Hotbar width should be 5.");
            Assert.AreEqual(1, _inventory.Hotbar.Height, "Hotbar height should be 1.");
            Assert.AreEqual(5, _inventory.Hotbar.SlotCount, "Hotbar should have 5 slots.");
        }

        [UnityTest]
        public IEnumerator Main_InitializesWithCorrectSize()
        {
            yield return null;

            Assert.IsNotNull(_inventory.Main, "Main inventory should not be null after Awake.");
            Assert.AreEqual(5, _inventory.Main.Width, "Main width should be 5.");
            Assert.AreEqual(7, _inventory.Main.Height, "Main height should be 7.");
            Assert.AreEqual(35, _inventory.Main.SlotCount, "Main should have 35 slots.");
        }

        // ---------------------------------------------------------------
        //  2. Adding items fills hotbar first
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AddItem_FillsHotbarFirst()
        {
            yield return null;

            bool result = _inventory.AddItem(new ItemStack(_resourceA, 10));

            Assert.IsTrue(result, "AddItem should succeed.");
            Assert.AreEqual(10, _inventory.Hotbar.CountItem("test_ore"),
                "Items should land in the hotbar first.");
            Assert.AreEqual(0, _inventory.Main.CountItem("test_ore"),
                "Main inventory should still be empty.");
        }

        // ---------------------------------------------------------------
        //  3. Overflow to main inventory
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AddItem_OverflowsToMainInventory()
        {
            yield return null;

            // Fill all 5 hotbar slots (5 x 64 = 320).
            int hotbarCapacity = 5 * _resourceA.maxStackSize;
            _inventory.AddItem(new ItemStack(_resourceA, hotbarCapacity));

            // Add more — should overflow into main.
            _inventory.AddItem(new ItemStack(_resourceA, 30));

            Assert.AreEqual(hotbarCapacity, _inventory.Hotbar.CountItem("test_ore"),
                "Hotbar should remain full.");
            Assert.AreEqual(30, _inventory.Main.CountItem("test_ore"),
                "Overflow should go to main inventory.");
        }

        // ---------------------------------------------------------------
        //  4. RemoveItem removes by itemId
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator RemoveItem_RemovesByItemId()
        {
            yield return null;

            _inventory.AddItem(new ItemStack(_resourceA, 20));
            bool removed = _inventory.RemoveItem("test_ore", 8);

            Assert.IsTrue(removed, "RemoveItem should return true.");
            Assert.AreEqual(12, _inventory.CountAllItem("test_ore"),
                "Remaining count should be 12.");
        }

        [UnityTest]
        public IEnumerator RemoveItem_ReturnsFalse_WhenInsufficientQuantity()
        {
            yield return null;

            _inventory.AddItem(new ItemStack(_resourceA, 5));
            bool removed = _inventory.RemoveItem("test_ore", 10);

            Assert.IsFalse(removed, "RemoveItem should return false when quantity is insufficient.");
            Assert.AreEqual(5, _inventory.CountAllItem("test_ore"),
                "Count should be unchanged after failed removal.");
        }

        // ---------------------------------------------------------------
        //  5. CountAllItem counts across all inventories
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CountAllItem_CountsAcrossAllInventories()
        {
            yield return null;

            // Place items in hotbar directly and main directly.
            _inventory.Hotbar.AddItem(new ItemStack(_resourceA, 20));
            _inventory.Main.AddItem(new ItemStack(_resourceA, 15));

            Assert.AreEqual(35, _inventory.CountAllItem("test_ore"),
                "CountAllItem should sum hotbar and main.");
        }

        // ---------------------------------------------------------------
        //  6. HasItem returns correct boolean
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator HasItem_ReturnsTrue_WhenItemExists()
        {
            yield return null;

            _inventory.AddItem(new ItemStack(_resourceA, 1));

            Assert.IsTrue(_inventory.HasItem(_resourceA),
                "HasItem should return true when the item is in inventory.");
        }

        [UnityTest]
        public IEnumerator HasItem_ReturnsFalse_WhenItemMissing()
        {
            yield return null;

            Assert.IsFalse(_inventory.HasItem(_resourceB),
                "HasItem should return false when no items present.");
        }

        [UnityTest]
        public IEnumerator HasItem_ReturnsFalse_WhenNull()
        {
            yield return null;

            Assert.IsFalse(_inventory.HasItem(null),
                "HasItem should return false for null.");
        }

        // ---------------------------------------------------------------
        //  7. SelectedHotbarIndex changes correctly
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator SelectedHotbarIndex_DefaultsToZero()
        {
            yield return null;

            Assert.AreEqual(0, _inventory.SelectedHotbarIndex);
        }

        [UnityTest]
        public IEnumerator SelectedHotbarIndex_ClampsToValidRange()
        {
            yield return null;

            _inventory.SelectedHotbarIndex = 3;
            Assert.AreEqual(3, _inventory.SelectedHotbarIndex,
                "Should accept valid index 3.");

            _inventory.SelectedHotbarIndex = -1;
            Assert.AreEqual(0, _inventory.SelectedHotbarIndex,
                "Negative index should clamp to 0.");

            _inventory.SelectedHotbarIndex = 99;
            Assert.AreEqual(4, _inventory.SelectedHotbarIndex,
                "Index above max should clamp to 4.");
        }

        // ---------------------------------------------------------------
        //  8. ActiveHotbarItem returns correct stack
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator ActiveHotbarItem_ReturnsCorrectStack()
        {
            yield return null;

            _inventory.Hotbar.SetSlot(0, new ItemStack(_resourceA, 5));
            _inventory.Hotbar.SetSlot(2, new ItemStack(_resourceB, 3));

            _inventory.SelectedHotbarIndex = 0;
            ItemStack active0 = _inventory.ActiveHotbarItem;
            Assert.AreEqual(_resourceA, active0.item);
            Assert.AreEqual(5, active0.quantity);

            _inventory.SelectedHotbarIndex = 2;
            ItemStack active2 = _inventory.ActiveHotbarItem;
            Assert.AreEqual(_resourceB, active2.item);
            Assert.AreEqual(3, active2.quantity);
        }

        [UnityTest]
        public IEnumerator ActiveHotbarItem_ReturnsEmpty_WhenSlotIsEmpty()
        {
            yield return null;

            _inventory.SelectedHotbarIndex = 4;
            ItemStack active = _inventory.ActiveHotbarItem;
            Assert.IsTrue(active.IsEmpty, "Empty slot should return an empty ItemStack.");
        }

        // ---------------------------------------------------------------
        //  9. OnInventoryChanged fires when items added/removed
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnInventoryChanged_Fires_WhenItemAdded()
        {
            yield return null;

            int fireCount = 0;
            _inventory.OnInventoryChanged += () => fireCount++;

            _inventory.AddItem(new ItemStack(_resourceA, 1));

            Assert.Greater(fireCount, 0,
                "OnInventoryChanged should fire at least once when an item is added.");
        }

        [UnityTest]
        public IEnumerator OnInventoryChanged_Fires_WhenItemRemoved()
        {
            yield return null;

            _inventory.AddItem(new ItemStack(_resourceA, 10));

            int fireCount = 0;
            _inventory.OnInventoryChanged += () => fireCount++;

            _inventory.RemoveItem("test_ore", 5);

            Assert.Greater(fireCount, 0,
                "OnInventoryChanged should fire at least once when an item is removed.");
        }

        // ---------------------------------------------------------------
        //  10. OnItemAdded static event fires with correct parameters
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnItemAdded_Fires_WithCorrectParameters()
        {
            yield return null;

            ItemDefinition receivedItem = null;
            int receivedCount = 0;

            PlayerInventory.OnItemAdded += (item, count) =>
            {
                receivedItem = item;
                receivedCount = count;
            };

            _inventory.AddItem(new ItemStack(_resourceA, 7));

            Assert.AreEqual(_resourceA, receivedItem,
                "OnItemAdded should fire with the correct ItemDefinition.");
            Assert.AreEqual(7, receivedCount,
                "OnItemAdded should fire with the correct quantity.");
        }

        [UnityTest]
        public IEnumerator OnItemAdded_DoesNotFire_WhenStackIsEmpty()
        {
            yield return null;

            bool fired = false;
            PlayerInventory.OnItemAdded += (_, __) => fired = true;

            _inventory.AddItem(new ItemStack(null, 0));

            Assert.IsFalse(fired,
                "OnItemAdded should not fire for an empty stack.");
        }
    }
}
