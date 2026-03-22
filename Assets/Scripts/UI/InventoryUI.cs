using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The main inventory panel (Tab/I to open). Shows:
///   • 9x3 main grid
///   • Hotbar row
///   • Single "Backpack" worn-slot (only accepts BackpackItem)
///
/// Created and owned by UIManager.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants / layout
    // ---------------------------------------------------------------
    private const float SlotSize    = 50f;
    private const float SlotSpacing = 4f;
    private const float PanelPadding = 10f;
    private const float SectionGap   = 8f;
    private const int   Columns     = 9;

    // ---------------------------------------------------------------
    //  References
    // ---------------------------------------------------------------
    private PlayerInventory _playerInventory;
    private RectTransform   _panelRoot;

    // Slot lists per section
    private readonly List<SlotUI> _hotbarSlots  = new List<SlotUI>();
    private readonly List<SlotUI> _mainSlots    = new List<SlotUI>();
    private SlotUI _wornBackpackSlot;

    // Section roots
    private GameObject _mainSection;
    private GameObject _hotbarSection;
    private GameObject _wornSection;

    // ---------------------------------------------------------------
    //  Init
    // ---------------------------------------------------------------

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
        BuildPanel();
        PopulateAll();
        playerInventory.OnInventoryChanged += OnInventoryChanged;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_playerInventory != null)
            _playerInventory.OnInventoryChanged -= OnInventoryChanged;
    }

    // ---------------------------------------------------------------
    //  Show / Hide
    // ---------------------------------------------------------------

    public void Show()
    {
        RefreshAll();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        TooltipUI.Hide();
    }

    // ---------------------------------------------------------------
    //  Panel construction
    // ---------------------------------------------------------------

    private void BuildPanel()
    {
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 0.92f);

        _panelRoot = GetComponent<RectTransform>();

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = SectionGap;
        vlg.padding = new RectOffset(
            (int)PanelPadding, (int)PanelPadding,
            (int)PanelPadding, (int)PanelPadding);

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Title label
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(transform, false);
        TMP_Text title = titleGO.GetComponent<TMP_Text>();
        title.text      = "INVENTORY";
        title.color     = new Color(0.8f, 0.8f, 0.8f);
        title.fontSize  = 14f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 22f;
        titleLE.minHeight       = 22f;

        // Main 9x3 grid
        _mainSection   = BuildSection("Main",   _playerInventory.Main,   _mainSlots,   Columns, SlotType.Main);

        // Hotbar row
        _hotbarSection = BuildSection("Hotbar", _playerInventory.Hotbar, _hotbarSlots, Columns, SlotType.Hotbar);

        // Worn backpack slot — distinct background color
        _wornSection = BuildWornBackpackSection();
    }

    private GameObject BuildWornBackpackSection()
    {
        // Section wrapper
        GameObject section = new GameObject("WornBackpackSection", typeof(RectTransform));
        section.transform.SetParent(transform, false);

        VerticalLayoutGroup secVLG = section.AddComponent<VerticalLayoutGroup>();
        secVLG.childControlWidth      = true;
        secVLG.childControlHeight     = true;
        secVLG.childForceExpandWidth  = false;
        secVLG.childForceExpandHeight = false;
        secVLG.spacing = 2f;

        ContentSizeFitter secCSF = section.AddComponent<ContentSizeFitter>();
        secCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        secCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(section.transform, false);
        TMP_Text lbl = labelGO.GetComponent<TMP_Text>();
        lbl.text      = "Backpack (Worn)";
        lbl.color     = new Color(0.45f, 0.75f, 0.45f);
        lbl.fontSize  = 10f;
        lbl.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement lblLE = labelGO.AddComponent<LayoutElement>();
        lblLE.preferredHeight = 16f;
        lblLE.minHeight       = 16f;

        // Single slot
        GameObject slotGO = new GameObject("WornSlot", typeof(RectTransform), typeof(Image));
        slotGO.transform.SetParent(section.transform, false);

        // Give it a distinct green tint background
        slotGO.GetComponent<Image>().color = new Color(0.10f, 0.22f, 0.12f, 0.90f);

        RectTransform slotRT = slotGO.GetComponent<RectTransform>();
        slotRT.anchorMin = new Vector2(0f, 1f);
        slotRT.anchorMax = new Vector2(0f, 1f);
        slotRT.pivot     = new Vector2(0f, 1f);

        LayoutElement slotLE = slotGO.AddComponent<LayoutElement>();
        slotLE.preferredWidth  = SlotSize;
        slotLE.preferredHeight = SlotSize;
        slotLE.minWidth        = SlotSize;
        slotLE.minHeight       = SlotSize;

        _wornBackpackSlot = slotGO.AddComponent<SlotUI>();
        _wornBackpackSlot.Init(_playerInventory.WornSlotInventory, 0);

        // Only BackpackItem is allowed in the worn slot
        _wornBackpackSlot.ValidatePlace = stack =>
            !stack.IsEmpty && stack.item is BackpackItem;

        _wornBackpackSlot.OnShiftClick += HandleShiftClick;

        return section;
    }

    private GameObject BuildSection(string label, Inventory inventory, List<SlotUI> slotList,
                                    int cols, SlotType slotType)
    {
        if (inventory == null) return null;

        int rows          = inventory.Height;
        int effectiveCols = Mathf.Min(inventory.Width, cols);

        GameObject section = new GameObject(label + "Section", typeof(RectTransform));
        section.transform.SetParent(transform, false);

        VerticalLayoutGroup secVLG = section.AddComponent<VerticalLayoutGroup>();
        secVLG.childControlWidth      = true;
        secVLG.childControlHeight     = true;
        secVLG.childForceExpandWidth  = false;
        secVLG.childForceExpandHeight = false;
        secVLG.spacing = 2f;

        ContentSizeFitter secCSF = section.AddComponent<ContentSizeFitter>();
        secCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        secCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(section.transform, false);
        TMP_Text lbl = labelGO.GetComponent<TMP_Text>();
        lbl.text      = label;
        lbl.color     = new Color(0.55f, 0.55f, 0.55f);
        lbl.fontSize  = 10f;
        lbl.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement lblLE = labelGO.AddComponent<LayoutElement>();
        lblLE.preferredHeight = 16f;
        lblLE.minHeight       = 16f;

        // Grid container
        float gridW = effectiveCols * SlotSize + (effectiveCols - 1) * SlotSpacing;
        float gridH = rows          * SlotSize + (rows          - 1) * SlotSpacing;

        GameObject gridGO = new GameObject("Grid", typeof(RectTransform));
        gridGO.transform.SetParent(section.transform, false);

        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 1f);
        gridRT.anchorMax = new Vector2(0f, 1f);
        gridRT.pivot     = new Vector2(0f, 1f);

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(SlotSize, SlotSize);
        glg.spacing         = new Vector2(SlotSpacing, SlotSpacing);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = effectiveCols;

        ContentSizeFitter gridCSF = gridGO.AddComponent<ContentSizeFitter>();
        gridCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement gridLE = gridGO.AddComponent<LayoutElement>();
        gridLE.preferredWidth  = gridW;
        gridLE.preferredHeight = gridH;
        gridLE.minWidth        = gridW;
        gridLE.minHeight       = gridH;

        // Slots
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            GameObject slotGO = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(gridGO.transform, false);

            SlotUI slot = slotGO.AddComponent<SlotUI>();
            slot.Init(inventory, i);
            slot.OnShiftClick += HandleShiftClick;

            // Reject BackpackItem inside a worn backpack's contents
            if (slotType == SlotType.BackpackContents)
            {
                slot.ValidatePlace = stack =>
                    stack.IsEmpty || !(stack.item is BackpackItem);
            }

            slotList.Add(slot);
        }

        return section;
    }

    // ---------------------------------------------------------------
    //  Refresh
    // ---------------------------------------------------------------

    private void OnInventoryChanged()
    {
        if (gameObject.activeInHierarchy) RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (SlotUI s in _hotbarSlots)  s.Refresh();
        foreach (SlotUI s in _mainSlots)    s.Refresh();
        _wornBackpackSlot?.Refresh();
    }

    private void PopulateAll()
    {
        foreach (SlotUI s in _hotbarSlots)  s.Refresh();
        foreach (SlotUI s in _mainSlots)    s.Refresh();
        _wornBackpackSlot?.Refresh();
    }

    // ---------------------------------------------------------------
    //  Shift-click quick move
    // ---------------------------------------------------------------

    private void HandleShiftClick(SlotUI slot, ItemStack stack)
    {
        if (stack.IsEmpty) return;

        bool isHotbar = _hotbarSection != null && slot.transform.IsChildOf(_hotbarSection.transform);
        bool isMain   = _mainSection   != null && slot.transform.IsChildOf(_mainSection.transform);
        bool isWorn   = slot == _wornBackpackSlot;

        Inventory src;
        if (isHotbar) src = _playerInventory.Hotbar;
        else if (isMain) src = _playerInventory.Main;
        else if (isWorn) src = _playerInventory.WornSlotInventory;
        else return;

        int srcIdx = GetSlotIndex(slot, src);
        if (srcIdx < 0) return;

        // BackpackItem from Main/Hotbar → try worn slot (if empty)
        if (stack.item is BackpackItem && !isWorn)
        {
            ItemStack wornCurrent = _playerInventory.WornSlotInventory.GetSlot(0);
            if (wornCurrent.IsEmpty)
            {
                _playerInventory.WornSlotInventory.SetSlot(0, new ItemStack(stack.item, 1));
                int newQty = stack.quantity - 1;
                src.SetSlot(srcIdx, newQty > 0 ? new ItemStack(stack.item, newQty) : default);
                RefreshAll();
                return;
            }
        }

        // Normal shift-click between main/hotbar
        Inventory target;
        if (src == _playerInventory.Hotbar) target = _playerInventory.Main;
        else if (src == _playerInventory.Main) target = _playerInventory.Hotbar;
        else target = _playerInventory.Hotbar; // from worn slot → hotbar

        ItemStack remaining = stack;
        bool moved = TryMoveToInventory(src, srcIdx, target, ref remaining);
        if (!remaining.IsEmpty && (moved || remaining.quantity < stack.quantity))
            src.SetSlot(srcIdx, remaining);

        RefreshAll();
    }

    private bool TryMoveToInventory(Inventory src, int srcIdx, Inventory target, ref ItemStack stack)
    {
        int startQty = stack.quantity;

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

        for (int i = 0; i < target.SlotCount && stack.quantity > 0; i++)
        {
            if (!target.GetSlot(i).IsEmpty) continue;
            int move = Mathf.Min(stack.item.maxStackSize, stack.quantity);
            target.SetSlot(i, new ItemStack(stack.item, move));
            stack = new ItemStack(stack.item, stack.quantity - move);
        }

        if (stack.IsEmpty)
        {
            src.SetSlot(srcIdx, default);
            return true;
        }

        if (stack.quantity < startQty)
            src.SetSlot(srcIdx, stack);

        return false;
    }

    private int GetSlotIndex(SlotUI slot, Inventory inventory)
    {
        if (inventory == _playerInventory.WornSlotInventory) return 0;
        List<SlotUI> list = (inventory == _playerInventory.Hotbar) ? _hotbarSlots : _mainSlots;
        return list.IndexOf(slot);
    }
}
