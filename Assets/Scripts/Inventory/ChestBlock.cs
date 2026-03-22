using System.Collections.Generic;
using UnityEngine;
using Voidborne;
using Voidborne.Building.Electricity;

/// <summary>
/// A placeable chest in the world. Configurable slot grid and optional power requirement.
///
/// Tiers:
///   Wood Chest:        9×3  (27 slots), no power
///   Iron Chest:        9×6  (54 slots), no power
///   Compression Chest: 9×12 (108 slots), 25W, stacks to 4× normal
///
/// When a PowerConsumer is present and unpowered, the chest enters frozen view-only mode:
/// the player can open and see contents but cannot insert or extract.
/// </summary>
public class ChestBlock : MonoBehaviour, IInteractable
{
    [Header("Chest Configuration")]
    [Tooltip("Number of columns in the chest grid.")]
    [SerializeField] private int cols = 9;

    [Tooltip("Number of rows in the chest grid.")]
    [SerializeField] private int rows = 3;

    [Tooltip("Stack size multiplier. 1 = normal, 4 = compression (64→256).")]
    [SerializeField] private int stackMultiplier = 1;

    [Header("Optional Power")]
    [Tooltip("If set, chest requires power to insert/extract. Assign a PowerConsumer on the same GO.")]
    [SerializeField] private PowerConsumer powerConsumer;

    public Inventory ChestInventory { get; private set; }

    [Tooltip("Items pre-loaded into the chest when the scene starts.")]
    [SerializeField] public List<ItemStack> starterItems = new List<ItemStack>();

    /// <summary>Number of columns in this chest.</summary>
    public int Cols => cols;
    /// <summary>Number of rows in this chest.</summary>
    public int Rows => rows;
    /// <summary>Total slot count.</summary>
    public int SlotCount => cols * rows;
    /// <summary>Stack size multiplier (1 = normal, 4 = compression).</summary>
    public int StackMultiplier => stackMultiplier;
    /// <summary>True if this chest requires power and is currently unpowered.</summary>
    public bool IsFrozen => powerConsumer != null && !powerConsumer.IsPowered;

    private void Awake()
    {
        ChestInventory = new Inventory(cols, rows, stackMultiplier);

        if (powerConsumer == null)
            powerConsumer = GetComponent<PowerConsumer>();
    }

    private void Start()
    {
        foreach (var stack in starterItems)
        {
            if (!stack.IsEmpty)
                ChestInventory.AddItem(stack);
        }
    }

    // ── IInteractable ──────────────────────────────────────────────────────────

    public virtual string InteractPrompt
    {
        get
        {
            if (IsFrozen) return "View Chest (No Power)";
            return "Open Chest";
        }
    }

    public bool CanInteract(Vector3 fromPosition)
    {
        return Vector3.Distance(fromPosition, transform.position) <= 3f;
    }

    public void Interact(GameObject interactor)
    {
        if (UIManager.Instance == null) return;

        // Toggle: if already open for this chest, close it
        if (UIManager.Instance.IsChestOpen(this))
            UIManager.Instance.CloseChest();
        else
            UIManager.Instance.OpenChest(this);
    }
}
