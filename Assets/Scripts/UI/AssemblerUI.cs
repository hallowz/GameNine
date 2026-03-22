using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Voidborne.Automation;

namespace Voidborne.UI
{
    /// <summary>
    /// UI panel for the Assembler machine.
    ///
    /// Shows:
    ///   • Schematic Card slot — insert/remove SchematicCards to set the active recipe.
    ///   • Input buffer display — up to 9 ingredient entries (read-only; belt-fed).
    ///   • Output buffer — up to 3 clickable output slots; player can take items.
    ///   • Craft progress bar.
    ///   • Power status and items-per-minute counter.
    ///
    /// Created by UIManager.BuildAssemblerPanel(). Call Open(assembler) / Close().
    /// </summary>
    public class AssemblerUI : MonoBehaviour
    {
        // ── Layout constants ────────────────────────────────────────────────
        private const float PanelW       = 360f;
        private const float PanelH       = 340f;
        private const float PanelPad     = 12f;
        private const float SlotSize     = 46f;
        private const float SlotPad      = 4f;

        // ── State ───────────────────────────────────────────────────────────
        private Assembler _assembler;

        // ── Visual references ───────────────────────────────────────────────
        private AssemblerSlotButton   _schematicSlotButton;
        private List<AssemblerSlotButton> _outputSlotButtons = new List<AssemblerSlotButton>();
        private RectTransform         _progressFillRT;
        private TMP_Text              _recipeName;
        private TMP_Text              _statusLabel;
        private TMP_Text              _inputBufferLabel;

        // ── Init ────────────────────────────────────────────────────────────

        public void Init()
        {
            BuildPanel();
            gameObject.SetActive(false);
        }

        // ── Open / Close ────────────────────────────────────────────────────

        public void Open(Assembler assembler)
        {
            if (_assembler != null) _assembler.OnStateChanged -= Refresh;

            _assembler = assembler;
            _assembler.OnStateChanged += Refresh;

            Refresh();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_assembler != null) { _assembler.OnStateChanged -= Refresh; _assembler = null; }
            gameObject.SetActive(false);
        }

        // ── Update (progress bar) ───────────────────────────────────────────

        private void Update()
        {
            if (_assembler == null || _progressFillRT == null) return;
            _progressFillRT.anchorMax = new Vector2(_assembler.CraftProgress, 1f);

            // Live items/min update
            if (_statusLabel != null)
                _statusLabel.text = BuildStatusText();
        }

        // ── Refresh ─────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_assembler == null) return;

            // Schematic slot
            _schematicSlotButton?.SetStack(_assembler.RecipeSlot);

            // Recipe name
            if (_recipeName != null)
                _recipeName.text = _assembler.ActiveRecipe != null
                    ? _assembler.ActiveRecipe.recipeName
                    : "(No Schematic)";

            // Input buffer summary
            if (_inputBufferLabel != null)
                _inputBufferLabel.text = BuildInputBufferText();

            // Output slots (up to 3)
            var output = _assembler.OutputBuffer;
            for (int i = 0; i < _outputSlotButtons.Count; i++)
            {
                var stack = i < output.Count ? output[i] : new ItemStack(null, 0);
                _outputSlotButtons[i].SetStack(stack);
            }

            // Progress bar
            if (_progressFillRT != null)
                _progressFillRT.anchorMax = new Vector2(_assembler.CraftProgress, 1f);

