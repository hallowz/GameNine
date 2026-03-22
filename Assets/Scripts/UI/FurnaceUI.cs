using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Voidborne.Automation;

namespace Voidborne.UI
{
    /// <summary>
    /// UI panel for the FurnaceBlock.
    /// Displays Input, Fuel and Output slots plus a smelting progress bar.
    /// Created at runtime by UIManager. Call Open(furnace) to bind to a furnace, Close() to hide.
    ///
    /// Slot interaction uses the same Minecraft-style cursor pick-up/place pattern
    /// as CraftingUI (left-click to pick up / place full stack, right-click for one item).
    /// </summary>
    public class FurnaceUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        //  Layout constants
        // ------------------------------------------------------------------
        private const float SlotSize    = 50f;
        private const float SlotPad     = 4f;
        private const float PanelPad    = 12f;
        private const float SectionGap  = 10f;

        // ------------------------------------------------------------------
        //  State
        // ------------------------------------------------------------------
        private FurnaceBlock _furnace;

        // ------------------------------------------------------------------
        //  Visual references (built once in BuildPanel)
        // ------------------------------------------------------------------
        private FurnaceSlotButton _inputSlotButton;
        private FurnaceSlotButton _fuelSlotButton;
        private FurnaceSlotButton _outputSlotButton;
        private RectTransform     _progressFillRT;
        private Image             _fireFill;  // fuel indicator

        // ------------------------------------------------------------------
        //  Init (called once by UIManager during canvas construction)
        // ------------------------------------------------------------------

