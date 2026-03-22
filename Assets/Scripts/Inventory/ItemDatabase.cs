using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Voidborne/Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    // Static singleton — set in OnEnable so it's available after domain reload.
    public static ItemDatabase Instance { get; private set; }

    [Tooltip("All item definitions known to the game. Populate this list in the Inspector.")]
    public List<ItemDefinition> items = new List<ItemDefinition>();

    // Lookup cache built on first use.
    private Dictionary<string, ItemDefinition> _lookup;

    private void OnEnable()
    {
        Instance = this;
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, ItemDefinition>(items.Count);
        foreach (var def in items)
        {
            if (def == null) continue;
            if (string.IsNullOrEmpty(def.itemId)) continue;
            if (_lookup.ContainsKey(def.itemId))
            {
                Debug.LogWarning($"[ItemDatabase] Duplicate item ID '{def.itemId}' — skipping second entry.", this);
                continue;
            }
            _lookup[def.itemId] = def;
        }
    }

    /// <summary>Read-only access to the full item list (used by DevBackpack).</summary>
    public List<ItemDefinition> AllItems => items;

    /// <summary>Returns the ItemDefinition with the given ID, or null if not found.</summary>
    public ItemDefinition GetItem(string id)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(id, out var result);
        return result;
    }

    /// <summary>
    /// Fallback: load the ItemDatabase from Resources if Instance is null.
    /// Place the ItemDatabase asset anywhere inside an Assets/Resources/ folder
    /// and name it "ItemDatabase" for this to work automatically.
    /// </summary>
    public static ItemDatabase GetOrLoad()
    {
        if (Instance != null) return Instance;
        var loaded = Resources.Load<ItemDatabase>("ItemDatabase");
        if (loaded == null)
            Debug.LogError("[ItemDatabase] No ItemDatabase found in Resources. Create one at Assets/Resources/ItemDatabase.asset");
        return loaded;
    }
}
