using System.Collections.Generic;
using UnityEngine;
using Voidborne.Automation.Transport;

namespace Voidborne.Automation.Machines
{
    /// <summary>
    /// V9.2 — Storage Chest (Picky_Specialty). Not a crafting station per se:
    /// it's an inventory container that exposes a simple deposit / withdraw
    /// API used by the V9.5 Inserter and by player interaction.
    ///
    /// <para>Container size is fixed at 9x3 (27 slots) for M2's wooden chest
    /// tier. Future tiers (iron 9x6, compression 9x12) can subclass + bump
    /// <see cref="Cols"/> / <see cref="Rows"/>.</para>
    ///
    /// <para>On <see cref="Interact"/>, the runtime reuses the existing
    /// <see cref="ChestBlock"/> + ChestUI pipeline by auto-attaching a
    /// <see cref="ChestBlock"/> sibling and binding the runtime's
    /// <see cref="Inventory"/> into it. This avoids building a parallel
    /// ContainerUI from scratch -- the V4.x ChestUI already covers it.</para>
    /// </summary>
    public class StorageChestRuntime : MachineRuntime, IBeltSink
    {
        // ---------------------------------------------------------------
        //  Configuration
        // ---------------------------------------------------------------

        [Tooltip("Grid width. Default 9 (wooden chest).")]
        [SerializeField] private int cols = 9;

        [Tooltip("Grid height. Default 3 (wooden chest).")]
        [SerializeField] private int rows = 3;

        // ---------------------------------------------------------------
        //  Storage
        // ---------------------------------------------------------------

        private Inventory _contents;
        private ChestBlock _chestSibling;

        /// <summary>The chest's inventory. Lazily allocated on first access.</summary>
        public Inventory Contents
        {
            get
            {
                EnsureInventory();
                return _contents;
            }
        }

        /// <summary>Read-only enumeration of the current chest contents (for tests).</summary>
        public IReadOnlyList<ItemStack> ContentsView
        {
            get
            {
                EnsureInventory();
                var list = new List<ItemStack>(_contents.SlotCount);
                for (int i = 0; i < _contents.SlotCount; i++) list.Add(_contents.GetSlot(i));
                return list;
            }
        }

        /// <summary>Total slot count (cols*rows).</summary>
        public int SlotCount => cols * rows;

        /// <summary>Width of the chest grid.</summary>
        public int Cols => cols;

        /// <summary>Height of the chest grid.</summary>
        public int Rows => rows;

        // ---------------------------------------------------------------
        //  Public deposit / withdraw API (used by Inserter)
        // ---------------------------------------------------------------

        /// <summary>
        /// Try to deposit <paramref name="stack"/> into the chest. Returns true
        /// iff every unit landed in the inventory.
        /// </summary>
        public bool TryDeposit(ItemStack stack)
        {
            if (stack.IsEmpty) return true;
            EnsureInventory();
            return _contents.AddItem(stack);
        }

        /// <summary>
        /// Try to pull <paramref name="qty"/> units of <paramref name="itemId"/>
        /// out of the chest. Returns the resulting <see cref="ItemStack"/>;
        /// empty if no matching items are available.
        /// </summary>
        public ItemStack TryWithdraw(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return default;
            EnsureInventory();
            int available = _contents.CountItem(itemId);
            int take = Mathf.Min(qty, available);
            if (take <= 0) return default;
            // Resolve the ItemDefinition by scanning the slots.
            ItemDefinition def = null;
            for (int i = 0; i < _contents.SlotCount; i++)
            {
                var slot = _contents.GetSlot(i);
                if (!slot.IsEmpty && slot.item != null && slot.item.itemId == itemId)
                {
                    def = slot.item;
                    break;
                }
            }
            if (def == null) return default;
            if (!_contents.RemoveItem(itemId, take)) return default;
            return new ItemStack(def, take);
        }

        // ---------------------------------------------------------------
        //  IBeltSink (V9.5)
        // ---------------------------------------------------------------

        public bool CanInsert
        {
            get
            {
                EnsureInventory();
                // True if there's any open / partially-full slot. The
                // Inventory.HasRoomFor check is per-item; we approximate
                // by "is any slot empty" since a belt insert is one stack
                // and the receiving slot policy is "AddItem".
                for (int i = 0; i < _contents.SlotCount; i++)
                {
                    if (_contents.GetSlot(i).IsEmpty) return true;
                }
                return false;
            }
        }

        public bool TryInsert(ItemStack stack) => TryDeposit(stack);

        // ---------------------------------------------------------------
        //  Interact
        // ---------------------------------------------------------------

        public override void Interact(GameObject interactor)
        {
            if (UIManager.Instance == null) return;
            EnsureInventory();
            EnsureChestSibling();
            if (UIManager.Instance.IsChestOpen(_chestSibling))
                UIManager.Instance.CloseChest();
            else
                UIManager.Instance.OpenChest(_chestSibling);
        }

        public override string InteractPrompt => "Open Storage Chest";

        // ---------------------------------------------------------------
        //  Internal
        // ---------------------------------------------------------------

        private void EnsureInventory()
        {
            if (_contents == null)
            {
                _contents = new Inventory(Mathf.Max(1, cols), Mathf.Max(1, rows));
            }
        }

        private void EnsureChestSibling()
        {
            if (_chestSibling == null)
            {
                _chestSibling = GetComponent<ChestBlock>();
                if (_chestSibling == null)
                {
                    _chestSibling = gameObject.AddComponent<ChestBlock>();
                }
            }
            _chestSibling.BindInventory(_contents);
        }
    }
}