            if (_statusLabel != null) _statusLabel.text = BuildStatusText();
        }

        private string BuildInputBufferText()
        {
            if (_assembler.ActiveRecipe == null) return "No recipe loaded.";
            var buf = _assembler.InputBuffer;
            if (buf.Count == 0) return "Input buffer empty.";

            var sb = new System.Text.StringBuilder();
            foreach (var kv in buf)
                if (kv.Key != null)
                    sb.AppendLine($"  {kv.Key.displayName}: {kv.Value}");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "Input buffer empty.";
        }

        private string BuildStatusText()
        {
            if (_assembler == null) return "";
            bool powered = _assembler.IsRunning || (_assembler.ActiveRecipe != null);
            return $"Power: {(_assembler.IsRunning ? "RUNNING" : "IDLE")}   {_assembler.ItemsPerMinute}/min";
        }

        // ── Slot interaction ─────────────────────────────────────────────────

        private void HandleSchematicClick(bool rightClick)
        {
            if (_assembler == null || UIManager.Instance == null) return;
            var cursor = UIManager.Instance.Cursor;

            if (cursor.IsHolding)
            {
                // Only accept SchematicCards
                if (!(cursor.HeldStack.item is SchematicCard)) return;

                if (_assembler.RecipeSlot.IsEmpty)
                {
                    // Place card
                    var card = new ItemStack(cursor.HeldStack.item, 1);
                    int leftover = cursor.HeldStack.quantity - 1;
                    _assembler.SetRecipeSlot(card);
                    if (leftover > 0) cursor.HeldStack = new ItemStack(cursor.HeldStack.item, leftover);
                    else cursor.Clear();
                }
                else if (_assembler.RecipeSlot.item == cursor.HeldStack.item)
                {
                    // Already same card — swap
                    var held = cursor.HeldStack;
                    cursor.PickUpFurnace(_assembler.TakeRecipeSlot());
                    _assembler.SetRecipeSlot(new ItemStack(held.item, 1));
                }
                else
                {
                    // Different card — swap
                    var prev = _assembler.TakeRecipeSlot();
                    _assembler.SetRecipeSlot(new ItemStack(cursor.HeldStack.item, 1));
                    cursor.PickUpFurnace(prev);
                }
            }
            else
            {
                // Pick up the schematic card
                if (!_assembler.RecipeSlot.IsEmpty)
                    cursor.PickUpFurnace(_assembler.TakeRecipeSlot());
            }

            Refresh();
        }

        private void HandleOutputClick(int slotIndex, bool rightClick)
        {
            if (_assembler == null || UIManager.Instance == null) return;
            var cursor = UIManager.Instance.Cursor;
            var output = _assembler.OutputBuffer;
            if (slotIndex >= output.Count) return;

            var stack = output[slotIndex];
            if (stack.IsEmpty) return;

            if (!cursor.IsHolding)
            {
                // Pick up all from this output slot
                var extracted = _assembler.TryExtract(stack.item);
                // TryExtract only gives 1 at a time; keep extracting to pick up full stack
                int qty = extracted.quantity;
                while (_assembler.HasItem(stack.item) && qty < stack.item.maxStackSize)
                {
                    var more = _assembler.TryExtract(stack.item);
                    if (more.IsEmpty) break;
                    qty += more.quantity;
                }
                cursor.PickUpFurnace(new ItemStack(stack.item, qty));
            }
            else if (cursor.HeldStack.item == stack.item)
            {
                // Put one from held back into assembler? Not applicable — output-only.
                // Instead, put down into inventory via normal inventory interaction.
            }

            Refresh();
        }

        // ── Panel builder ────────────────────────────────────────────────────

        private void BuildPanel()
        {
            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(PanelW, PanelH);

            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);

            float y = PanelH / 2f - PanelPad - 10f;

            // ── Title ──────────────────────────────────────────────────────
            CreateLabel("ASSEMBLER", new Vector2(0f, y), 16f, FontStyles.Bold, Color.white);
            y -= 22f;

            // ── Recipe name ────────────────────────────────────────────────
            _recipeName = CreateLabel("(No Schematic)", new Vector2(0f, y), 11f,
                                      FontStyles.Normal, new Color(0.7f, 0.7f, 0.5f));
            y -= 18f;

            // ── Schematic card slot ────────────────────────────────────────
            CreateLabel("Schematic Card:", new Vector2(-PanelW / 2f + PanelPad + 30f, y),
                        10f, FontStyles.Normal, Color.gray, TextAlignmentOptions.Left);
            y -= 14f;

            _schematicSlotButton = CreateSlotButton(
                new Vector2(-PanelW / 2f + PanelPad + 30f, y),
                new Color(0.2f, 0.35f, 0.5f, 0.9f));
            _schematicSlotButton.gameObject.name = "SchematicSlot";
            _schematicSlotButton.OnLeftClick  = () => HandleSchematicClick(false);
            _schematicSlotButton.OnRightClick = () => HandleSchematicClick(true);
            y -= SlotSize + SlotPad + 4f;

            // ── Progress bar ───────────────────────────────────────────────
            CreateLabel("Progress:", new Vector2(-PanelW / 2f + PanelPad + 30f, y),
                        10f, FontStyles.Normal, Color.gray, TextAlignmentOptions.Left);
            y -= 14f;

            float barW = PanelW - PanelPad * 2f - 60f;
            CreateProgressBar(new Vector2(0f, y), barW);
            y -= 20f;

            // ── Input buffer label ─────────────────────────────────────────
            CreateLabel("Ingredients:", new Vector2(-PanelW / 2f + PanelPad, y),
                        10f, FontStyles.Normal, Color.gray, TextAlignmentOptions.Left);
            y -= 14f;

            _inputBufferLabel = CreateLabel("", new Vector2(0f, y - 20f), 9f,
                                             FontStyles.Normal, new Color(0.6f, 0.8f, 0.6f));
            (_inputBufferLabel.GetComponent<RectTransform>()).sizeDelta = new Vector2(PanelW - PanelPad * 2f, 60f);
            y -= 80f;

            // ── Output slots ───────────────────────────────────────────────
            CreateLabel("Output:", new Vector2(-PanelW / 2f + PanelPad, y),
                        10f, FontStyles.Normal, Color.gray, TextAlignmentOptions.Left);
            y -= 14f;

            float slotStartX = -(SlotSize + SlotPad);
            for (int i = 0; i < 3; i++)
            {
                float sx = slotStartX + i * (SlotSize + SlotPad);
                int idx = i;
                var btn = CreateSlotButton(new Vector2(sx, y - SlotSize / 2f), new Color(0.2f, 0.25f, 0.2f, 0.9f));
                btn.gameObject.name = $"OutputSlot{i}";
                btn.OnLeftClick  = () => HandleOutputClick(idx, false);
                btn.OnRightClick = () => HandleOutputClick(idx, true);
                _outputSlotButtons.Add(btn);
            }
            y -= SlotSize + 8f;

            // ── Status ─────────────────────────────────────────────────────
            _statusLabel = CreateLabel("Power: IDLE   0/min", new Vector2(0f, y),
                                        10f, FontStyles.Normal, new Color(0.5f, 0.8f, 0.5f));
        }

        // ── Widget helpers ───────────────────────────────────────────────────

        private TMP_Text CreateLabel(string text, Vector2 anchoredPos, float fontSize,
                                      FontStyles style, Color color,
                                      TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Label_" + (text.Length > 0 ? text.Substring(0, Mathf.Min(8, text.Length)) : "Empty"),
                                    typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(PanelW - PanelPad * 2f, 20f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.color     = color;
            tmp.alignment = align;
            return tmp;
        }

        private void CreateProgressBar(Vector2 center, float width)
        {
            // Background
            var bgGO = new GameObject("ProgressBG", typeof(RectTransform));
            bgGO.transform.SetParent(transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = center;
            bgRT.sizeDelta = new Vector2(width, 12f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Fill
            var fillGO = new GameObject("ProgressFill", typeof(RectTransform));
            fillGO.transform.SetParent(bgGO.transform, false);
            _progressFillRT = fillGO.GetComponent<RectTransform>();
            _progressFillRT.anchorMin        = Vector2.zero;
            _progressFillRT.anchorMax        = new Vector2(0f, 1f);
            _progressFillRT.offsetMin        = Vector2.zero;
            _progressFillRT.offsetMax        = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.7f, 1f, 1f);
        }

        private AssemblerSlotButton CreateSlotButton(Vector2 pos, Color bgColor)
        {
            var go = new GameObject("SlotBtn", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(SlotSize, SlotSize);

            var bg = go.AddComponent<Image>();
            bg.color = bgColor;

            var btn = go.AddComponent<AssemblerSlotButton>();
            btn.Init(bgColor);
            return btn;
        }
    }

    // ── Slot button helper ──────────────────────────────────────────────────

    /// <summary>
    /// Clickable slot button used by AssemblerUI for recipe and output slots.
    /// </summary>
    public class AssemblerSlotButton : MonoBehaviour, IPointerClickHandler
    {
        public Action OnLeftClick;
        public Action OnRightClick;

        private Image    _bg;
        private TMP_Text _qtyText;
        private Color    _defaultColor;

        public void Init(Color defaultColor)
        {
            _defaultColor = defaultColor;
            _bg           = GetComponent<Image>();

            // Quantity label
            var qtyGO = new GameObject("Qty", typeof(RectTransform));
            qtyGO.transform.SetParent(transform, false);
            var qrt = qtyGO.GetComponent<RectTransform>();
            qrt.anchorMin        = Vector2.zero;
            qrt.anchorMax        = Vector2.one;
            qrt.offsetMin        = Vector2.zero;
            qrt.offsetMax        = Vector2.zero;
            _qtyText              = qtyGO.AddComponent<TextMeshProUGUI>();
            _qtyText.fontSize    = 9f;
            _qtyText.alignment   = TextAlignmentOptions.BottomRight;
            _qtyText.color       = Color.white;
        }

        public void SetStack(ItemStack stack)
        {
            if (_bg == null) return;
            if (stack.IsEmpty)
            {
                _bg.color  = _defaultColor;
                if (_qtyText != null) _qtyText.text = "";
            }
            else
            {
                _bg.color  = new Color(_defaultColor.r + 0.1f,
                                       _defaultColor.g + 0.1f,
                                       _defaultColor.b + 0.1f, _defaultColor.a);
                if (_qtyText != null)
                    _qtyText.text = stack.quantity > 1 ? stack.quantity.ToString() : "";
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) OnRightClick?.Invoke();
            else                                                  OnLeftClick?.Invoke();
        }
    }
}
