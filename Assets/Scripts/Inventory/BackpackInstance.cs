using System.Collections.Generic;

/// <summary>
/// Wraps an Inventory for a specific backpack item instance.
/// Persisted in a static registry so contents survive the item being
/// dropped and picked back up during the same session.
/// </summary>
public class BackpackInstance
{
    // ---------------------------------------------------------------
    //  Static registry
    // ---------------------------------------------------------------
    private static readonly Dictionary<string, BackpackInstance> _registry =
        new Dictionary<string, BackpackInstance>();

    public static BackpackInstance Get(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        _registry.TryGetValue(instanceId, out var inst);
        return inst;
    }

    /// <summary>
    /// Returns the existing instance for <paramref name="instanceId"/> if found,
    /// otherwise creates a fresh one using <paramref name="definition"/>.
    /// </summary>
    public static BackpackInstance GetOrCreate(string instanceId, BackpackItem definition)
    {
        var existing = Get(instanceId);
        if (existing != null) return existing;
        return new BackpackInstance(definition);
    }

    // ---------------------------------------------------------------
    //  Instance state
    // ---------------------------------------------------------------
    public string      InstanceId { get; }
    public BackpackItem Definition { get; }
    public Inventory   Inventory  { get; }

    public BackpackInstance(BackpackItem definition)
    {
        InstanceId = System.Guid.NewGuid().ToString("N");
        Definition = definition;

        if (definition is DevBackpackItem)
        {
            Inventory = CreateDevInventory(definition.extraColumns);
        }
        else
        {
            Inventory = new Inventory(definition.extraColumns, definition.extraRows);
        }

        _registry[InstanceId] = this;
    }

    /// <summary>
    /// Creates an inventory sized to hold every item in the database,
    /// fills each slot, and marks it infinite so items never deplete.
    /// </summary>
    private Inventory CreateDevInventory(int columns)
    {
        ItemDatabase db = ItemDatabase.GetOrLoad();
        int itemCount = 0;
        List<ItemDefinition> validItems = null;

        if (db != null && db.AllItems != null)
        {
            validItems = new List<ItemDefinition>();
            foreach (var itemDef in db.AllItems)
            {
                if (itemDef != null)
                    validItems.Add(itemDef);
            }
            itemCount = validItems.Count;
        }

        // Size the grid to fit all items (minimum 1 row)
        if (columns < 1) columns = 9;
        int rows = itemCount > 0
            ? ((itemCount + columns - 1) / columns)
            : Definition.extraRows;

        var inv = new Inventory(columns, rows);
        inv.IsInfinite = true;

        if (validItems != null)
        {
            for (int i = 0; i < validItems.Count; i++)
                inv.SetSlot(i, new ItemStack(validItems[i], validItems[i].maxStackSize));
        }

        return inv;
    }

    public Inventory GetInventory() => Inventory;

    public void Open()  => UIManager.Instance?.OpenBackpack(this);
    public void Close() => UIManager.Instance?.CloseBackpack();
}
