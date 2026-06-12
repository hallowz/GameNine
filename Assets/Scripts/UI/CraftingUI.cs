using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Voidborne;
using Voidborne.Crafting;
using Voidborne.UI;
using Voidborne.UI.Style;

/// <summary>
/// UI panel for a CraftingStation (or the personal 2×2 grid).
/// Created at runtime by UIManager. Call Open() to bind to a grid, Close() to hide.
///
/// Crafting input slots use Minecraft-style cursor pick-up/place (same as inventory slots).
/// The output slot is click-to-take only; items cannot be placed into it.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Layout constants
    // ---------------------------------------------------------------
    private const float SlotSize    = 50f;
    private const float SlotSpacing = 4f;
    private const float PanelPad    = 12f;
    private const float SectionGap  = 10f;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------
    private CraftingGrid          _grid;
    private Action<ItemStack>     _onTakeResult;
    private CraftingStation       _station;   // null when used for personal grid
    private PlayerInventory       _playerInventory;

    private readonly List<CraftingSlotButton> _inputSlots = new List<CraftingSlotButton>();
    private CraftingOutputSlot               _outputSlot;

    // ---------------------------------------------------------------
    //  Init (called once by UIManager during canvas construction)
    // ---------------------------------------------------------------

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
        BuildPanel();
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  Open / Close
    // ---------------------------------------------------------------

    /// <summary>
    /// Open the crafting UI for the given CraftingStation.
    /// Pass null station to use the panel as a free-standing grid.
    /// </summary>
    public void Open(CraftingStation station, CraftingGrid grid, Action<ItemStack> onTakeResult)
    {
        _station      = station;
        _grid         = grid;
        _onTakeResult = onTakeResult;

        // Rebuild slots if grid dimensions differ from the last open
        RebuildSlots(grid.Width, grid.Height);

        RefreshInputSlots();
        RefreshOutput();

        if (_station != null)
            _station.OnGridChanged += OnGridChanged;

        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (_station != null)
        {
            _station.OnGridChanged -= OnGridChanged;
            _station = null;
        }

        _grid         = null;
        _onTakeResult = null;
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  Event callbacks
    // ---------------------------------------------------------------

    private void OnGridChanged()
    {
        RefreshInputSlots();
        RefreshOutput();
    }

    // ---------------------------------------------------------------
    //  Panel construction (called once in Init)
    // ---------------------------------------------------------------

    // Root references rebuilt in RebuildSlots
    private GameObject _gridContainer;
    private GameObject _outputContainer;
    private TMP_Text   _titleLabel;
    private int        _builtWidth;
    private int        _builtHeight;

    private void BuildPanel()
    {
        // V4.5 restyle — UIStyle.Panel + Border for the standalone CraftingUI
        // side panel. Matches V4.1 kit so palette changes are global.
        Image bg = gameObject.AddComponent<Image>();
        bg.color = UIStyle.Panel;
        UIBuilder.Border(GetComponent<RectTransform>(), UIStyle.Border);

        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = SectionGap;
        vlg.padding = new RectOffset(
            (int)PanelPad, (int)PanelPad, (int)PanelPad, (int)PanelPad);

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(transform, false);
        _titleLabel = titleGO.GetComponent<TMP_Text>();
        _titleLabel.text      = "CRAFTING";
        _titleLabel.font      = UIStyle.Font;
        _titleLabel.color     = UIStyle.Text;
        _titleLabel.fontSize  = UIStyle.FontSizeHeader;
        _titleLabel.fontStyle = FontStyles.Bold;
        _titleLabel.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 22f;
        titleLE.minHeight       = 22f;

        // Grid container placeholder (rebuilt in RebuildSlots)
        _gridContainer = new GameObject("GridContainer", typeof(RectTransform));
        _gridContainer.transform.SetParent(transform, false);

        // Arrow label
        GameObject arrowGO = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGO.transform.SetParent(transform, false);
        TMP_Text arrow = arrowGO.GetComponent<TMP_Text>();
        arrow.text      = "->  Result";
        arrow.font      = UIStyle.Font;
        arrow.color     = UIStyle.TextDim;
        arrow.fontSize  = UIStyle.FontSizeSmall;
        arrow.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement arrowLE = arrowGO.AddComponent<LayoutElement>();
        arrowLE.preferredHeight = 18f;
        arrowLE.minHeight       = 18f;

        // Output container
        _outputContainer = new GameObject("OutputContainer", typeof(RectTransform));
        _outputContainer.transform.SetParent(transform, false);

        LayoutElement outLE = _outputContainer.AddComponent<LayoutElement>();
        outLE.preferredHeight = SlotSize;
        outLE.minHeight       = SlotSize;
        outLE.preferredWidth  = SlotSize;
        outLE.minWidth        = SlotSize;

        // Output slot button
        GameObject outSlotGO = new GameObject("OutputSlot", typeof(RectTransform), typeof(Image));
        outSlotGO.transform.SetParent(_outputContainer.transform, false);
        RectTransform outRT = outSlotGO.GetComponent<RectTransform>();
        outRT.anchorMin = Vector2.zero;
        outRT.anchorMax = Vector2.one;
        outRT.offsetMin = Vector2.zero;
        outRT.offsetMax = Vector2.zero;

        _outputSlot = outSlotGO.AddComponent<CraftingOutputSlot>();
        _outputSlot.Init(OnClickOutput);

        // Close hint
        GameObject hintGO = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGO.transform.SetParent(transform, false);
        TMP_Text hint = hintGO.GetComponent<TMP_Text>();
        hint.text      = "[Esc] Close";
        hint.font      = UIStyle.Font;
        hint.color     = UIStyle.TextDim;
        hint.fontSize  = UIStyle.FontSizeSmall;
        hint.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement hintLE = hintGO.AddComponent<LayoutElement>();
        hintLE.preferredHeight = 16f;
        hintLE.minHeight       = 16f;
    }

    // ---------------------------------------------------------------
    //  Dynamic slot rebuild (grid may be 2×2 or 3×3)
    // ---------------------------------------------------------------

    private void RebuildSlots(int width, int height)
    {
        if (_builtWidth == width && _builtHeight == height && _inputSlots.Count > 0)
            return;

        _inputSlots.Clear();

        // Destroy old children
        foreach (Transform child in _gridContainer.transform)
            Destroy(child.gameObject);

        _builtWidth  = width;
        _builtHeight = height;

        float gridW = width  * SlotSize + (width  - 1) * SlotSpacing;
        float gridH = height * SlotSize + (height - 1) * SlotSpacing;

        // Reuse existing layout components rather than destroy+add (Destroy is deferred)
        GridLayoutGroup glg = _gridContainer.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _gridContainer.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(SlotSize, SlotSize);
        glg.spacing         = new Vector2(SlotSpacing, SlotSpacing);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = width;

        ContentSizeFitter gridCSF = _gridContainer.GetComponent<ContentSizeFitter>();
        if (gridCSF == null) gridCSF = _gridContainer.AddComponent<ContentSizeFitter>();
        gridCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement gridLE = _gridContainer.GetComponent<LayoutElement>();
        if (gridLE == null) gridLE = _gridContainer.AddComponent<LayoutElement>();
        gridLE.preferredWidth  = gridW;
        gridLE.preferredHeight = gridH;
        gridLE.minWidth        = gridW;
        gridLE.minHeight       = gridH;

        int total = width * height;
        for (int i = 0; i < total; i++)
        {
            int sx = i % width;
            int sy = i / width;

            GameObject slotGO = new GameObject($"CraftSlot_{sx}_{sy}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(_gridContainer.transform, false);

            CraftingSlotButton slot = slotGO.AddComponent<CraftingSlotButton>();
            slot.Init(sx, sy,
                onLeftClick:  (x, y) => HandleSlotLeftClick(x, y),
                onRightClick: (x, y) => HandleSlotRightClick(x, y));

            _inputSlots.Add(slot);
        }
    }

    // ---------------------------------------------------------------
    //  Slot interaction — Minecraft-style cursor logic
    // ---------------------------------------------------------------

    /// <summary>Left-click: Minecraft-style pick up / place / swap.</summary>
    private void HandleSlotLeftClick(int x, int y)
    {
        if (_grid == null) return;
        if (UIManager.Instance == null) return;

        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack slotStack = _grid.GetSlot(x, y);

        if (cursor.IsHolding)
        {
            if (slotStack.IsEmpty)
            {
                // Place the full held stack into this empty crafting slot
                SetGridSlot(x, y, cursor.HeldStack);
                cursor.Clear();
            }
            else if (slotStack.item == cursor.HeldStack.item)
            {
                // Same item — fill up this slot, leave remainder on cursor
                int space = slotStack.item.maxStackSize - slotStack.quantity;
                int move  = Mathf.Min(space, cursor.HeldStack.quantity);
                if (move > 0)
                {
                    SetGridSlot(x, y, new ItemStack(slotStack.item, slotStack.quantity + move));
                    int leftover = cursor.HeldStack.quantity - move;
                    if (leftover > 0)
                        cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                    else
                        cursor.Clear();
                }
                else
                {
                    // Slot full — swap
                    ItemStack prev = slotStack;
                    SetGridSlot(x, y, cursor.HeldStack);
                    cursor.PickUp(prev, _grid, x, y);
                }
            }
            else
            {
                // Different item — swap
                ItemStack prev = slotStack;
                SetGridSlot(x, y, cursor.HeldStack);
                cursor.PickUp(prev, _grid, x, y);
            }
        }
        else
        {
            // Cursor empty — pick up the stack from this crafting slot
            if (!slotStack.IsEmpty)
            {
                cursor.PickUp(slotStack, _grid, x, y);
                SetGridSlot(x, y, default);
            }
        }
    }

    /// <summary>Right-click: place one item from cursor, or pick up half the stack.</summary>
    private void HandleSlotRightClick(int x, int y)
    {
        if (_grid == null) return;
        if (UIManager.Instance == null) return;

        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack slotStack = _grid.GetSlot(x, y);

        if (cursor.IsHolding)
        {
            // Place one item into this slot (if empty or same item type)
            if (slotStack.IsEmpty || slotStack.item == cursor.HeldStack.item)
            {
                int currentQty = slotStack.IsEmpty ? 0 : slotStack.quantity;
                int maxStack   = cursor.HeldStack.item.maxStackSize;

                if (currentQty < maxStack)
                {
                    SetGridSlot(x, y, new ItemStack(cursor.HeldStack.item, currentQty + 1));

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
            // Cursor empty — pick up half the stack (rounded up)
            if (!slotStack.IsEmpty)
            {
                int half      = (slotStack.quantity + 1) / 2;
                int remainder = slotStack.quantity - half;

                cursor.PickUp(new ItemStack(slotStack.item, half), _grid, x, y);

                if (remainder > 0)
                    SetGridSlot(x, y, new ItemStack(slotStack.item, remainder));
                else
                    SetGridSlot(x, y, default);
            }
        }
    }

    private void SetGridSlot(int x, int y, ItemStack stack)
    {
        if (_station != null)
        {
            _station.SetSlot(x, y, stack);
        }
        else
        {
            _grid.SetSlot(x, y, stack);
            RefreshInputSlots();
            RefreshOutput();
        }
    }

    private void OnClickOutput()
    {
        if (_onTakeResult == null || _grid == null) return;
        if (UIManager.Instance == null) return;

        // Output slot cannot receive items — only take the crafting result
        // (cursor state does not matter for taking the result; items cannot be placed here)
        InventoryCursor cursor = UIManager.Instance.Cursor;
        if (cursor.IsHolding) return; // don't allow taking while holding something

        // Determine result
        ItemStack result;
        if (_station != null)
        {
            result = _station.TakeResult();
        }
        else
        {
            // Compute result directly from grid
            CraftingRecipe match = CraftingManager.Instance?.FindMatch(_grid);
            if (match == null) return;
            result = match.result;
            ConsumeGridIngredients(match);
        }

        if (result.IsEmpty) return;

        _onTakeResult(result);
        RefreshInputSlots();
        RefreshOutput();
    }

    // ---------------------------------------------------------------
    //  Refresh helpers
    // ---------------------------------------------------------------

    private void RefreshInputSlots()
    {
        if (_grid == null) return;
        foreach (CraftingSlotButton slot in _inputSlots)
        {
            ItemStack stack = _grid.GetSlot(slot.SlotX, slot.SlotY);
            slot.SetStack(stack);
        }
    }

    private void RefreshOutput()
    {
        if (_grid == null)
        {
            _outputSlot.SetStack(default);
            return;
        }

        CraftingRecipe match = CraftingManager.Instance?.FindMatch(_grid);
        ItemStack result = (match != null) ? match.result : default;
        _outputSlot.SetStack(result);

        if (_titleLabel != null && _station != null)
            _titleLabel.text = _station.InteractPrompt.Replace("Press E to craft", "CRAFTING");
    }

    // ---------------------------------------------------------------
    //  Inventory helpers
    // ---------------------------------------------------------------

    private bool TryAddToPlayerInventory(ItemStack stack)
    {
        if (_playerInventory == null || stack.IsEmpty) return false;

        // Try hotbar first, then main
        if (TryAddTo(_playerInventory.Hotbar, stack)) return true;
        if (TryAddTo(_playerInventory.Main,   stack)) return true;
        return false;
    }

    private bool TryAddTo(Inventory inv, ItemStack stack)
    {
        // Top up existing stacks
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
        // Empty slots
        for (int i = 0; i < inv.SlotCount; i++)
        {
            if (!inv.GetSlot(i).IsEmpty) continue;
            inv.SetSlot(i, new ItemStack(stack.item, stack.quantity));
            return true;
        }
        return false;
    }

    // Simple grid ingredient consume (for non-station grids, mirrors PersonalCraftingGrid logic)
    private void ConsumeGridIngredients(CraftingRecipe recipe)
    {
        int gMinX = int.MaxValue, gMinY = int.MaxValue;
        int gMaxX = int.MinValue, gMaxY = int.MinValue;

        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                if (!_grid.GetSlot(x, y).IsEmpty)
                {
                    if (x < gMinX) gMinX = x;
                    if (x > gMaxX) gMaxX = x;
                    if (y < gMinY) gMinY = y;
                    if (y > gMaxY) gMaxY = y;
                }
            }
        }

        if (gMaxX < gMinX) return;

        int bbW = gMaxX - gMinX + 1;
        int bbH = gMaxY - gMinY + 1;

        for (int dy = 0; dy < bbH; dy++)
        {
            for (int dx = 0; dx < bbW; dx++)
            {
                int gx = gMinX + dx;
                int gy = gMinY + dy;

                ItemStack slot = _grid.GetSlot(gx, gy);
                if (slot.IsEmpty) continue;

                int newQty = slot.quantity - 1;
                _grid.SetSlot(gx, gy, newQty > 0 ? new ItemStack(slot.item, newQty) : default);
            }
        }
    }
}

// ---------------------------------------------------------------
//  Helper: a single clickable crafting input slot
// ---------------------------------------------------------------

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class CraftingSlotButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // V4.5 restyle — UIStyle palette. Normal = PanelLight, Hover = Border.
    private static Color ColorNormal => UIStyle.PanelLight;
    private static Color ColorHover  => UIStyle.Border;

    public int SlotX { get; private set; }
    public int SlotY { get; private set; }

    private Image    _background;
    private Image    _itemIcon;
    private TMP_Text _stackLabel;
    private ItemStack _stack;

    private Action<int,int> _onLeftClick;
    private Action<int,int> _onRightClick;

    public void Init(int x, int y, Action<int,int> onLeftClick, Action<int,int> onRightClick)
    {
        SlotX         = x;
        SlotY         = y;
        _onLeftClick  = onLeftClick;
        _onRightClick = onRightClick;
        BuildVisuals();
    }

    public void SetStack(ItemStack stack)
    {
        _stack = stack;
        UpdateVisuals();
    }

    public void OnPointerClick(PointerEventData ev)
    {
        if (ev.button == PointerEventData.InputButton.Left)
            _onLeftClick?.Invoke(SlotX, SlotY);
        else if (ev.button == PointerEventData.InputButton.Right)
            _onRightClick?.Invoke(SlotX, SlotY);
    }

    public void OnPointerEnter(PointerEventData ev)
    {
        _background.color = ColorHover;
        if (!_stack.IsEmpty) TooltipUI.Show(_stack.item);
    }

    public void OnPointerExit(PointerEventData ev)
    {
        _background.color = ColorNormal;
        TooltipUI.Hide();
    }

    private void BuildVisuals()
    {
        _background       = GetComponent<Image>();
        _background.color = ColorNormal;

        // Inner background
        GameObject innerBg = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
        innerBg.transform.SetParent(transform, false);
        innerBg.GetComponent<Image>().color = ColorNormal;
        RectTransform innerRT = innerBg.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(transform, false);
        _itemIcon = iconGO.GetComponent<Image>();
        _itemIcon.preserveAspect = true;
        _itemIcon.raycastTarget  = false;
        _itemIcon.enabled        = false;
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        // Stack label
        GameObject labelGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(transform, false);
        _stackLabel = labelGO.GetComponent<TMP_Text>();
        _stackLabel.text          = "";
        _stackLabel.font          = UIStyle.Font;
        _stackLabel.color         = UIStyle.Text;
        _stackLabel.fontSize      = UIStyle.FontSizeSmall;
        _stackLabel.fontStyle     = FontStyles.Bold;
        _stackLabel.alignment     = TextAlignmentOptions.BottomRight;
        _stackLabel.raycastTarget = false;
        _stackLabel.outlineWidth  = 0.2f;
        _stackLabel.outlineColor  = Color.black;
        _stackLabel.enabled       = false;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(2, 2);
        labelRT.offsetMax = new Vector2(-2, -2);
    }

    private void UpdateVisuals()
    {
        if (_stack.IsEmpty)
        {
            _itemIcon.enabled   = false;
            _stackLabel.enabled = false;
        }
        else
        {
            _itemIcon.sprite  = ItemIconGenerator.GetIcon(_stack.item);
            _itemIcon.color   = Color.white;
            _itemIcon.enabled = true;
            _stackLabel.enabled = true;
            _stackLabel.text  = _stack.quantity > 1 ? _stack.quantity.ToString() : "";
        }
    }
}

