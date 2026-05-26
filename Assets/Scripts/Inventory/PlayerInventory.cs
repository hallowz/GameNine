using System.Collections.Generic;
using UnityEngine;

public enum SlotType { Hotbar, Main, WornBackpack, BackpackContents }

/// <summary>
/// MonoBehaviour that lives on the Player GameObject.
/// Manages four logical inventories:
///   • Hotbar         — 5 × 1  (5 slots, always visible on bracer)
///   • Main           — 5 × 7  (35 slots, opened with inventory key)
///   • WornSlot       — 1 × 1  (dedicated backpack equipment slot)
///   • BackpackInventory — sized by the worn BackpackItem, null when empty
///
/// The selected hotbar slot index is tracked here so other systems
/// (e.g., weapon switching, item use) can query it.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Inventories
    // ---------------------------------------------------------------

    /// <summary>5-slot hotbar row (visible on Index bracer).</summary>
    public Inventory Hotbar { get; private set; }

    /// <summary>35-slot main inventory (5 × 7).</summary>
    public Inventory Main { get; private set; }

    /// <summary>
    /// Single-slot inventory representing the worn backpack equipment slot.
    /// Only BackpackItem stacks are valid here.
    /// </summary>
    public Inventory WornSlotInventory { get; private set; }

    /// <summary>
    /// The worn backpack's internal inventory.  Null when nothing is equipped.
    /// </summary>
    public Inventory BackpackInventory => _wornBackpackInstance?.Inventory;

    // ---------------------------------------------------------------
    //  Worn backpack state
    // ---------------------------------------------------------------

    private BackpackInstance _wornBackpackInstance;

    public BackpackInstance WornBackpackInstance => _wornBackpackInstance;

    // ---------------------------------------------------------------
    //  Hotbar backpack instances (keyed by hotbar slot index)
    // ---------------------------------------------------------------

    private readonly Dictionary<int, BackpackInstance> _hotbarBackpackInstances =
        new Dictionary<int, BackpackInstance>();

    // ---------------------------------------------------------------
    //  Selected hotbar index
    // ---------------------------------------------------------------

    private int _selectedHotbarIndex = 0;

    /// <summary>Currently selected hotbar slot (0–4).</summary>
    public int SelectedHotbarIndex
    {
        get => _selectedHotbarIndex;
        set => _selectedHotbarIndex = Mathf.Clamp(value, 0, Hotbar.SlotCount - 1);
    }

    /// <summary>The ItemStack in the currently selected hotbar slot.</summary>
    public ItemStack ActiveHotbarItem => Hotbar.GetSlot(_selectedHotbarIndex);

    // ---------------------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        Hotbar           = new Inventory(5, 1);
        Main             = new Inventory(5, 7);
        WornSlotInventory = new Inventory(1, 1);

        Hotbar.OnInventoryChanged            += RaiseChanged;
        Main.OnInventoryChanged              += RaiseChanged;
        WornSlotInventory.OnInventoryChanged += OnWornSlotChanged;
    }

    private void Start()
    {
        AddStarterItems();
    }

    private void AddStarterItems()
    {
        ItemDatabase db = ItemDatabase.GetOrLoad();
        if (db == null)
        {
            Debug.LogWarning("[PlayerInventory] Could not load ItemDatabase — starter items not added.");
            return;
        }

        // Only the dev backpack, placed directly in the worn slot
        ItemDefinition backpackDef = db.GetItem("dev_backpack");
        if (backpackDef == null)
        {
            Debug.LogWarning("[PlayerInventory] Starter item 'dev_backpack' not found in ItemDatabase.");
            return;
        }
        WornSlotInventory.SetSlot(0, new ItemStack(backpackDef, 1));
    }

    // ---------------------------------------------------------------
    //  Public event
    // ---------------------------------------------------------------

    public event System.Action OnInventoryChanged;
    private void RaiseChanged() => OnInventoryChanged?.Invoke();

    /// <summary>Fired whenever items are successfully added to any player inventory slot.</summary>
    public static event System.Action<ItemDefinition, int> OnItemAdded;

    // ---------------------------------------------------------------
    //  Worn slot change handler
    // ---------------------------------------------------------------

    private void OnWornSlotChanged()
    {
        ItemStack slot = WornSlotInventory.GetSlot(0);

        if (_wornBackpackInstance != null)
            _wornBackpackInstance.Inventory.OnInventoryChanged -= RaiseChanged;

        if (!slot.IsEmpty && slot.item is BackpackItem bp)
        {
            // Reuse existing instance if available (e.g., moved from hotbar)
            if (_wornBackpackInstance == null || _wornBackpackInstance.Definition != bp)
                _wornBackpackInstance = new BackpackInstance(bp);
            _wornBackpackInstance.Inventory.OnInventoryChanged += RaiseChanged;
        }
        else
        {
            _wornBackpackInstance = null;
        }

        RaiseChanged();
    }

    // ---------------------------------------------------------------
    //  Worn slot validation
    // ---------------------------------------------------------------

    /// <summary>
    /// Returns true if <paramref name="stack"/> is allowed in <paramref name="slot"/>.
    /// BackpackItems are allowed anywhere except inside another backpack's contents.
    /// </summary>
    public bool CanPlaceInSlot(ItemStack stack, SlotType slot)
    {
        if (stack.IsEmpty) return true;
        if (stack.item is BackpackItem)
            return slot != SlotType.BackpackContents;
        return true;
    }

    // ---------------------------------------------------------------
    //  Hotbar backpack instances
    // ---------------------------------------------------------------

    /// <summary>
    /// Gets or creates a BackpackInstance for the given hotbar slot.
    /// Returns null if that hotbar slot does not contain a BackpackItem.
    /// </summary>
    public BackpackInstance GetOrCreateHotbarBackpackInstance(int hotbarSlot)
    {
        ItemStack stack = Hotbar.GetSlot(hotbarSlot);
        if (stack.IsEmpty || !(stack.item is BackpackItem bp)) return null;

        if (!_hotbarBackpackInstances.TryGetValue(hotbarSlot, out var inst) || inst == null)
        {
            inst = new BackpackInstance(bp);
            _hotbarBackpackInstances[hotbarSlot] = inst;
        }
        return inst;
    }

    /// <summary>
    /// Restores a previously-created BackpackInstance to a hotbar slot
    /// (used when a dropped backpack is picked back up).
    /// </summary>
    public void RestoreHotbarBackpackInstance(int hotbarSlot, BackpackInstance instance)
    {
        if (instance != null)
            _hotbarBackpackInstances[hotbarSlot] = instance;
    }

    // ---------------------------------------------------------------
    //  Convenience helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Tries to add an ItemStack to hotbar first, then main, then backpack.
    /// Returns true if all items were placed.
    /// </summary>
    public bool AddItem(ItemStack stack)
    {
        if (stack.IsEmpty) return true;

        int remaining = stack.quantity;
        remaining = AddToInventoryGetRemainder(Hotbar, stack.item, remaining);
        if (remaining == 0) { OnItemAdded?.Invoke(stack.item, stack.quantity); return true; }
        remaining = AddToInventoryGetRemainder(Main, stack.item, remaining);
        if (remaining == 0) { OnItemAdded?.Invoke(stack.item, stack.quantity); return true; }
        if (BackpackInventory != null && !(stack.item is BackpackItem))
            remaining = AddToInventoryGetRemainder(BackpackInventory, stack.item, remaining);
        int placed = stack.quantity - remaining;
        if (placed > 0) OnItemAdded?.Invoke(stack.item, placed);
        return remaining == 0;
    }

    private static int AddToInventoryGetRemainder(Inventory inv, ItemDefinition item, int quantity)
    {
        if (quantity <= 0) return 0;
        int before = inv.CountItem(item.itemId);
        inv.AddItem(new ItemStack(item, quantity));
        int after  = inv.CountItem(item.itemId);
        return quantity - (after - before);
    }

    /// <summary>
    /// Removes <paramref name="quantity"/> of the item across all inventories.
    /// Returns true if the full amount was removed.
    /// </summary>
    public bool RemoveItem(string itemId, int quantity)
    {
        if (CountAllItem(itemId) < quantity) return false;

        int remaining = quantity;
        remaining -= RemoveFromInventory(Hotbar, itemId, remaining);
        if (remaining <= 0) return true;
        remaining -= RemoveFromInventory(Main, itemId, remaining);
        if (remaining <= 0) return true;
        if (BackpackInventory != null)
            RemoveFromInventory(BackpackInventory, itemId, remaining);
        return true;
    }

    private static int RemoveFromInventory(Inventory inv, string itemId, int quantity)
    {
        int available = Mathf.Min(inv.CountItem(itemId), quantity);
        if (available > 0) inv.RemoveItem(itemId, available);
        return available;
    }

    /// <summary>Total count of an item across all inventories.</summary>
    public int CountAllItem(string itemId)
    {
        int total = Hotbar.CountItem(itemId) + Main.CountItem(itemId);
        if (BackpackInventory != null) total += BackpackInventory.CountItem(itemId);
        return total;
    }

    /// <summary>Returns true if the player has at least one of the given item in any inventory.</summary>
    public bool HasItem(ItemDefinition item)
    {
        if (item == null) return false;
        return CountAllItem(item.itemId) > 0;
    }
}
