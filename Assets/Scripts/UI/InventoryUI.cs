using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Voidborne.Crafting;
using Voidborne.Player;
using Voidborne.UI;
using Voidborne.UI.Style;

/// <summary>
/// The main player inventory panel (Tab/I to open). V4.3 restyle.
///
/// Layout (top → bottom):
///   • Header strip — "Inventory" left, "[ESC] Close" hint right.
///   • Crafting strip — personal 2×2 input grid + arrow + output slot, right-justified.
///   • Main inventory grid — reads PlayerInventory.Main (currently 5×7).
///   • Hotbar mirror — reads PlayerInventory.Hotbar (currently 5×1) and
///     highlights the currently selected slot.
///
/// V4.3 changes from the previous incarnation:
///   • No worn-backpack slot inside this panel. Backpacks are a side panel
///     (BackpackUI, opened with B or right-click from hotbar). V11 owns the
///     full backpack rework.
///   • Personal crafting is embedded directly in this panel. UIManager no
///     longer opens CraftingUI as a side panel when toggling the inventory;
///     CraftingUI is reserved for crafting stations and furnaces.
///   • Colours, fonts, and slot sizes all read from UIStyle so visual changes
///     are global.
///
/// Cursor lock state is owned by UIManager (unlocks on Show, locks on Hide).
/// Drag/drop is not used — interaction is Minecraft-style click/right-click
/// via SlotUI + InventoryCursor. Shift-click quick-move and right-click split
/// are inherited from SlotUI and are not touched here.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Layout
    // ---------------------------------------------------------------
    private const float SectionGap   = 8f;
    private const float HeaderHeight = 22f;

    // ---------------------------------------------------------------
    //  References
    // ---------------------------------------------------------------
    private PlayerInventory      _playerInventory;
    private PersonalCraftingGrid _personalCraftingGrid;

    private readonly List<SlotUI> _hotbarSlots = new List<SlotUI>();
    private readonly List<SlotUI> _mainSlots   = new List<SlotUI>();

    // Personal craft slots (4 input + 1 output)
    private readonly List<CraftingSlotButton> _craftInputs = new List<CraftingSlotButton>();
    private CraftingOutputSlot _craftOutput;

    // Section roots (used by HandleShiftClick to determine source pool)
    private GameObject _mainSection;
    private GameObject _hotbarSection;

    // Track the hotbar selection so we can repaint when the player changes it
    // while the inventory is open (number keys / scroll wheel).
    private int _lastSelectedHotbarIndex = -1;

    // Hotbar mirror keeps its own listener registration around so we can
    // detach in OnDestroy without double-unsubscribing.
    private bool _subscribedToInventory;
    private bool _subscribedToGrid;

    // ---------------------------------------------------------------
    //  Init
    // ---------------------------------------------------------------

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
        EnsureBuilt();
        PopulateAll();

        if (!_subscribedToInventory)
        {
            playerInventory.OnInventoryChanged += OnInventoryChanged;
            _subscribedToInventory = true;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Idempotent build hook. Tests that AddComponent this MonoBehaviour can
    /// drive it directly without triggering Awake / Init.
    /// </summary>
    public void EnsureBuilt()
    {
        if (_panelBuilt) return;
        BuildPanel();
        _panelBuilt = true;
    }

    private bool _panelBuilt;

    /// <summary>
    /// Late-binding seam: UIManager calls this once the PersonalCraftingGrid
    /// component has been located on the player so the embedded crafting
    /// section is connected to the canonical model.
    /// </summary>
    public void BindPersonalCraftingGrid(PersonalCraftingGrid grid)
    {
        if (_personalCraftingGrid == grid) return;

        if (_personalCraftingGrid != null && _subscribedToGrid)
        {
            _personalCraftingGrid.OnGridChanged -= OnPersonalGridChanged;
            _subscribedToGrid = false;
        }

        _personalCraftingGrid = grid;

        if (_personalCraftingGrid != null)
        {
            _personalCraftingGrid.OnGridChanged += OnPersonalGridChanged;
            _subscribedToGrid = true;
            RefreshCraftSlots();
            RefreshCraftOutput();
        }
        else
        {
            // No grid bound — empty the craft visuals defensively.
            foreach (CraftingSlotButton btn in _craftInputs)
                btn.SetStack(default);
            _craftOutput?.SetStack(default);
        }
    }

    private void OnDestroy()
    {
        if (_playerInventory != null && _subscribedToInventory)
            _playerInventory.OnInventoryChanged -= OnInventoryChanged;
        if (_personalCraftingGrid != null && _subscribedToGrid)
            _personalCraftingGrid.OnGridChanged -= OnPersonalGridChanged;
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
    //  Unity update — keep hotbar mirror selection in sync
    // ---------------------------------------------------------------

    private void Update()
    {
        if (_playerInventory == null) return;
        if (!gameObject.activeInHierarchy) return;
        UpdateHotbarSelection(_playerInventory.SelectedHotbarIndex);
    }

    private void UpdateHotbarSelection(int selectedIndex)
    {
        if (selectedIndex == _lastSelectedHotbarIndex) return;

        if (_lastSelectedHotbarIndex >= 0 && _lastSelectedHotbarIndex < _hotbarSlots.Count)
            _hotbarSlots[_lastSelectedHotbarIndex].SetSelected(false);
        if (selectedIndex >= 0 && selectedIndex < _hotbarSlots.Count)
            _hotbarSlots[selectedIndex].SetSelected(true);

        _lastSelectedHotbarIndex = selectedIndex;
    }

    // ---------------------------------------------------------------
    //  Panel construction
    // ---------------------------------------------------------------

    private void BuildPanel()
    {
        // Outer panel: UIStyle.Panel fill + 1px UIStyle.Border frame.
        Image bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = UIStyle.Panel;
        bg.raycastTarget = true;

        // The Border helper adds an Image as the first sibling sized to the
        // full rect. Because the bg image is on the same GO it draws above
        // the border child, so we draw the border as a sibling overlay.
        UIBuilder.Border(gameObject.GetComponent<RectTransform>(), UIStyle.Border);

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = SectionGap;
        vlg.padding = new RectOffset(
            UIStyle.PanelPadding, UIStyle.PanelPadding,
            UIStyle.PanelPadding, UIStyle.PanelPadding);

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        BuildHeader();
        BuildPersonalCraftSection();
        _mainSection   = BuildInventorySection("MainSection",  "Inventory", _playerInventory.Main,   _mainSlots);
        _hotbarSection = BuildInventorySection("HotbarMirror", "Hotbar",    _playerInventory.Hotbar, _hotbarSlots);
    }

    private void BuildHeader()
    {
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(transform, false);

        Image headerBg = header.GetComponent<Image>();
        headerBg.color = UIStyle.PanelLight;
        headerBg.raycastTarget = false;

        LayoutElement hdrLE = header.AddComponent<LayoutElement>();
        hdrLE.preferredHeight = HeaderHeight + 8f;
        hdrLE.minHeight       = HeaderHeight + 8f;

        // Title (left)
        TextMeshProUGUI title = UIBuilder.Text(
            header.transform, "Inventory",
            UIStyle.FontSizeLabel, UIStyle.Text, "HeaderTitle");
        title.alignment = TextAlignmentOptions.Left;
        title.fontStyle = FontStyles.Bold;
        RectTransform tRT = title.rectTransform;
        tRT.anchorMin        = new Vector2(0f, 0f);
        tRT.anchorMax        = new Vector2(0.5f, 1f);
        tRT.offsetMin        = new Vector2(8f, 0f);
        tRT.offsetMax        = new Vector2(0f, 0f);

        // Close hint (right)
        TextMeshProUGUI hint = UIBuilder.Text(
            header.transform, "[ESC] Close",
            UIStyle.FontSizeBody, UIStyle.TextDim, "HeaderHint");
        hint.alignment = TextAlignmentOptions.Right;
        RectTransform hRT = hint.rectTransform;
        hRT.anchorMin = new Vector2(0.5f, 0f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, 0f);
        hRT.offsetMax = new Vector2(-8f, 0f);
    }

    /// <summary>
    /// Builds the personal 2×2 craft grid plus the output slot to the right.
    /// Bound at runtime via BindPersonalCraftingGrid.
    /// </summary>
    private void BuildPersonalCraftSection()
    {
        const int CraftCols = 2;
        const int CraftRows = 2;
        float slotSize    = UIStyle.SlotSize;
        float slotGap     = UIStyle.SlotGap;
        float gridWidth   = CraftCols * slotSize + (CraftCols - 1) * slotGap;
        float gridHeight  = CraftRows * slotSize + (CraftRows - 1) * slotGap;
        float arrowWidth  = 24f;
        float sectionPad  = 8f;
        float sectionWidth  = gridWidth + arrowWidth + slotSize + sectionPad * 2f;
        float sectionHeight = Mathf.Max(gridHeight, slotSize) + 24f; // + label

        GameObject section = new GameObject("PersonalCraftSection", typeof(RectTransform));
        section.transform.SetParent(transform, false);

        LayoutElement secLE = section.AddComponent<LayoutElement>();
        secLE.preferredHeight = sectionHeight;
        secLE.minHeight       = sectionHeight;

        RectTransform secRT = section.GetComponent<RectTransform>();

        // Section label (top-left)
        TextMeshProUGUI label = UIBuilder.Text(
            section.transform, "Crafting",
            UIStyle.FontSizeSmall, UIStyle.TextDim, "CraftLabel");
        label.alignment = TextAlignmentOptions.Left;
        RectTransform lblRT = label.rectTransform;
        lblRT.anchorMin = new Vector2(1f, 1f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.pivot     = new Vector2(1f, 1f);
        lblRT.sizeDelta = new Vector2(120f, 16f);
        lblRT.anchoredPosition = new Vector2(-(sectionPad), 0f);

        // Right-justified container holding inputs / arrow / output
        GameObject rightBlock = new GameObject("RightBlock", typeof(RectTransform));
        rightBlock.transform.SetParent(section.transform, false);
        RectTransform rightRT = rightBlock.GetComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(1f, 0f);
        rightRT.anchorMax = new Vector2(1f, 0f);
        rightRT.pivot     = new Vector2(1f, 0f);
        rightRT.sizeDelta = new Vector2(gridWidth + arrowWidth + slotSize, Mathf.Max(gridHeight, slotSize));
        rightRT.anchoredPosition = new Vector2(-sectionPad, 0f);

        // ---- Input grid (left of right-block) ----
        GameObject gridGO = new GameObject("CraftInputGrid", typeof(RectTransform));
        gridGO.transform.SetParent(rightBlock.transform, false);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 0.5f);
        gridRT.anchorMax = new Vector2(0f, 0.5f);
        gridRT.pivot     = new Vector2(0f, 0.5f);
        gridRT.sizeDelta = new Vector2(gridWidth, gridHeight);
        gridRT.anchoredPosition = Vector2.zero;

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(slotSize, slotSize);
        glg.spacing         = new Vector2(slotGap, slotGap);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = CraftCols;

        for (int i = 0; i < CraftCols * CraftRows; i++)
        {
            int sx = i % CraftCols;
            int sy = i / CraftCols;

            GameObject slotGO = new GameObject($"CraftSlot_{sx}_{sy}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(gridGO.transform, false);

            CraftingSlotButton slot = slotGO.AddComponent<CraftingSlotButton>();
            slot.Init(sx, sy,
                onLeftClick:  (x, y) => HandleCraftSlotLeftClick(x, y),
                onRightClick: (x, y) => HandleCraftSlotRightClick(x, y));
            _craftInputs.Add(slot);
        }

        // ---- Arrow ----
        TextMeshProUGUI arrow = UIBuilder.Text(
            rightBlock.transform, "->",
            UIStyle.FontSizeHeader, UIStyle.TextDim, "CraftArrow");
        arrow.alignment = TextAlignmentOptions.Center;
        RectTransform arrowRT = arrow.rectTransform;
        arrowRT.anchorMin = new Vector2(0f, 0.5f);
        arrowRT.anchorMax = new Vector2(0f, 0.5f);
        arrowRT.pivot     = new Vector2(0f, 0.5f);
        arrowRT.sizeDelta = new Vector2(arrowWidth, slotSize);
        arrowRT.anchoredPosition = new Vector2(gridWidth, 0f);

        // ---- Output slot ----
        GameObject outGO = new GameObject("CraftOutput", typeof(RectTransform), typeof(Image));
        outGO.transform.SetParent(rightBlock.transform, false);
        RectTransform outRT = outGO.GetComponent<RectTransform>();
        outRT.anchorMin = new Vector2(0f, 0.5f);
        outRT.anchorMax = new Vector2(0f, 0.5f);
        outRT.pivot     = new Vector2(0f, 0.5f);
        outRT.sizeDelta = new Vector2(slotSize, slotSize);
        outRT.anchoredPosition = new Vector2(gridWidth + arrowWidth, 0f);

        _craftOutput = outGO.AddComponent<CraftingOutputSlot>();
        _craftOutput.Init(OnClickCraftOutput);
    }

    /// <summary>
    /// Builds a labelled inventory grid (used for both Main and Hotbar mirror).
    /// </summary>
    private GameObject BuildInventorySection(string sectionName, string labelText,
                                             Inventory inventory, List<SlotUI> slotList)
    {
        if (inventory == null) return null;

        int rows = inventory.Height;
        int cols = inventory.Width;

        float slotSize = UIStyle.SlotSize;
        float slotGap  = UIStyle.SlotGap;
        float gridW = cols * slotSize + (cols - 1) * slotGap;
        float gridH = rows * slotSize + (rows - 1) * slotGap;
        float labelH = 18f;
        float sectionH = gridH + labelH + 4f;

        GameObject section = new GameObject(sectionName, typeof(RectTransform));
        section.transform.SetParent(transform, false);

        LayoutElement secLE = section.AddComponent<LayoutElement>();
        secLE.preferredHeight = sectionH;
        secLE.minHeight       = sectionH;
        secLE.preferredWidth  = gridW;
        secLE.minWidth        = gridW;

        // Label (top-left)
        TextMeshProUGUI label = UIBuilder.Text(
            section.transform, labelText,
            UIStyle.FontSizeSmall, UIStyle.TextDim, sectionName + "_Label");
        label.alignment = TextAlignmentOptions.Left;
        RectTransform lblRT = label.rectTransform;
        lblRT.anchorMin = new Vector2(0f, 1f);
        lblRT.anchorMax = new Vector2(0f, 1f);
        lblRT.pivot     = new Vector2(0f, 1f);
        lblRT.sizeDelta = new Vector2(120f, labelH);
        lblRT.anchoredPosition = Vector2.zero;

        // Grid
        GameObject gridGO = new GameObject("Grid", typeof(RectTransform));
        gridGO.transform.SetParent(section.transform, false);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 0f);
        gridRT.anchorMax = new Vector2(0f, 0f);
        gridRT.pivot     = new Vector2(0f, 0f);
        gridRT.sizeDelta = new Vector2(gridW, gridH);
        gridRT.anchoredPosition = Vector2.zero;

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(slotSize, slotSize);
        glg.spacing         = new Vector2(slotGap, slotGap);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            GameObject slotGO = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(gridGO.transform, false);

            SlotUI slot = slotGO.AddComponent<SlotUI>();
            slot.Init(inventory, i);
            slot.OnShiftClick += HandleShiftClick;

            // Per V4.2 spec: the slot itself never accepts BackpackItems in the
            // inventory-contents-of-a-backpack case. The player inventory
            // panel does not have backpack-content sections, so no
            // ValidatePlace override is needed here.

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

    private void OnPersonalGridChanged()
    {
        if (gameObject.activeInHierarchy)
        {
            RefreshCraftSlots();
            RefreshCraftOutput();
        }
    }

    private void RefreshAll()
    {
        foreach (SlotUI s in _hotbarSlots) s.Refresh();
        foreach (SlotUI s in _mainSlots)   s.Refresh();
        RefreshCraftSlots();
        RefreshCraftOutput();

        // Force a redraw of the selected hotbar highlight.
        if (_playerInventory != null)
        {
            _lastSelectedHotbarIndex = -1; // force diff
            UpdateHotbarSelection(_playerInventory.SelectedHotbarIndex);
        }
    }

    private void PopulateAll() => RefreshAll();

    private void RefreshCraftSlots()
    {
        if (_personalCraftingGrid == null) return;
        CraftingGrid grid = _personalCraftingGrid.Grid;
        if (grid == null) return;

        foreach (CraftingSlotButton btn in _craftInputs)
            btn.SetStack(grid.GetSlot(btn.SlotX, btn.SlotY));
    }

    private void RefreshCraftOutput()
    {
        if (_craftOutput == null) return;
        if (_personalCraftingGrid == null)
        {
            _craftOutput.SetStack(default);
            return;
        }
        _personalCraftingGrid.UpdateResult();
        _craftOutput.SetStack(_personalCraftingGrid.CurrentResult);
    }

    // ---------------------------------------------------------------
    //  Personal craft interaction (mirrors CraftingUI's slot logic)
    // ---------------------------------------------------------------

    private void HandleCraftSlotLeftClick(int x, int y)
    {
        if (_personalCraftingGrid == null) return;
        if (UIManager.Instance == null) return;

        CraftingGrid grid = _personalCraftingGrid.Grid;
        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack slotStack = grid.GetSlot(x, y);

        if (cursor.IsHolding)
        {
            if (slotStack.IsEmpty)
            {
                _personalCraftingGrid.SetSlot(x, y, cursor.HeldStack);
                cursor.Clear();
            }
            else if (slotStack.item == cursor.HeldStack.item)
            {
                int space = slotStack.item.maxStackSize - slotStack.quantity;
                int move  = Mathf.Min(space, cursor.HeldStack.quantity);
                if (move > 0)
                {
                    _personalCraftingGrid.SetSlot(x, y,
                        new ItemStack(slotStack.item, slotStack.quantity + move));
                    int leftover = cursor.HeldStack.quantity - move;
                    if (leftover > 0)
                        cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                    else
                        cursor.Clear();
                }
                else
                {
                    ItemStack prev = slotStack;
                    _personalCraftingGrid.SetSlot(x, y, cursor.HeldStack);
                    cursor.PickUp(prev, grid, x, y);
                }
            }
            else
            {
                ItemStack prev = slotStack;
                _personalCraftingGrid.SetSlot(x, y, cursor.HeldStack);
                cursor.PickUp(prev, grid, x, y);
            }
        }
        else
        {
            if (!slotStack.IsEmpty)
            {
                cursor.PickUp(slotStack, grid, x, y);
                _personalCraftingGrid.SetSlot(x, y, default);
            }
        }
    }

    private void HandleCraftSlotRightClick(int x, int y)
    {
        if (_personalCraftingGrid == null) return;
        if (UIManager.Instance == null) return;

        CraftingGrid grid = _personalCraftingGrid.Grid;
        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack slotStack = grid.GetSlot(x, y);

        if (cursor.IsHolding)
        {
            if (slotStack.IsEmpty || slotStack.item == cursor.HeldStack.item)
            {
                int currentQty = slotStack.IsEmpty ? 0 : slotStack.quantity;
                int maxStack   = cursor.HeldStack.item.maxStackSize;

                if (currentQty < maxStack)
                {
                    _personalCraftingGrid.SetSlot(x, y,
                        new ItemStack(cursor.HeldStack.item, currentQty + 1));
                    int leftover = cursor.HeldStack.quantity - 1;
                    if (leftover > 0)
                        cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                    else
                        cursor.Clear();
                }
            }
        }
        else
        {
            if (!slotStack.IsEmpty)
            {
                int half      = (slotStack.quantity + 1) / 2;
                int remainder = slotStack.quantity - half;

                cursor.PickUp(new ItemStack(slotStack.item, half), grid, x, y);

                if (remainder > 0)
                    _personalCraftingGrid.SetSlot(x, y, new ItemStack(slotStack.item, remainder));
                else
                    _personalCraftingGrid.SetSlot(x, y, default);
            }
        }
    }

    private void OnClickCraftOutput()
    {
        if (_personalCraftingGrid == null) return;
        if (UIManager.Instance == null) return;

        InventoryCursor cursor = UIManager.Instance.Cursor;
        if (cursor.IsHolding) return; // can't take while holding

        ItemStack result = _personalCraftingGrid.TakeResult();
        if (result.IsEmpty) return;

        if (_playerInventory != null)
        {
            // Try hotbar first, then main inventory. If both are full the
            // craft result is silently dropped — matches V2 behaviour and
            // matches CraftingUI's OnTakeResultFromStation path.
            if (!TryAddToInventory(_playerInventory.Hotbar, result))
                TryAddToInventory(_playerInventory.Main, result);
        }

        RefreshCraftSlots();
        RefreshCraftOutput();
    }

    private static bool TryAddToInventory(Inventory inv, ItemStack stack)
    {
        if (stack.IsEmpty || inv == null) return false;

        for (int i = 0; i < inv.SlotCount; i++)
        {
            ItemStack slot = inv.GetSlot(i);
            if (slot.IsEmpty || slot.item != stack.item) continue;
            int space = slot.item.maxStackSize - slot.quantity;
            int move  = Mathf.Min(space, stack.quantity);
            if (move <= 0) continue;
            inv.SetSlot(i, new ItemStack(slot.item, slot.quantity + move));
            return true;
        }
        for (int i = 0; i < inv.SlotCount; i++)
        {
            if (!inv.GetSlot(i).IsEmpty) continue;
            inv.SetSlot(i, new ItemStack(stack.item, stack.quantity));
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------
    //  Shift-click quick move (between main and hotbar only)
    // ---------------------------------------------------------------

    private void HandleShiftClick(SlotUI slot, ItemStack stack)
    {
        if (stack.IsEmpty) return;
        if (_playerInventory == null) return;

        bool isHotbar = _hotbarSection != null && slot.transform.IsChildOf(_hotbarSection.transform);
        bool isMain   = _mainSection   != null && slot.transform.IsChildOf(_mainSection.transform);

        Inventory src;
        if (isHotbar)      src = _playerInventory.Hotbar;
        else if (isMain)   src = _playerInventory.Main;
        else               return;

        int srcIdx = GetSlotIndex(slot, src);
        if (srcIdx < 0) return;

        Inventory target = (src == _playerInventory.Hotbar)
            ? _playerInventory.Main
            : _playerInventory.Hotbar;

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
        List<SlotUI> list = (inventory == _playerInventory.Hotbar) ? _hotbarSlots : _mainSlots;
        return list.IndexOf(slot);
    }

    // ---------------------------------------------------------------
    //  Test introspection
    // ---------------------------------------------------------------

    /// <summary>Slots in the bottom hotbar mirror, top-to-bottom build order.</summary>
    public IReadOnlyList<SlotUI> HotbarMirrorSlots => _hotbarSlots;

    /// <summary>Slots in the main inventory grid, row-major.</summary>
    public IReadOnlyList<SlotUI> MainGridSlots => _mainSlots;

    /// <summary>The 4 input slots of the embedded personal craft grid.</summary>
    public IReadOnlyList<CraftingSlotButton> PersonalCraftInputs => _craftInputs;

    /// <summary>The output slot of the embedded personal craft grid.</summary>
    public CraftingOutputSlot PersonalCraftOutput => _craftOutput;
}
