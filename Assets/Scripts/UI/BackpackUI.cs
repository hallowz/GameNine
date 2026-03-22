using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel that opens alongside the player inventory when a backpack is right-clicked
/// from the hotbar (or via B for the worn slot).
/// Grid is sized to the backpack's rows × columns.
/// BackpackItems cannot be dropped into this panel.
/// Closing the inventory also closes this panel.
/// </summary>
public class BackpackUI : MonoBehaviour
{
    private const float SlotSize    = 50f;
    private const float SlotSpacing = 4f;
    private const float PanelPadding = 10f;

    private PlayerInventory _playerInventory;
    private BackpackInstance _currentInstance;
    private readonly List<SlotUI> _slots = new List<SlotUI>();
    private GameObject _gridSection;
    private GameObject _nameLabel;

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
    }

    // ---------------------------------------------------------------
    //  Open / Close
    // ---------------------------------------------------------------

    public void Open(BackpackInstance instance)
    {
        if (instance == null) return;

        if (_currentInstance == instance && gameObject.activeSelf)
            return; // already open for this instance

        _currentInstance = instance;
        RebuildGrid();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        _currentInstance = null;
    }

    public bool IsOpen => gameObject.activeSelf;
    public BackpackInstance CurrentInstance => _currentInstance;

    // ---------------------------------------------------------------
    //  Panel building
    // ---------------------------------------------------------------

    private void Awake()
    {
        // Build the static panel chrome (background + layout)
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.10f, 0.08f, 0.92f);

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(
            (int)PanelPadding, (int)PanelPadding,
            (int)PanelPadding, (int)PanelPadding);

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(transform, false);
        TMP_Text title = titleGO.GetComponent<TMP_Text>();
        title.text      = "BACKPACK";
        title.color     = new Color(0.45f, 0.85f, 0.55f);
        title.fontSize  = 14f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 22f;
        titleLE.minHeight       = 22f;

        gameObject.SetActive(false);
    }

    private void RebuildGrid()
    {
        // Destroy old dynamic children if present
        if (_gridSection != null) { Destroy(_gridSection); _gridSection = null; }
        if (_nameLabel   != null) { Destroy(_nameLabel);   _nameLabel   = null; }
        _slots.Clear();

        if (_currentInstance == null) return;

        Inventory inv  = _currentInstance.Inventory;
        int cols       = inv.Width;
        int rows       = inv.Height;
        float gridW    = cols * SlotSize + (cols - 1) * SlotSpacing;
        float gridH    = rows * SlotSize + (rows - 1) * SlotSpacing;

        // Sub-label showing backpack name
        _nameLabel = new GameObject("BackpackName", typeof(RectTransform), typeof(TextMeshProUGUI));
        _nameLabel.transform.SetParent(transform, false);
        TMP_Text nameLabel = _nameLabel.GetComponent<TMP_Text>();
        nameLabel.text      = _currentInstance.Definition != null
                              ? _currentInstance.Definition.displayName
                              : "Backpack";
        nameLabel.color     = new Color(0.65f, 0.65f, 0.65f);
        nameLabel.fontSize  = 10f;
        nameLabel.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement nameLE = _nameLabel.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 16f;
        nameLE.minHeight       = 16f;

        // Grid
        _gridSection = new GameObject("Grid", typeof(RectTransform));
        _gridSection.transform.SetParent(transform, false);

        GridLayoutGroup glg = _gridSection.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(SlotSize, SlotSize);
        glg.spacing         = new Vector2(SlotSpacing, SlotSpacing);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;

        ContentSizeFitter gridCSF = _gridSection.AddComponent<ContentSizeFitter>();
        gridCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement gridLE = _gridSection.AddComponent<LayoutElement>();
        gridLE.preferredWidth  = gridW;
        gridLE.preferredHeight = gridH;
        gridLE.minWidth        = gridW;
        gridLE.minHeight       = gridH;

        for (int i = 0; i < inv.SlotCount; i++)
        {
            GameObject slotGO = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(_gridSection.transform, false);

            SlotUI slot = slotGO.AddComponent<SlotUI>();
            slot.Init(inv, i);

            // Reject BackpackItem being dropped into the backpack panel
            slot.ValidatePlace = stack => stack.IsEmpty || !(stack.item is BackpackItem);
            slot.OnShiftClick += HandleShiftClick;

            _slots.Add(slot);
        }
    }

    // ---------------------------------------------------------------
    //  Shift-click: move items from backpack → player inventory
    // ---------------------------------------------------------------

    private void HandleShiftClick(SlotUI slot, ItemStack stack)
    {
        if (stack.IsEmpty || _playerInventory == null || _currentInstance == null) return;

        Inventory src = _currentInstance.Inventory;
        int srcIdx = _slots.IndexOf(slot);
        if (srcIdx < 0) return;

        ItemStack remaining = stack;

        // Try hotbar first, then main
        TryMoveToInventory(src, srcIdx, _playerInventory.Hotbar, ref remaining);
        if (!remaining.IsEmpty)
            TryMoveToInventory(src, srcIdx, _playerInventory.Main, ref remaining);

        RefreshAll();
    }

    private void TryMoveToInventory(Inventory src, int srcIdx, Inventory target, ref ItemStack stack)
    {
        // Pass 1: fill existing stacks
        for (int i = 0; i < target.SlotCount && stack.quantity > 0; i++)
        {
            ItemStack slot = target.GetSlot(i);
            if (slot.IsEmpty || slot.item != stack.item) continue;
            int space = slot.item.maxStackSize - slot.quantity;
            int move  = Mathf.Min(space, stack.quantity);
            if (move <= 0) continue;
            target.SetSlot(i, new ItemStack(slot.item, slot.quantity + move));
            stack = new ItemStack(stack.item, stack.quantity - move);
        }

        // Pass 2: fill empty slots
        for (int i = 0; i < target.SlotCount && stack.quantity > 0; i++)
        {
            if (!target.GetSlot(i).IsEmpty) continue;
            int move = Mathf.Min(stack.item.maxStackSize, stack.quantity);
            target.SetSlot(i, new ItemStack(stack.item, move));
            stack = new ItemStack(stack.item, stack.quantity - move);
        }

        // Update source
        if (stack.IsEmpty)
            src.SetSlot(srcIdx, default);
        else
            src.SetSlot(srcIdx, stack);
    }

    // ---------------------------------------------------------------
    //  Refresh
    // ---------------------------------------------------------------

    public void RefreshAll()
    {
        foreach (SlotUI s in _slots) s.Refresh();
    }
}