        public void Init()
        {
            BuildPanel();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        //  Open / Close
        // ------------------------------------------------------------------

        public void Open(FurnaceBlock furnace)
        {
            if (_furnace != null)
                _furnace.OnStateChanged -= Refresh;

            _furnace = furnace;
            _furnace.OnStateChanged += Refresh;

            Refresh();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_furnace != null)
            {
                _furnace.OnStateChanged -= Refresh;
                _furnace = null;
            }
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        //  Refresh
        // ------------------------------------------------------------------

        private void Update()
        {
            // Poll SmeltProgress every frame so the bar animates smoothly
            // (OnStateChanged fires for slot changes, but not every frame during smelting)
            if (_furnace != null && _progressFillRT != null)
                _progressFillRT.anchorMax = new Vector2(_furnace.SmeltProgress, 1f);
        }

        private void Refresh()
        {
            if (_furnace == null) return;

            _inputSlotButton.SetStack(_furnace.InputSlot);
            _fuelSlotButton.SetStack(_furnace.FuelSlot);
            _outputSlotButton.SetStack(_furnace.OutputSlot);

            if (_progressFillRT != null)
                _progressFillRT.anchorMax = new Vector2(_furnace.SmeltProgress, 1f);

            if (_fireFill != null)
                _fireFill.color = _furnace.IsBurning
                    ? new Color(1f, 0.5f, 0f, 1f)   // orange-ish when burning
                    : new Color(0.3f, 0.3f, 0.3f, 1f); // grey when cold
        }

        // ------------------------------------------------------------------
        //  Slot interaction helpers (Minecraft-style cursor logic)
        // ------------------------------------------------------------------

        private void HandleSlotClick(FurnaceSlotType slotType, bool rightClick)
        {
            if (_furnace == null) return;
            if (UIManager.Instance == null) return;

            InventoryCursor cursor = UIManager.Instance.Cursor;
            ItemStack slotStack = GetSlotStack(slotType);

            if (rightClick)
            {
                HandleRightClick(cursor, slotType, slotStack);
            }
            else
            {
                HandleLeftClick(cursor, slotType, slotStack);
            }

            Refresh();
        }

        private void HandleLeftClick(InventoryCursor cursor, FurnaceSlotType slotType, ItemStack slotStack)
        {
            if (cursor.IsHolding)
            {
                // Output slot: cannot place items in
                if (slotType == FurnaceSlotType.Output) return;

                if (slotStack.IsEmpty)
                {
                    SetSlotStack(slotType, cursor.HeldStack);
                    cursor.Clear();
                }
                else if (slotStack.item == cursor.HeldStack.item)
                {
                    int space = slotStack.item.maxStackSize - slotStack.quantity;
                    int move  = Mathf.Min(space, cursor.HeldStack.quantity);
                    if (move > 0)
                    {
                        SetSlotStack(slotType, new ItemStack(slotStack.item, slotStack.quantity + move));
                        int leftover = cursor.HeldStack.quantity - move;
                        if (leftover > 0)
                            cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                        else
                            cursor.Clear();
                    }
                    else
                    {
                        // Swap
                        SetSlotStack(slotType, cursor.HeldStack);
                        cursor.PickUpFurnace(slotStack);
                    }
                }
                else
                {
                    // Different items — swap
                    SetSlotStack(slotType, cursor.HeldStack);
                    cursor.PickUpFurnace(slotStack);
                }
            }
            else
            {
                // Pick up from slot
                if (!slotStack.IsEmpty)
                {
                    cursor.PickUpFurnace(slotStack);
                    SetSlotStack(slotType, default);
                }
            }
        }

        private void HandleRightClick(InventoryCursor cursor, FurnaceSlotType slotType, ItemStack slotStack)
        {
            // Output slot: cannot place items in
            if (slotType == FurnaceSlotType.Output) return;

            if (cursor.IsHolding)
            {
                if (slotStack.IsEmpty || slotStack.item == cursor.HeldStack.item)
                {
                    int currentQty = slotStack.IsEmpty ? 0 : slotStack.quantity;
                    int maxStack   = cursor.HeldStack.item.maxStackSize;
                    if (currentQty < maxStack)
                    {
                        SetSlotStack(slotType, new ItemStack(cursor.HeldStack.item, currentQty + 1));
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
                    cursor.PickUpFurnace(new ItemStack(slotStack.item, half));
                    SetSlotStack(slotType, remainder > 0 ? new ItemStack(slotStack.item, remainder) : default);
                }
            }
        }

        private ItemStack GetSlotStack(FurnaceSlotType slotType)
        {
            switch (slotType)
            {
                case FurnaceSlotType.Input:  return _furnace.InputSlot;
                case FurnaceSlotType.Fuel:   return _furnace.FuelSlot;
                case FurnaceSlotType.Output: return _furnace.OutputSlot;
                default: return default;
            }
        }

        private void SetSlotStack(FurnaceSlotType slotType, ItemStack stack)
        {
            switch (slotType)
            {
                case FurnaceSlotType.Input:  _furnace.SetInputSlot(stack);  break;
                case FurnaceSlotType.Fuel:   _furnace.SetFuelSlot(stack);   break;
                case FurnaceSlotType.Output: _furnace.SetOutputSlot(stack); break;
            }
        }

        // ------------------------------------------------------------------
        //  Panel construction
        // ------------------------------------------------------------------

        private void BuildPanel()
        {
            // Dark background
            Image bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            // Vertical layout for stacking title + slots + progress
            VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = SectionGap;
            vlg.padding = new RectOffset((int)PanelPad, (int)PanelPad, (int)PanelPad, (int)PanelPad);

            ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ── Title ──────────────────────────────────────────────────────
            AddLabel("FURNACE", 14f, FontStyles.Bold, new Color(0.8f, 0.8f, 0.8f), 22f);

            // ── Slot row: Input → Fire → Output ───────────────────────────
            BuildSlotRow();

            // ── Progress bar row ───────────────────────────────────────────
            BuildProgressBar();

            // ── Close hint ─────────────────────────────────────────────────
            AddLabel("[Esc] Close", 9f, FontStyles.Normal, new Color(0.4f, 0.4f, 0.4f), 16f);
        }

        private void BuildSlotRow()
        {
            // Horizontal row: [Input Slot] [fire indicator] [Output Slot]
            // Fuel slot sits below Input in a sub-column.

            // We use a HorizontalLayoutGroup for the row.
            GameObject row = new GameObject("SlotRow", typeof(RectTransform));
            row.transform.SetParent(transform, false);

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter rowCSF = row.AddComponent<ContentSizeFitter>();
            rowCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Left column: Input (top) + Fuel (below)
            GameObject leftCol = new GameObject("LeftCol", typeof(RectTransform));
            leftCol.transform.SetParent(row.transform, false);

            VerticalLayoutGroup leftVLG = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVLG.childControlWidth      = true;
            leftVLG.childControlHeight     = true;
            leftVLG.childForceExpandWidth  = false;
            leftVLG.childForceExpandHeight = false;
            leftVLG.spacing = SlotPad;

            ContentSizeFitter leftCSF = leftCol.AddComponent<ContentSizeFitter>();
            leftCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            leftCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Input slot
            _inputSlotButton = BuildSlotButton(leftCol.transform, "Input", FurnaceSlotType.Input,
                new Color(0.15f, 0.25f, 0.15f, 0.9f));

            // Fuel slot
            _fuelSlotButton = BuildSlotButton(leftCol.transform, "Fuel", FurnaceSlotType.Fuel,
                new Color(0.25f, 0.18f, 0.08f, 0.9f));

            // Fire / burn indicator (middle column)
            GameObject fireContainer = new GameObject("FireContainer", typeof(RectTransform));
            fireContainer.transform.SetParent(row.transform, false);
            LayoutElement fireLE = fireContainer.AddComponent<LayoutElement>();
            fireLE.preferredWidth  = 24f;
            fireLE.preferredHeight = SlotSize * 2 + SlotPad;
            fireLE.minWidth        = 24f;
            fireLE.minHeight       = SlotSize * 2 + SlotPad;

            // Background of the fire bar
            Image fireBg = fireContainer.AddComponent<Image>();
            fireBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Flame fill (color changes when burning)
            GameObject fireFillGO = new GameObject("FireFill", typeof(RectTransform), typeof(Image));
            fireFillGO.transform.SetParent(fireContainer.transform, false);
            _fireFill = fireFillGO.GetComponent<Image>();
            _fireFill.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            RectTransform fireFillRT = fireFillGO.GetComponent<RectTransform>();
            fireFillRT.anchorMin = new Vector2(0.1f, 0.1f);
            fireFillRT.anchorMax = new Vector2(0.9f, 0.9f);
            fireFillRT.offsetMin = Vector2.zero;
            fireFillRT.offsetMax = Vector2.zero;

            // Flame label
            GameObject flameLabelGO = new GameObject("FlameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            flameLabelGO.transform.SetParent(fireContainer.transform, false);
            TMP_Text flameTxt = flameLabelGO.GetComponent<TMP_Text>();
            flameTxt.text          = "~";
            flameTxt.fontSize      = 14f;
            flameTxt.color         = new Color(1f, 0.6f, 0f);
            flameTxt.alignment     = TextAlignmentOptions.Center;
            flameTxt.raycastTarget = false;
            RectTransform flameLblRT = flameLabelGO.GetComponent<RectTransform>();
            flameLblRT.anchorMin = Vector2.zero;
            flameLblRT.anchorMax = Vector2.one;
            flameLblRT.offsetMin = Vector2.zero;
            flameLblRT.offsetMax = Vector2.zero;

            // Output slot (right column, vertically centered)
            GameObject rightCol = new GameObject("RightCol", typeof(RectTransform));
            rightCol.transform.SetParent(row.transform, false);

            VerticalLayoutGroup rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVLG.childControlWidth      = true;
            rightVLG.childControlHeight     = true;
            rightVLG.childForceExpandWidth  = false;
            rightVLG.childForceExpandHeight = false;
            rightVLG.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter rightCSF = rightCol.AddComponent<ContentSizeFitter>();
            rightCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rightCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Label "Output" above the output slot
            GameObject outLblGO = new GameObject("OutLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            outLblGO.transform.SetParent(rightCol.transform, false);
            TMP_Text outLbl = outLblGO.GetComponent<TMP_Text>();
            outLbl.text          = "Output";
            outLbl.fontSize      = 9f;
            outLbl.color         = new Color(0.55f, 0.55f, 0.55f);
            outLbl.alignment     = TextAlignmentOptions.Center;
            outLbl.raycastTarget = false;
            LayoutElement outLblLE = outLblGO.AddComponent<LayoutElement>();
            outLblLE.preferredHeight = 14f;
            outLblLE.minHeight       = 14f;

            _outputSlotButton = BuildSlotButton(rightCol.transform, "Output", FurnaceSlotType.Output,
                new Color(0.15f, 0.35f, 0.15f, 0.9f));
        }

        private FurnaceSlotButton BuildSlotButton(
            Transform parent,
            string slotName,
            FurnaceSlotType slotType,
            Color bgColor)
        {
            GameObject go = new GameObject(slotName + "Slot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            // Size via LayoutElement
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = SlotSize;
            le.preferredHeight = SlotSize;
            le.minWidth        = SlotSize;
            le.minHeight       = SlotSize;

            FurnaceSlotButton btn = go.AddComponent<FurnaceSlotButton>();
            btn.Init(slotType, bgColor, (t, right) => HandleSlotClick(t, right));
            return btn;
        }

        private void BuildProgressBar()
        {
            // Container row: label + bar
            GameObject barRow = new GameObject("ProgressRow", typeof(RectTransform));
            barRow.transform.SetParent(transform, false);

            HorizontalLayoutGroup hlg = barRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 6f;

            ContentSizeFitter rowCSF = barRow.AddComponent<ContentSizeFitter>();
            rowCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // "Smelting:" label
            GameObject lbl = new GameObject("ProgressLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(barRow.transform, false);
            TMP_Text lblTxt = lbl.GetComponent<TMP_Text>();
            lblTxt.text          = "Smelting:";
            lblTxt.fontSize      = 10f;
            lblTxt.color         = new Color(0.6f, 0.6f, 0.6f);
            lblTxt.alignment     = TextAlignmentOptions.MidlineLeft;
            lblTxt.raycastTarget = false;
            LayoutElement lblLE = lbl.AddComponent<LayoutElement>();
            lblLE.preferredHeight = 14f;
            lblLE.minHeight       = 14f;
            lblLE.preferredWidth  = 60f;

            // Bar background
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(barRow.transform, false);
            barBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
            LayoutElement barBgLE = barBg.AddComponent<LayoutElement>();
            barBgLE.preferredWidth  = 100f;
            barBgLE.preferredHeight = 14f;
            barBgLE.minWidth        = 100f;
            barBgLE.minHeight       = 14f;

            // Fill (child of bar background) — width driven by anchorMax.x = SmeltProgress
            GameObject fillGO = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(barBg.transform, false);
            Image progressFillImg = fillGO.GetComponent<Image>();
            progressFillImg.color        = new Color(0.9f, 0.6f, 0.1f, 1f); // amber
            progressFillImg.raycastTarget = false;
            _progressFillRT = fillGO.GetComponent<RectTransform>();
            _progressFillRT.anchorMin = Vector2.zero;
            _progressFillRT.anchorMax = new Vector2(0f, 1f); // zero width initially
            _progressFillRT.offsetMin = Vector2.zero;
            _progressFillRT.offsetMax = Vector2.zero;
        }

        private void AddLabel(string text, float fontSize, FontStyles style, Color color, float height)
        {
            GameObject go = new GameObject("Label_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            TMP_Text lbl = go.GetComponent<TMP_Text>();
            lbl.text          = text;
            lbl.fontSize      = fontSize;
            lbl.fontStyle     = style;
            lbl.color         = color;
            lbl.alignment     = TextAlignmentOptions.TopLeft;
            lbl.raycastTarget = false;
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight       = height;
        }
    }

    // -----------------------------------------------------------------------
    //  Slot type enum (internal to the Furnace UI)
    // -----------------------------------------------------------------------
    internal enum FurnaceSlotType { Input, Fuel, Output }

    // -----------------------------------------------------------------------
    //  FurnaceSlotButton — clickable slot for the furnace UI
    // -----------------------------------------------------------------------
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    internal class FurnaceSlotButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color ColorHover = new Color(0.35f, 0.35f, 0.35f, 1f);

        private FurnaceSlotType             _slotType;
        private Color                       _normalColor;
        private Image                       _background;
        private Image                       _itemIcon;
        private TMP_Text                    _stackLabel;
        private ItemStack                   _stack;
        private Action<FurnaceSlotType, bool> _onClick; // (slotType, isRightClick)

        public void Init(FurnaceSlotType slotType, Color normalColor, Action<FurnaceSlotType, bool> onClick)
        {
            _slotType    = slotType;
            _normalColor = normalColor;
            _onClick     = onClick;
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
                _onClick?.Invoke(_slotType, false);
            else if (ev.button == PointerEventData.InputButton.Right)
                _onClick?.Invoke(_slotType, true);
        }

        public void OnPointerEnter(PointerEventData ev)
        {
            if (_background != null) _background.color = ColorHover;
            if (!_stack.IsEmpty) TooltipUI.Show(_stack.item);
        }

        public void OnPointerExit(PointerEventData ev)
        {
            if (_background != null) _background.color = _normalColor;
            TooltipUI.Hide();
        }

        private void BuildVisuals()
        {
            _background       = GetComponent<Image>();
            _background.color = _normalColor;

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

            // Stack count label
            GameObject labelGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(transform, false);
            _stackLabel = labelGO.GetComponent<TMP_Text>();
            _stackLabel.text          = "";
            _stackLabel.color         = Color.white;
            _stackLabel.fontSize      = 10f;
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
}
