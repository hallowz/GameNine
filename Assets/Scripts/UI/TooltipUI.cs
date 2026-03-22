using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Floating tooltip panel. Call TooltipUI.Show(itemDef) on hover,
/// TooltipUI.Hide() when the cursor leaves the slot.
/// The panel follows the mouse and fades in over 0.15 s.
/// </summary>
public class TooltipUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Singleton
    // ---------------------------------------------------------------
    public static TooltipUI Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Inspector references (all built in code — set by Build())
    // ---------------------------------------------------------------
    private RectTransform _rectTransform;
    private CanvasGroup   _group;

    // Left border (coloured by item type)
    private Image _borderImage;

    // Content elements
    private Image    _iconImage;
    private TMP_Text _nameText;
    private TMP_Text _typeText;
    private TMP_Text _descText;
    private TMP_Text _statsText;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------
    private bool  _visible;
    private float _fadeTimer;
    private const float FadeDuration = 0.15f;

    // Tooltip size
    private const float TooltipWidth  = 240f;
    private const float TooltipHeight = 140f;

    // ---------------------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        Build();

        _group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_visible) return;

        // Fade in
        _fadeTimer += Time.unscaledDeltaTime;
        _group.alpha = Mathf.Clamp01(_fadeTimer / FadeDuration);

        // Follow mouse, clamped to screen
        FollowMouse();
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    public static void Show(ItemDefinition itemDef)
    {
        if (Instance == null || itemDef == null) return;
        Instance.ShowInternal(itemDef);
    }

    /// <summary>Show tooltip for a vehicle part with runtime condition info.</summary>
    public static void Show(ItemDefinition itemDef, Voidborne.Vehicles.VehiclePartCondition condition)
    {
        if (Instance == null || itemDef == null) return;
        Instance.ShowInternal(itemDef);
        // Append condition info to stats line
        if (Instance._statsText != null)
        {
            float pct = condition.Ratio * 100f;
            string stage = condition.stage.ToString();
            Instance._statsText.text += $"\nCondition: {pct:F0}% [{stage}]";
        }
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance.HideInternal();
    }

    // ---------------------------------------------------------------
    //  Internal show / hide
    // ---------------------------------------------------------------

    private void ShowInternal(ItemDefinition itemDef)
    {
        // Icon
        Sprite icon = ItemIconGenerator.GetIcon(itemDef);
        _iconImage.sprite  = icon;
        _iconImage.enabled = icon != null;

        // Name
        _nameText.text = itemDef.displayName;

        // Type label
        _typeText.text  = $"[{itemDef.itemType}]";
        _typeText.color = ItemIconGenerator.GetTypeColor(itemDef.itemType);

        // Description
        _descText.text = string.IsNullOrEmpty(itemDef.description)
            ? "<i>No description.</i>"
            : itemDef.description;

        // Stats — show weapon or tool-specific info when applicable
        if (itemDef is Voidborne.Vehicles.VehiclePartItem vpi)
        {
            _statsText.text = $"Part: {vpi.partType}   Section: {vpi.fitsSection}   Max HP: {vpi.maxCondition:0}";
        }
        else if (itemDef is Voidborne.Automation.SegmentItem seg)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(seg.speedLabel))
                parts.Add($"Speed: {seg.speedLabel}");
            parts.Add($"Max length: {seg.maxLength:0} m");
            if (seg.powerDrawWatts > 0f)
                parts.Add($"Draw: {seg.powerDrawWatts:0} W");
            if (seg.capacity > 0)
                parts.Add($"Buffer: {seg.capacity} items");
            _statsText.text = string.Join("   ", parts);
        }
        else if (itemDef is Voidborne.Automation.AutomationItem ai)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(ai.speedLabel))
                parts.Add($"Speed: {ai.speedLabel}");
            else if (ai.tickInterval > 0f)
                parts.Add($"Tick: {ai.tickInterval:0.00} s");
            if (ai.powerDrawWatts > 0f)
                parts.Add($"Draw: {ai.powerDrawWatts:0} W");
            if (ai.capacity > 0)
                parts.Add($"Buffer: {ai.capacity} items");
            _statsText.text = parts.Count > 0
                ? string.Join("   ", parts)
                : "Automation device";
        }
        else if (itemDef is DevBackpackItem)
        {
            _statsText.text = "INFINITE   All items   Never depletes";
        }
        else if (itemDef is BackpackItem bpDef)
        {
            int slots = bpDef.extraRows * bpDef.extraColumns;
            _statsText.text = $"Storage: {bpDef.extraColumns}×{bpDef.extraRows}  ({slots} slots)   Worn slot or Hotbar";
        }
        else if (itemDef is StorageItem sti)
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"Storage: {sti.cols}×{sti.rows} ({sti.SlotCount} slots)");
            if (sti.stackMultiplier > 1)
                parts.Add($"Stacks to {sti.stackMultiplier}×");
            if (sti.powerDrawWatts > 0f)
                parts.Add($"Draw: {sti.powerDrawWatts:0} W");
            _statsText.text = string.Join("   ", parts);
        }
        else if (itemDef is PlaceableItem plc)
        {
            _statsText.text = $"{plc.categoryLabel}   Select + Click to place";
        }
        else if (itemDef is Voidborne.Building.Electricity.ElectricityItem ei)
        {
            string output = ei.outputWatts > 0f ? $"Output: {ei.outputWatts:0} W   " : "";
            string draw   = ei.drawWatts   > 0f ? $"Draw: {ei.drawWatts:0} W" : "";
            _statsText.text = (output + draw).Trim();
            if (string.IsNullOrEmpty(_statsText.text)) _statsText.text = "Passive device";
        }
        else if (itemDef is Voidborne.Automation.NetworkCableItem nci)
        {
            _statsText.text = $"Data cable   Max length: {nci.maxLength:0} m   Stack: {nci.maxStackSize}";
        }
        else if (itemDef is Voidborne.Building.Electricity.WireItem wri)
        {
            string throughput = wri.maxThroughputWatts > 0
                ? $"Max throughput: {wri.maxThroughputWatts:0} W   "
                : "";
            _statsText.text = $"{throughput}Max length: {wri.maxLength:0} m   Stack: {wri.maxStackSize}";
        }
        else if (itemDef is Voidborne.Building.Electricity.PowerProbeItem ppi)
        {
            _statsText.text = $"Scan range: {ppi.scanRange:0} m";
        }
        else if (itemDef is BuildingPieceItem bpi && bpi.pieceData != null)
        {
            var data = bpi.pieceData;
            int tierIdx = (int)bpi.materialTier;
            float hp = (data.healthPerTier != null && tierIdx < data.healthPerTier.Length) ? data.healthPerTier[tierIdx] : 150f;
            _statsText.text = $"Type: {data.pieceType}   Tier: {bpi.materialTier}   HP: {hp:0}";
        }
        else if (itemDef is ToolDefinition toolDef)
        {
            _statsText.text = $"Tier: {toolDef.toolTier}   Speed: {toolDef.miningSpeedMultiplier:0.0}x   Durability: {toolDef.maxDurability}";
        }
        else if (itemDef is WeaponItem wi && wi.meleeDefinition != null)
        {
            var m = wi.meleeDefinition;
            string pen = m.armorPenetration > 0f ? $"   Pen: {m.armorPenetration * 100f:0}%" : "";
            _statsText.text = $"DMG: {m.damage:0}   Range: {m.range:0.0}m   Speed: {m.attackSpeed:0.0}x{pen}";
        }
        else if (itemDef is WeaponItem gwi && gwi.gunDefinition != null)
        {
            var g = gwi.gunDefinition;
            _statsText.text = $"DMG: {g.damage:0}   RPM: {g.fireRate:0}   Mag: {g.magazineSize}";
        }
        else if (itemDef is WeaponItem bwi && bwi.bowDefinition != null)
        {
            var b = bwi.bowDefinition;
            _statsText.text = $"Draw: {b.fullDrawTime:0.0}s   Spd: {b.minVelocity:0}–{b.maxVelocity:0} m/s   Zoom: {b.drawZoom:0.00}x";
        }
        else if (itemDef is WeaponItem twi && twi.throwableDefinition != null)
        {
            var t = twi.throwableDefinition;
            string spin = t.tumbles ? "Tumbles" : "Stable";
            _statsText.text = $"Windup: {t.windupTime:0.0}s   Spd: {t.throwVelocity:0} m/s   {spin}";
        }
        else
        {
            _statsText.text = $"Weight: {itemDef.weight:0.0}   Max Stack: {itemDef.maxStackSize}";
        }

        // Border colour
        _borderImage.color = ItemIconGenerator.GetTypeColor(itemDef.itemType);

        gameObject.SetActive(true);
        _visible    = true;
        _fadeTimer  = 0f;
        _group.alpha = 0f;

        FollowMouse();
    }

    private void HideInternal()
    {
        _visible = false;
        gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Offset so tooltip appears to the right and slightly below cursor
        Vector2 offset = new Vector2(15f, -15f);
        Vector2 targetPos = mousePos + offset;

        // Get tooltip size for edge clamping
        Vector2 size = _rectTransform.sizeDelta;

        // Clamp so tooltip stays on screen
        float maxX = Screen.width - size.x - 5f;
        float maxY = Screen.height - 5f;
        float minX = 5f;
        float minY = size.y + 5f;

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        _rectTransform.position = new Vector3(targetPos.x, targetPos.y, 0f);
    }

    // ---------------------------------------------------------------
    //  Build the tooltip hierarchy in code
    // ---------------------------------------------------------------

    private void Build()
    {
        _rectTransform  = GetComponent<RectTransform>();
        _group = gameObject.AddComponent<CanvasGroup>();

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot     = Vector2.zero;
        _rectTransform.sizeDelta = new Vector2(TooltipWidth, TooltipHeight);

        // ── Background panel ──────────────────────────────────────
        Color bgColor = new Color(0.10f, 0.10f, 0.10f, 0.92f);
        Image bg = gameObject.AddComponent<Image>();
        bg.color = bgColor;

        // ── Left border strip ─────────────────────────────────────
        GameObject borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(transform, false);
        _borderImage = borderGO.GetComponent<Image>();
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin  = new Vector2(0, 0);
        borderRT.anchorMax  = new Vector2(0, 1);
        borderRT.offsetMin  = Vector2.zero;
        borderRT.offsetMax  = new Vector2(4, 0);

        // ── Content area ──────────────────────────────────────────
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(transform, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = new Vector2(10, 6);
        contentRT.offsetMax = new Vector2(-6, -6);

        // ── Header row (icon + name + type) ───────────────────────
        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(contentGO.transform, false);
        _iconImage = iconGO.GetComponent<Image>();
        _iconImage.preserveAspect = true;
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin  = new Vector2(0, 1);
        iconRT.anchorMax  = new Vector2(0, 1);
        iconRT.pivot      = new Vector2(0, 1);
        iconRT.sizeDelta  = new Vector2(32, 32);
        iconRT.anchoredPosition = Vector2.zero;

        // Name label
        GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(contentGO.transform, false);
        _nameText = nameGO.GetComponent<TMP_Text>();
        _nameText.text      = "";
        _nameText.color     = Color.white;
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.fontSize  = 14f;
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin        = new Vector2(0, 1);
        nameRT.anchorMax        = new Vector2(1, 1);
        nameRT.pivot            = new Vector2(0, 1);
        nameRT.anchoredPosition = new Vector2(40, 0);
        nameRT.sizeDelta        = new Vector2(-40, 18);

        // Type label (right-aligned in name row, below name)
        GameObject typeGO = new GameObject("Type", typeof(RectTransform), typeof(TextMeshProUGUI));
        typeGO.transform.SetParent(contentGO.transform, false);
        _typeText = typeGO.GetComponent<TMP_Text>();
        _typeText.text      = "";
        _typeText.fontSize  = 11f;
        _typeText.fontStyle = FontStyles.Normal;
        RectTransform typeRT = typeGO.GetComponent<RectTransform>();
        typeRT.anchorMin        = new Vector2(0, 1);
        typeRT.anchorMax        = new Vector2(1, 1);
        typeRT.pivot            = new Vector2(0, 1);
        typeRT.anchoredPosition = new Vector2(40, -18);
        typeRT.sizeDelta        = new Vector2(-40, 16);

        // ── Divider ───────────────────────────────────────────────
        GameObject div1 = new GameObject("Div1", typeof(RectTransform), typeof(Image));
        div1.transform.SetParent(contentGO.transform, false);
        div1.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        RectTransform div1RT = div1.GetComponent<RectTransform>();
        div1RT.anchorMin        = new Vector2(0, 1);
        div1RT.anchorMax        = new Vector2(1, 1);
        div1RT.pivot            = new Vector2(0, 1);
        div1RT.anchoredPosition = new Vector2(0, -40);
        div1RT.sizeDelta        = new Vector2(0, 1);

        // ── Description ───────────────────────────────────────────
        GameObject descGO = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(contentGO.transform, false);
        _descText = descGO.GetComponent<TMP_Text>();
        _descText.text           = "";
        _descText.color          = new Color(0.75f, 0.75f, 0.75f);
        _descText.fontSize       = 11f;
        _descText.fontStyle      = FontStyles.Italic;
        _descText.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin        = new Vector2(0, 1);
        descRT.anchorMax        = new Vector2(1, 1);
        descRT.pivot            = new Vector2(0, 1);
        descRT.anchoredPosition = new Vector2(0, -44);
        descRT.sizeDelta        = new Vector2(0, 52);

        // ── Divider 2 ─────────────────────────────────────────────
        GameObject div2 = new GameObject("Div2", typeof(RectTransform), typeof(Image));
        div2.transform.SetParent(contentGO.transform, false);
        div2.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        RectTransform div2RT = div2.GetComponent<RectTransform>();
        div2RT.anchorMin        = new Vector2(0, 1);
        div2RT.anchorMax        = new Vector2(1, 1);
        div2RT.pivot            = new Vector2(0, 1);
        div2RT.anchoredPosition = new Vector2(0, -98);
        div2RT.sizeDelta        = new Vector2(0, 1);

        // ── Stats ─────────────────────────────────────────────────
        GameObject statsGO = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsGO.transform.SetParent(contentGO.transform, false);
        _statsText = statsGO.GetComponent<TMP_Text>();
        _statsText.text      = "";
        _statsText.color     = new Color(0.55f, 0.55f, 0.55f);
        _statsText.fontSize  = 10f;
        RectTransform statsRT = statsGO.GetComponent<RectTransform>();
        statsRT.anchorMin        = new Vector2(0, 1);
        statsRT.anchorMax        = new Vector2(1, 1);
        statsRT.pivot            = new Vector2(0, 1);
        statsRT.anchoredPosition = new Vector2(0, -102);
        statsRT.sizeDelta        = new Vector2(0, 20);
    }
}
