using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using Voidborne.UI;

/// <summary>
/// Individual inventory/hotbar slot.
/// Handles hover highlighting, tooltip, shift-click, and Minecraft-style
/// left/right click-to-grab/place via UIManager.Cursor (InventoryCursor).
///
/// Drag-and-drop has been removed; all interaction is click-based.
/// InventoryUI / HotbarUI create these at runtime and call Init().
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class SlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler
{
    // ---------------------------------------------------------------
    //  Colours
    // ---------------------------------------------------------------
    private static readonly Color ColorNormal   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    private static readonly Color ColorHover    = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColorSelected = new Color(0.70f, 0.60f, 0.10f, 1.00f); // gold

    // ---------------------------------------------------------------
    //  Slot data
    // ---------------------------------------------------------------
    private Inventory _inventory;
    private int       _slotIndex;

    // ---------------------------------------------------------------
    //  Visual child references
    // ---------------------------------------------------------------
    private Image    _background;
    private Image    _itemIcon;
    private TMP_Text _stackLabel;
    private Image    _selectionBorder;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------
    private bool _isSelected;

    // ---------------------------------------------------------------
    //  Init
    // ---------------------------------------------------------------

    public void Init(Inventory inventory, int slotIndex)
    {
        _inventory  = inventory;
        _slotIndex  = slotIndex;
        BuildVisuals();
        Refresh();
    }

    // ---------------------------------------------------------------
    //  Public helpers
    // ---------------------------------------------------------------

    public void Refresh()
    {
        if (_inventory == null) return;
        ItemStack stack = _inventory.GetSlot(_slotIndex);
        SetVisuals(stack);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_selectionBorder != null)
            _selectionBorder.enabled = selected;
        if (_background != null && !selected)
            _background.color = ColorNormal;
    }

    // ---------------------------------------------------------------
    //  Pointer events
    // ---------------------------------------------------------------

    public void OnPointerEnter(PointerEventData ev)
    {
        if (!_isSelected) _background.color = ColorHover;
        ItemStack stack = _inventory.GetSlot(_slotIndex);
        if (!stack.IsEmpty) TooltipUI.Show(stack.item);
    }

    public void OnPointerExit(PointerEventData ev)
    {
        if (!_isSelected) _background.color = ColorNormal;
        TooltipUI.Hide();
    }

    public void OnPointerClick(PointerEventData ev)
    {
        // Shift+left-click — quick move
        if (ev.button == PointerEventData.InputButton.Left &&
            Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
        {
            OnShiftClick?.Invoke(this, _inventory.GetSlot(_slotIndex));
            return;
        }

        if (ev.button == PointerEventData.InputButton.Left)
        {
            HandleLeftClick();
            return;
        }

        if (ev.button == PointerEventData.InputButton.Right)
        {
            HandleRightClick();
            return;
        }
    }

    /// <summary>Raised on shift-click. InventoryUI/HotbarUI subscribe to implement quick-move.</summary>
    public event System.Action<SlotUI, ItemStack> OnShiftClick;

    /// <summary>
    /// Optional validator called before placing an item into this slot.
    /// Return false to reject the placement with a visual shake.
    /// </summary>
    public System.Func<ItemStack, bool> ValidatePlace;

    // ---------------------------------------------------------------
    //  Minecraft-style click logic
    // ---------------------------------------------------------------

    private System.Collections.IEnumerator ShakeCoroutine()
    {
        Vector3 origin = transform.localPosition;
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            float offset = Mathf.Sin(elapsed * Mathf.PI * 28f) * 5f * (1f - elapsed / 0.25f);
            transform.localPosition = origin + new Vector3(offset, 0f, 0f);
            elapsed += UnityEngine.Time.deltaTime;
            yield return null;
        }
        transform.localPosition = origin;
    }

    private void HandleLeftClick()
    {
        if (UIManager.Instance == null) return;
        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack myStack = _inventory.GetSlot(_slotIndex);

        if (cursor.IsHolding)
        {
            // Validate before placing
            if (ValidatePlace != null && !ValidatePlace(cursor.HeldStack))
            {
                StartCoroutine(ShakeCoroutine());
                return;
            }

            if (myStack.IsEmpty)
            {
                // Place the full held stack into this empty slot
                _inventory.SetSlot(_slotIndex, cursor.HeldStack);
                cursor.Clear();
            }
            else if (myStack.item == cursor.HeldStack.item)
            {
                // Same item — fill up this slot, leave remainder on cursor
                int space = myStack.item.maxStackSize - myStack.quantity;
                int move  = Mathf.Min(space, cursor.HeldStack.quantity);
                if (move > 0)
                {
                    _inventory.SetSlot(_slotIndex, new ItemStack(myStack.item, myStack.quantity + move));
                    int leftover = cursor.HeldStack.quantity - move;
                    if (leftover > 0)
                        cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                    else
                        cursor.Clear();
                }
                else
                {
                    // Slot is full — swap
                    ItemStack prev = myStack;
                    _inventory.SetSlot(_slotIndex, cursor.HeldStack);
                    cursor.PickUp(prev, _inventory, _slotIndex);
                }
            }
            else
            {
                // Different item — swap: cursor goes into slot, slot comes to cursor
                ItemStack prev = myStack;
                _inventory.SetSlot(_slotIndex, cursor.HeldStack);
                cursor.PickUp(prev, _inventory, _slotIndex);
            }
        }
        else
        {
            // Cursor is empty — pick up the stack in this slot
            if (!myStack.IsEmpty)
            {
                cursor.PickUp(myStack, _inventory, _slotIndex);
                _inventory.SetSlot(_slotIndex, default);
            }
        }

        Refresh();
        TooltipUI.Hide();
    }

    private void HandleRightClick()
    {
        if (UIManager.Instance == null) return;
        InventoryCursor cursor = UIManager.Instance.Cursor;
        ItemStack myStack = _inventory.GetSlot(_slotIndex);

        if (cursor.IsHolding)
        {
            // Place one item from cursor into this slot (if empty or same item type)
            if (myStack.IsEmpty || myStack.item == cursor.HeldStack.item)
            {
                int currentQty = myStack.IsEmpty ? 0 : myStack.quantity;
                int maxStack   = cursor.HeldStack.item.maxStackSize;

                if (currentQty < maxStack)
                {
                    _inventory.SetSlot(_slotIndex,
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
            // Cursor empty — pick up half the stack (rounded up)
            if (!myStack.IsEmpty)
            {
                int half      = (myStack.quantity + 1) / 2; // rounded up
                int remainder = myStack.quantity - half;

                cursor.PickUp(new ItemStack(myStack.item, half), _inventory, _slotIndex);

                if (remainder > 0)
                    _inventory.SetSlot(_slotIndex, new ItemStack(myStack.item, remainder));
                else
                    _inventory.SetSlot(_slotIndex, default);
            }
        }

        Refresh();
        TooltipUI.Hide();
    }

    // ---------------------------------------------------------------
    //  Visual setup
    // ---------------------------------------------------------------

    private void BuildVisuals()
    {
        // Size is controlled by the parent GridLayoutGroup — do not override sizeDelta here.

        // Background
        _background = GetComponent<Image>();
        if (_background == null) _background = gameObject.AddComponent<Image>();
        _background.color = ColorNormal;

        // Selection border (gold outline)
        GameObject borderGO = new GameObject("SelectBorder", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(transform, false);
        _selectionBorder = borderGO.GetComponent<Image>();
        _selectionBorder.color   = ColorSelected;
        _selectionBorder.enabled = false;
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin  = Vector2.zero;
        borderRT.anchorMax  = Vector2.one;
        borderRT.offsetMin  = new Vector2(-2, -2);
        borderRT.offsetMax  = new Vector2(2, 2);
        borderGO.transform.SetAsFirstSibling();

        // Inner background (covers the border overflow for clean look)
        GameObject innerBg = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
        innerBg.transform.SetParent(transform, false);
        innerBg.GetComponent<Image>().color = ColorNormal;
        innerBg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        innerBg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        innerBg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        innerBg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // Item icon
        GameObject iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(transform, false);
        _itemIcon = iconGO.GetComponent<Image>();
        _itemIcon.preserveAspect = true;
        _itemIcon.raycastTarget  = false;
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        // Stack count label (bottom-right)
        GameObject labelGO = new GameObject("StackCount", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(transform, false);
        _stackLabel = labelGO.GetComponent<TMP_Text>();
        _stackLabel.text           = "";
        _stackLabel.color          = Color.white;
        _stackLabel.fontSize       = 10f;
        _stackLabel.fontStyle      = FontStyles.Bold;
        _stackLabel.alignment      = TextAlignmentOptions.BottomRight;
        _stackLabel.raycastTarget  = false;
        // Add outline
        _stackLabel.outlineWidth   = 0.2f;
        _stackLabel.outlineColor   = Color.black;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin  = Vector2.zero;
        labelRT.anchorMax  = Vector2.one;
        labelRT.offsetMin  = new Vector2(2, 2);
        labelRT.offsetMax  = new Vector2(-2, -2);
    }

    private void SetVisuals(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            _itemIcon.sprite  = null;
            _itemIcon.enabled = false;
            _stackLabel.text  = "";
            _stackLabel.enabled = false;
        }
        else
        {
            _itemIcon.sprite  = ItemIconGenerator.GetIcon(stack.item);
            _itemIcon.color   = Color.white;
            _itemIcon.enabled = true;
            _stackLabel.enabled = true;
            _stackLabel.text  = stack.quantity > 1 ? stack.quantity.ToString() : "";
        }
    }
}