// ---------------------------------------------------------------
//  Helper: the output slot button
// ---------------------------------------------------------------

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class CraftingOutputSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // V4.5 restyle — UIStyle palette. Empty = PanelLight, Ready = Accent (dim),
    // Hover = Accent. Matches V4.1 kit so palette changes are global.
    private static Color ColorEmpty => UIStyle.PanelLight;
    private static Color ColorReady => UIStyle.AccentDim;
    private static Color ColorHover => UIStyle.Accent;

    private Image    _background;
    private Image    _itemIcon;
    private TMP_Text _stackLabel;
    private ItemStack _stack;
    private Action   _onClick;

    public void Init(Action onClick)
    {
        _onClick = onClick;
        BuildVisuals();
    }

    public void SetStack(ItemStack stack)
    {
        _stack = stack;
        UpdateVisuals();
    }

    public void OnPointerClick(PointerEventData ev)
    {
        // Only allow left-click to take; ignore right-click and block placing
        if (ev.button == PointerEventData.InputButton.Left && !_stack.IsEmpty)
            _onClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData ev)
    {
        if (!_stack.IsEmpty)
        {
            _background.color = ColorHover;
            TooltipUI.Show(_stack.item);
        }
    }

    public void OnPointerExit(PointerEventData ev)
    {
        _background.color = _stack.IsEmpty ? ColorEmpty : ColorReady;
        TooltipUI.Hide();
    }

    private void BuildVisuals()
    {
        _background       = GetComponent<Image>();
        _background.color = ColorEmpty;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(transform, false);
        _itemIcon = iconGO.GetComponent<Image>();
        _itemIcon.preserveAspect = true;
        _itemIcon.raycastTarget  = false;
        _itemIcon.enabled        = false;
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        GameObject labelGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(transform, false);
        _stackLabel = labelGO.GetComponent<TMP_Text>();
        _stackLabel.text          = "";
        _stackLabel.font          = UIStyle.Font;
        _stackLabel.color         = UIStyle.Text;
        _stackLabel.fontSize      = UIStyle.FontSizeSmall;
        _stackLabel.fontStyle     = FontStyles.Bold;
        _stackLabel.alignment     = TextAlignmentOptions.BottomRight;
        _stackLabel.raycastTarget = false;
        _stackLabel.outlineWidth  = 0.2f;
        _stackLabel.outlineColor  = Color.black;
        _stackLabel.enabled       = false;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(2, 2);
        labelRT.offsetMax = new Vector2(-2, -2);
    }

    private void UpdateVisuals()
    {
        if (_stack.IsEmpty)
        {
            _itemIcon.enabled   = false;
            _stackLabel.enabled = false;
            _background.color   = ColorEmpty;
        }
        else
        {
            _itemIcon.sprite  = ItemIconGenerator.GetIcon(_stack.item);
            _itemIcon.color   = Color.white;
            _itemIcon.enabled = true;
            _stackLabel.enabled = true;
            _stackLabel.text  = _stack.quantity > 1 ? _stack.quantity.ToString() : "";
            _background.color = ColorReady;
        }
    }
}
