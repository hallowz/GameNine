using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.UI.Style;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.4 — generic machine UI panel. Hosts the "howItWorks" prose
    /// surface that tells the player how a machine behaves (forgiving vs.
    /// picky vs. hybrid), the input grid sized from
    /// <see cref="MachineDefinition.gridWidth"/> / <c>gridHeight</c>, a
    /// recipe tab strip (for machines with more than one recipe), an output
    /// slot, an optional fuel slot, a progress bar, and an optional power
    /// gauge.
    ///
    /// MachineUI is client-local. All simulation reads/writes go through an
    /// <see cref="IMachineInputProvider"/>; V9.1 will provide the real
    /// MachineRuntime-backed implementation. EditMode tests use
    /// <see cref="StubMachineInputProvider"/>.
    ///
    /// Coop note: this panel never mutates server-authoritative state
    /// directly. The Try* methods on the provider are the only mutation seam
    /// — the V9 / V21 implementation will route them through a ServerRpc.
    /// </summary>
    public class MachineUI : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Layout constants
        // ---------------------------------------------------------------

        private const float PanelWidth      = 700f;
        private const float PanelHeight     = 500f;
        private const float SectionGap      = 8f;
        private const float HeaderHeight    = 26f;
        private const float ProseHeight     = 92f;   // ~5 lines of body text
        private const float ProseWidth      = 500f;
        private const float OutputSlotScale = 1.2f;
        private const float ProgressHeight  = 12f;
        private const float PowerGaugeHeight = 8f;

        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private MachineDefinition       _machine;
        private IMachineInputProvider   _provider;

        // Built panel children
        private bool                _built;
        private TextMeshProUGUI     _headerTitle;
        private TextMeshProUGUI     _howItWorksText;
        private TextMeshProUGUI     _processBadge;
        private Image               _processBadgeBg;
        private RectTransform       _inputGridRect;
        private readonly List<Image>           _inputSlotBgs    = new List<Image>();
        private readonly List<Image>           _inputSlotIcons  = new List<Image>();
        private readonly List<TMP_Text>        _inputSlotLabels = new List<TMP_Text>();
        private Image               _outputSlotBg;
        private Image               _outputSlotIcon;
        private TMP_Text            _outputSlotLabel;
        private GameObject          _fuelSlotGo;
        private Image               _fuelSlotBg;
        private Image               _fuelSlotIcon;
        private TMP_Text            _fuelSlotLabel;
        private Image               _progressFill;
        private GameObject          _powerGaugeGo;
        private Image               _powerGaugeFill;
        private RecipeTabsUI        _recipeTabs;
        private GameObject          _recipeTabsGo;

        private bool _subscribedToProvider;

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>True while the panel is visible and bound to a machine.</summary>
        public bool IsOpen => gameObject.activeSelf && _machine != null;

        /// <summary>The machine the panel is currently bound to, or null.</summary>
        public MachineDefinition CurrentMachine => _machine;

        /// <summary>
        /// Open the panel for the given machine + provider. Idempotent: calling
        /// Open again with a different machine rebuilds the panel.
        /// </summary>
        public void Open(MachineDefinition machine, IMachineInputProvider inputs)
        {
            if (machine == null)
            {
                Debug.LogWarning("[MachineUI] Open called with null MachineDefinition.");
                return;
            }

            UnsubscribeFromProvider();

            _machine  = machine;
            _provider = inputs;

            EnsureBuilt();
            RebuildForMachine();

            SubscribeToProvider();
            RefreshAll();

            gameObject.SetActive(true);
        }

        /// <summary>Hide the panel. UIManager handles cursor re-lock.</summary>
        public void Close()
        {
            UnsubscribeFromProvider();
            _machine  = null;
            _provider = null;
            gameObject.SetActive(false);
            TooltipUI.Hide();
        }

        /// <summary>
        /// Idempotent build hook. Tests that AddComponent this MonoBehaviour
        /// directly can drive panel construction without triggering Awake.
        /// </summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            BuildPanelScaffold();
            _built = true;
        }

        // ---------------------------------------------------------------
        //  Test introspection (read-only)
        // ---------------------------------------------------------------

        public TextMeshProUGUI HowItWorksText => _howItWorksText;
        public TextMeshProUGUI HeaderTitle     => _headerTitle;
        public TextMeshProUGUI ProcessBadge    => _processBadge;
        public Color           ProcessBadgeColor =>
            _processBadge != null ? _processBadge.color : Color.clear;
        public Image           ProgressFill    => _progressFill;
        public Image           PowerGaugeFill  => _powerGaugeFill;
        public RecipeTabsUI    RecipeTabs      => _recipeTabs;
        public IReadOnlyList<Image> InputSlotBgs => _inputSlotBgs;
        public Image           OutputSlotBg    => _outputSlotBg;
        public GameObject      FuelSlotGo      => _fuelSlotGo;
        public GameObject      PowerGaugeGo    => _powerGaugeGo;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            EnsureBuilt();
            // Panels start hidden — UIManager.OpenMachineUI calls Open() which
            // re-activates the GO.
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromProvider();
        }

        // ---------------------------------------------------------------
        //  Panel construction (one-time scaffolding)
        // ---------------------------------------------------------------

        private void BuildPanelScaffold()
        {
            // Root sizing — fixed pixel size, centred by the caller's anchor.
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            // Background panel (UIStyle.Panel) on the same GameObject.
            Image bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = UIStyle.Panel;
            bg.raycastTarget = true;

            // 1px Border child sized to fill the panel.
            UIBuilder.Border(rt, UIStyle.Border);

            BuildHeader();
            BuildProseBlock();
            BuildProcessBadge();
            BuildInputGrid();
            BuildOutputSlot();
            BuildFuelSlot();
            BuildProgressBar();
            BuildPowerGauge();
            BuildRecipeTabs();
        }

        private void BuildHeader()
        {
            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            header.layer = uiLayer >= 0 ? uiLayer : 5;

            Image headerBg = header.GetComponent<Image>();
            headerBg.color = UIStyle.PanelLight;
            headerBg.raycastTarget = false;

            RectTransform hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot     = new Vector2(0.5f, 1f);
            hRt.sizeDelta = new Vector2(0f, HeaderHeight);
            hRt.anchoredPosition = new Vector2(0f, -UIStyle.PanelPadding);

            // Title (left)
            _headerTitle = UIBuilder.Text(
                header.transform, "Machine",
                UIStyle.FontSizeHeader, UIStyle.Text, "HeaderTitle");
            _headerTitle.alignment = TextAlignmentOptions.Left;
            _headerTitle.fontStyle = FontStyles.Bold;
            RectTransform tRt = _headerTitle.rectTransform;
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(0.6f, 1f);
            tRt.offsetMin = new Vector2(UIStyle.PanelPadding, 0f);
            tRt.offsetMax = new Vector2(0f, 0f);

            // ESC hint (right)
            TextMeshProUGUI hint = UIBuilder.Text(
                header.transform, "[ESC] Close",
                UIStyle.FontSizeBody, UIStyle.TextDim, "HeaderHint");
            hint.alignment = TextAlignmentOptions.Right;
            RectTransform hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0.4f, 0f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.offsetMin = new Vector2(0f, 0f);
            hintRt.offsetMax = new Vector2(-UIStyle.PanelPadding, 0f);
        }

        private void BuildProseBlock()
        {
            // The howItWorks block is the key design surface for V4.4. It tells
            // the player whether the machine is forgiving or picky and (for
            // forgiving machines) what kinds of substitutes it accepts.
            GameObject prose = new GameObject("HowItWorks", typeof(RectTransform));
            prose.transform.SetParent(transform, false);

            RectTransform pRt = prose.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 1f);
            pRt.anchorMax = new Vector2(0.5f, 1f);
            pRt.pivot     = new Vector2(0.5f, 1f);
            pRt.sizeDelta = new Vector2(ProseWidth, ProseHeight);
            pRt.anchoredPosition = new Vector2(
                0f,
                -(UIStyle.PanelPadding + HeaderHeight + SectionGap));

            _howItWorksText = UIBuilder.Text(
                prose.transform, string.Empty,
                UIStyle.FontSizeBody, UIStyle.TextDim, "HowItWorksText");
            _howItWorksText.alignment = TextAlignmentOptions.TopLeft;
            _howItWorksText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform tRt = _howItWorksText.rectTransform;
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        private void BuildProcessBadge()
        {
            // Small pill in the top-right of the panel showing the machine's
            // processType. Colour-coded so the player can read "forgiving" vs.
            // "picky" at a glance.
            GameObject badge = new GameObject("ProcessBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            badge.layer = uiLayer >= 0 ? uiLayer : 5;

            _processBadgeBg = badge.GetComponent<Image>();
            _processBadgeBg.color = UIStyle.PanelLight;
            _processBadgeBg.raycastTarget = false;

            RectTransform bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 1f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot     = new Vector2(1f, 1f);
            bRt.sizeDelta = new Vector2(160f, 18f);
            bRt.anchoredPosition = new Vector2(
                -(UIStyle.PanelPadding + 70f),
                -(UIStyle.PanelPadding + HeaderHeight + 4f));

            _processBadge = UIBuilder.Text(
                badge.transform, string.Empty,
                UIStyle.FontSizeSmall, UIStyle.Text, "ProcessBadgeText");
            _processBadge.alignment = TextAlignmentOptions.Center;
            _processBadge.fontStyle = FontStyles.Bold;
            RectTransform tRt = _processBadge.rectTransform;
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(4f, 0f);
            tRt.offsetMax = new Vector2(-4f, 0f);
        }

        private void BuildInputGrid()
        {
            // Input grid sized at runtime per MachineDefinition.gridWidth /
            // gridHeight. The slot count is rebuilt inside RebuildForMachine
            // because we don't know the dimensions until Open() is called.
            GameObject grid = new GameObject("InputGrid", typeof(RectTransform));
            grid.transform.SetParent(transform, false);

            _inputGridRect = grid.GetComponent<RectTransform>();
            _inputGridRect.anchorMin = new Vector2(0f, 1f);
            _inputGridRect.anchorMax = new Vector2(0f, 1f);
            _inputGridRect.pivot     = new Vector2(0f, 1f);
            _inputGridRect.anchoredPosition = new Vector2(
                UIStyle.PanelPadding,
                -(UIStyle.PanelPadding + HeaderHeight + SectionGap + ProseHeight + SectionGap));

            GridLayoutGroup glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(UIStyle.SlotSize, UIStyle.SlotSize);
            glg.spacing         = new Vector2(UIStyle.SlotGap, UIStyle.SlotGap);
            glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment  = TextAnchor.UpperLeft;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
        }

        private void BuildOutputSlot()
        {
            GameObject slot = new GameObject("OutputSlot", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            slot.layer = uiLayer >= 0 ? uiLayer : 5;

            _outputSlotBg = slot.GetComponent<Image>();
            _outputSlotBg.color = UIStyle.PanelLight;
            _outputSlotBg.raycastTarget = true;

            float size = UIStyle.SlotSize * OutputSlotScale;
            RectTransform sRt = slot.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(1f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.pivot     = new Vector2(1f, 1f);
            sRt.sizeDelta = new Vector2(size, size);
            sRt.anchoredPosition = new Vector2(
                -(UIStyle.PanelPadding),
                -(UIStyle.PanelPadding + HeaderHeight + SectionGap + ProseHeight + SectionGap));

            UIBuilder.Border(sRt, UIStyle.Border);

            // Icon
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slot.transform, false);
            iconGo.layer = slot.layer;
            _outputSlotIcon = iconGo.GetComponent<Image>();
            _outputSlotIcon.preserveAspect = true;
            _outputSlotIcon.raycastTarget = false;
            _outputSlotIcon.enabled = false;
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            // Stack label
            _outputSlotLabel = UIBuilder.Text(
                slot.transform, string.Empty,
                UIStyle.FontSizeSmall, UIStyle.Text, "Count");
            _outputSlotLabel.alignment = TextAlignmentOptions.BottomRight;
            _outputSlotLabel.fontStyle = FontStyles.Bold;
            _outputSlotLabel.enabled = false;
            RectTransform lblRt = _outputSlotLabel.rectTransform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(2f, 2f);
            lblRt.offsetMax = new Vector2(-2f, -2f);
        }

        private void BuildFuelSlot()
        {
            // Optional — only shown when the active provider's NeedsFuel is true.
            _fuelSlotGo = new GameObject("FuelSlot", typeof(RectTransform), typeof(Image));
            _fuelSlotGo.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            _fuelSlotGo.layer = uiLayer >= 0 ? uiLayer : 5;

            _fuelSlotBg = _fuelSlotGo.GetComponent<Image>();
            _fuelSlotBg.color = UIStyle.PanelLight;
            _fuelSlotBg.raycastTarget = true;

            RectTransform fRt = _fuelSlotGo.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0f, 1f);
            fRt.anchorMax = new Vector2(0f, 1f);
            fRt.pivot     = new Vector2(0f, 1f);
            fRt.sizeDelta = new Vector2(UIStyle.SlotSize, UIStyle.SlotSize);
            fRt.anchoredPosition = new Vector2(
                UIStyle.PanelPadding,
                -(UIStyle.PanelPadding + HeaderHeight + SectionGap + ProseHeight + SectionGap
                  + 3f * UIStyle.SlotSize + 2f * UIStyle.SlotGap + SectionGap));

            UIBuilder.Border(fRt, UIStyle.Border);

            // Icon
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_fuelSlotGo.transform, false);
            iconGo.layer = _fuelSlotGo.layer;
            _fuelSlotIcon = iconGo.GetComponent<Image>();
            _fuelSlotIcon.preserveAspect = true;
            _fuelSlotIcon.raycastTarget = false;
            _fuelSlotIcon.enabled = false;
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            // Stack label
            _fuelSlotLabel = UIBuilder.Text(
                _fuelSlotGo.transform, string.Empty,
                UIStyle.FontSizeSmall, UIStyle.Text, "Count");
            _fuelSlotLabel.alignment = TextAlignmentOptions.BottomRight;
            _fuelSlotLabel.fontStyle = FontStyles.Bold;
            _fuelSlotLabel.enabled = false;
            RectTransform lblRt = _fuelSlotLabel.rectTransform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(2f, 2f);
            lblRt.offsetMax = new Vector2(-2f, -2f);

            _fuelSlotGo.SetActive(false);
        }

        private void BuildProgressBar()
        {
            GameObject bar = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            bar.layer = uiLayer >= 0 ? uiLayer : 5;

            Image barBg = bar.GetComponent<Image>();
            barBg.color = UIStyle.Panel;
            barBg.raycastTarget = false;

            RectTransform bRt = bar.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.pivot     = new Vector2(0.5f, 0f);
            bRt.sizeDelta = new Vector2(
                -2f * UIStyle.PanelPadding,
                ProgressHeight);
            bRt.anchoredPosition = new Vector2(0f, UIStyle.PanelPadding);

            UIBuilder.Border(bRt, UIStyle.Border);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bar.transform, false);
            fillGo.layer = bar.layer;
            _progressFill = fillGo.GetComponent<Image>();
            _progressFill.color = UIStyle.Accent;
            _progressFill.raycastTarget = false;
            _progressFill.type       = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFill.fillAmount = 0f;
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(UIStyle.BorderWidth, UIStyle.BorderWidth);
            fillRt.offsetMax = new Vector2(-UIStyle.BorderWidth, -UIStyle.BorderWidth);
        }

        private void BuildPowerGauge()
        {
            _powerGaugeGo = new GameObject("PowerGauge", typeof(RectTransform), typeof(Image));
            _powerGaugeGo.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            _powerGaugeGo.layer = uiLayer >= 0 ? uiLayer : 5;

            Image pgBg = _powerGaugeGo.GetComponent<Image>();
            pgBg.color = UIStyle.Panel;
            pgBg.raycastTarget = false;

            RectTransform pgRt = _powerGaugeGo.GetComponent<RectTransform>();
            pgRt.anchorMin = new Vector2(0f, 0f);
            pgRt.anchorMax = new Vector2(1f, 0f);
            pgRt.pivot     = new Vector2(0.5f, 0f);
            pgRt.sizeDelta = new Vector2(
                -2f * UIStyle.PanelPadding,
                PowerGaugeHeight);
            pgRt.anchoredPosition = new Vector2(
                0f, UIStyle.PanelPadding + ProgressHeight + 4f);

            UIBuilder.Border(pgRt, UIStyle.Border);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_powerGaugeGo.transform, false);
            fillGo.layer = _powerGaugeGo.layer;
            _powerGaugeFill = fillGo.GetComponent<Image>();
            // Accent-dim so the gauge reads as "power available but not always on"
            _powerGaugeFill.color = UIStyle.AccentDim;
            _powerGaugeFill.raycastTarget = false;
            _powerGaugeFill.type       = Image.Type.Filled;
            _powerGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _powerGaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _powerGaugeFill.fillAmount = 0f;
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(UIStyle.BorderWidth, UIStyle.BorderWidth);
            fillRt.offsetMax = new Vector2(-UIStyle.BorderWidth, -UIStyle.BorderWidth);

            _powerGaugeGo.SetActive(false);
        }

        private void BuildRecipeTabs()
        {
            _recipeTabsGo = new GameObject("RecipeTabs", typeof(RectTransform));
            _recipeTabsGo.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            _recipeTabsGo.layer = uiLayer >= 0 ? uiLayer : 5;

            RectTransform rtRt = _recipeTabsGo.GetComponent<RectTransform>();
            rtRt.anchorMin = new Vector2(0f, 1f);
            rtRt.anchorMax = new Vector2(1f, 1f);
            rtRt.pivot     = new Vector2(0.5f, 1f);
            rtRt.sizeDelta = new Vector2(
                -2f * UIStyle.PanelPadding,
                30f);
            rtRt.anchoredPosition = new Vector2(
                0f,
                -(UIStyle.PanelPadding + HeaderHeight + SectionGap + ProseHeight + SectionGap
                  + 3f * UIStyle.SlotSize + 2f * UIStyle.SlotGap + SectionGap));

            _recipeTabs = _recipeTabsGo.AddComponent<RecipeTabsUI>();
            _recipeTabs.OnRecipeSelected += OnRecipeTabSelected;

            _recipeTabsGo.SetActive(false);
        }

        // ---------------------------------------------------------------
        //  Rebuild per-machine (input grid sizing, recipe tabs)
        // ---------------------------------------------------------------

        private void RebuildForMachine()
        {
            // Header — machine display name (falls back to ID).
            string title = !string.IsNullOrEmpty(_machine.displayName)
                ? _machine.displayName
                : (_machine.itemId ?? "Machine");
            if (_headerTitle != null) _headerTitle.text = title;

            // Prose block — the howItWorks description, verbatim.
            if (_howItWorksText != null)
            {
                _howItWorksText.text = _machine.howItWorks ?? string.Empty;
            }

            // Process badge — text + colour from processType.
            ApplyProcessBadge(_machine.processType);

            // Input grid — rebuild slots for the machine's dimensions.
            RebuildInputGrid(_machine.gridWidth, _machine.gridHeight);

            // Recipe tabs — show only when the machine has > 1 recipe.
            RebuildRecipeTabs();

            // Fuel + power visibility — driven by the provider's flags.
            UpdateOptionalSections();
        }

        private void ApplyProcessBadge(MachineProcessType type)
        {
            if (_processBadge == null) return;

            string label = type.ToString();
            Color badgeColor;

            if (type == MachineProcessType.Hybrid_Crafting)
                badgeColor = UIStyle.Accent;
            else if (label.StartsWith("Forgiving_", StringComparison.Ordinal))
                badgeColor = UIStyle.TextSuccess;
            else if (label.StartsWith("Picky_", StringComparison.Ordinal))
                badgeColor = UIStyle.TextError;
            else
                badgeColor = UIStyle.Text;

            _processBadge.text  = label;
            _processBadge.color = badgeColor;
        }

        private void RebuildInputGrid(int gridWidth, int gridHeight)
        {
            if (_inputGridRect == null) return;

            if (gridWidth  < 1) gridWidth  = 1;
            if (gridHeight < 1) gridHeight = 1;

            GridLayoutGroup glg = _inputGridRect.GetComponent<GridLayoutGroup>();
            if (glg != null) glg.constraintCount = gridWidth;

            // Tear down old slot children.
            for (int i = _inputGridRect.childCount - 1; i >= 0; i--)
            {
                Transform child = _inputGridRect.GetChild(i);
                if (child != null) GameObject.DestroyImmediate(child.gameObject);
            }
            _inputSlotBgs.Clear();
            _inputSlotIcons.Clear();
            _inputSlotLabels.Clear();

            int total = gridWidth * gridHeight;
            int uiLayer = LayerMask.NameToLayer("UI");
            int layer = uiLayer >= 0 ? uiLayer : 5;
            for (int i = 0; i < total; i++)
            {
                GameObject slot = new GameObject(
                    $"InputSlot_{i}",
                    typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(_inputGridRect, false);
                slot.layer = layer;

                Image bg = slot.GetComponent<Image>();
                bg.color = UIStyle.PanelLight;
                bg.raycastTarget = true;
                _inputSlotBgs.Add(bg);

                UIBuilder.Border(slot.GetComponent<RectTransform>(), UIStyle.Border);

                // Icon
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(slot.transform, false);
                iconGo.layer = layer;
                Image icon = iconGo.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.1f, 0.1f);
                iconRt.anchorMax = new Vector2(0.9f, 0.9f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                _inputSlotIcons.Add(icon);

                // Stack label
                TextMeshProUGUI label = UIBuilder.Text(
                    slot.transform, string.Empty,
                    UIStyle.FontSizeSmall, UIStyle.Text, "Count");
                label.alignment = TextAlignmentOptions.BottomRight;
                label.fontStyle = FontStyles.Bold;
                label.enabled = false;
                RectTransform lblRt = label.rectTransform;
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(2f, 2f);
                lblRt.offsetMax = new Vector2(-2f, -2f);
                _inputSlotLabels.Add(label);
            }

            // Re-size the grid rect so the slots actually fit within the
            // anchored-position math used by neighbouring children.
            _inputGridRect.sizeDelta = new Vector2(
                gridWidth  * UIStyle.SlotSize + (gridWidth  - 1) * UIStyle.SlotGap,
                gridHeight * UIStyle.SlotSize + (gridHeight - 1) * UIStyle.SlotGap);
        }

        private void RebuildRecipeTabs()
        {
            if (_recipeTabs == null || _recipeTabsGo == null) return;

            List<RecipeDefinition> recipes = ResolveRecipesForMachine(_machine);

            if (recipes.Count <= 1)
            {
                // Spec calls for the tab strip only when the machine has > 1
                // recipe. Hide the GO to keep it out of layout calculations.
                _recipeTabs.Build(System.Array.Empty<RecipeDefinition>());
                _recipeTabsGo.SetActive(false);
                return;
            }

            _recipeTabs.Build(recipes);
            _recipeTabsGo.SetActive(true);
        }

        private static List<RecipeDefinition> ResolveRecipesForMachine(MachineDefinition machine)
        {
            List<RecipeDefinition> list = new List<RecipeDefinition>(4);
            if (machine == null || string.IsNullOrEmpty(machine.itemId)) return list;

            RecipeRegistry reg = RecipeRegistry.Instance;
            if (reg == null) return list;

            foreach (RecipeDefinition r in reg.ByMachine(machine.itemId))
            {
                if (r != null) list.Add(r);
            }
            return list;
        }

        private void UpdateOptionalSections()
        {
            bool needsFuel  = _provider != null && _provider.NeedsFuel;
            bool needsPower = _provider != null && _provider.NeedsPower;

            if (_fuelSlotGo   != null) _fuelSlotGo.SetActive(needsFuel);
            if (_powerGaugeGo != null) _powerGaugeGo.SetActive(needsPower);
        }

        // ---------------------------------------------------------------
        //  Provider subscription
        // ---------------------------------------------------------------

        private void SubscribeToProvider()
        {
            if (_provider == null || _subscribedToProvider) return;
            _provider.OnStateChanged += OnProviderStateChanged;
            _subscribedToProvider = true;
        }

        private void UnsubscribeFromProvider()
        {
            if (_provider == null || !_subscribedToProvider) return;
            _provider.OnStateChanged -= OnProviderStateChanged;
            _subscribedToProvider = false;
        }

        private void OnProviderStateChanged()
        {
            if (!IsOpen) return;
            RefreshAll();
        }

        private void OnRecipeTabSelected(RecipeDefinition recipe)
        {
            if (_provider == null || recipe == null) return;
            // V4.4 scope: just fire the start request. V6.1 will gate this on
            // the match engine; for the M2 stub flow the provider records the
            // active recipe and the UI updates.
            _provider.TryStartRecipe(recipe);
        }

        // ---------------------------------------------------------------
        //  Refresh
        // ---------------------------------------------------------------

        /// <summary>Forces a re-read of provider state. Public so tests can
        /// poke the panel after mutating the stub without waiting for the
        /// next event broadcast.</summary>
        public void RefreshAll()
        {
            RefreshInputSlots();
            RefreshOutputSlot();
            RefreshFuelSlot();
            RefreshProgress();
            RefreshPowerGauge();
        }

        private void RefreshInputSlots()
        {
            if (_provider == null) return;
            IReadOnlyList<ItemStack> inputs = _provider.Inputs;
            int count = (inputs != null) ? inputs.Count : 0;
            for (int i = 0; i < _inputSlotIcons.Count; i++)
            {
                ItemStack stack = (i < count) ? inputs[i] : default;
                ApplyStackToSlot(stack, _inputSlotIcons[i], _inputSlotLabels[i]);
            }
        }

        private void RefreshOutputSlot()
        {
            if (_provider == null)
            {
                ApplyStackToSlot(default, _outputSlotIcon, _outputSlotLabel);
                return;
            }
            IReadOnlyList<ItemStack> outs = _provider.Outputs;
            ItemStack stack = (outs != null && outs.Count > 0) ? outs[0] : default;
            ApplyStackToSlot(stack, _outputSlotIcon, _outputSlotLabel);
        }

        private void RefreshFuelSlot()
        {
            if (_fuelSlotGo == null) return;
            bool needsFuel = _provider != null && _provider.NeedsFuel;
            _fuelSlotGo.SetActive(needsFuel);
            if (!needsFuel) return;
            ApplyStackToSlot(_provider.FuelSlot, _fuelSlotIcon, _fuelSlotLabel);
        }

        private void RefreshProgress()
        {
            if (_progressFill == null) return;
            float p = (_provider != null) ? _provider.Progress : 0f;
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            _progressFill.fillAmount = p;
        }

        private void RefreshPowerGauge()
        {
            if (_powerGaugeGo == null) return;
            bool needsPower = _provider != null && _provider.NeedsPower;
            _powerGaugeGo.SetActive(needsPower);
            if (!needsPower || _powerGaugeFill == null) return;
            float s = _provider.PowerSatisfaction;
            if (s < 0f) s = 0f;
            if (s > 1f) s = 1f;
            _powerGaugeFill.fillAmount = s;
        }

        private static void ApplyStackToSlot(ItemStack stack, Image icon, TMP_Text label)
        {
            if (icon == null || label == null) return;

            if (stack.IsEmpty)
            {
                icon.enabled = false;
                label.enabled = false;
                label.text = string.Empty;
                return;
            }

            Sprite sprite = ItemIconGenerator.GetIcon(stack.item);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color  = Color.white;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = false;
            }

            if (stack.quantity > 1)
            {
                label.enabled = true;
                label.text = stack.quantity.ToString();
            }
            else
            {
                label.enabled = false;
                label.text = string.Empty;
            }
        }
    }
}
