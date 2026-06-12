using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Data;
using Voidborne.UI;
using Voidborne.UI.Style;

/// <summary>
/// Volume 4.5 — rich floating tooltip panel.
///
/// Restyled to the V4.1 UIStyle / UIBuilder palette. Content layout
/// (top-to-bottom):
/// <list type="bullet">
///   <item>Item name (FontSizeHeader, UIStyle.Text).</item>
///   <item>Badges row — kind, source (if Source), one badge per category.</item>
///   <item>Properties row — color-coded MaterialProperties tags.</item>
///   <item>Description / role body text (FontSizeBody, UIStyle.TextDim).</item>
///   <item>Machine-only: processType badge + howItWorks first sentence.</item>
///   <item>Recipe summary ("Made via N recipes").</item>
///   <item>Tool/weapon stats (tier / damage / etc).</item>
/// </list>
///
/// API:
/// <list type="bullet">
///   <item><see cref="Show(ItemDefinition)"/> — primary entry, also legacy.</item>
///   <item><see cref="Show(ItemDefinition, Vector2)"/> — explicit cursor anchor.</item>
///   <item><see cref="Show(MachineDefinition, Vector2)"/> — processType family explainer.</item>
///   <item><see cref="Show(MaterialProperties, Vector2)"/> — property badge tooltip.</item>
///   <item><see cref="Show(string, string, Vector2)"/> — generic fallback.</item>
///   <item><see cref="Hide"/> — no-op when already hidden.</item>
/// </list>
///
/// Coop note: tooltips are client-local. Reads <see cref="ItemDefinition"/>,
/// <see cref="MachineDefinition"/>, and <see cref="RecipeRegistry"/> — all
/// stateless content data.
/// </summary>
public class TooltipUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Singleton
    // ---------------------------------------------------------------
    public static TooltipUI Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Sizing
    // ---------------------------------------------------------------
    private const float PanelWidth   = 280f;
    private const float PanelMinHeight = 80f;
    private const float CursorOffsetX = 18f;
    private const float CursorOffsetY = -18f;
    private const float FadeDuration  = 0.10f;

    // ---------------------------------------------------------------
    //  References (built in code by Build())
    // ---------------------------------------------------------------
    private RectTransform     _rectTransform;
    private CanvasGroup       _group;
    private VerticalLayoutGroup _layout;
    private ContentSizeFitter _fitter;

    // Sections — each toggled active per Show* path.
    private TextMeshProUGUI _nameText;
    private RectTransform   _badgesRow;
    private RectTransform   _propsRow;
    private TextMeshProUGUI _descText;
    private RectTransform   _machineRow;
    private TextMeshProUGUI _machineProse;
    private TextMeshProUGUI _recipeSummary;
    private TextMeshProUGUI _statsText;

    // ---------------------------------------------------------------
    //  State
    // ---------------------------------------------------------------
    private bool  _visible;
    private float _fadeTimer;
    private bool  _followMouse;

    // ---------------------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        EnsureBuilt();
        _group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_visible) return;

        _fadeTimer += Time.unscaledDeltaTime;
        _group.alpha = Mathf.Clamp01(_fadeTimer / FadeDuration);

        if (_followMouse) FollowMouse();
    }

    // ---------------------------------------------------------------
    //  Public API — Show overloads
    // ---------------------------------------------------------------

    /// <summary>
    /// Show the tooltip for an item at the current mouse position.
    /// Preferred entry for slot hover callers — matches the legacy signature.
    /// </summary>
    public static void Show(ItemDefinition itemDef)
    {
        if (Instance == null || itemDef == null) return;
        Instance.ShowItem(itemDef, useMouse: true, screenPosition: default);
    }

    /// <summary>Show the tooltip for an item at an explicit screen position.</summary>
    public static void Show(ItemDefinition itemDef, Vector2 screenPosition)
    {
        if (Instance == null || itemDef == null) return;
        Instance.ShowItem(itemDef, useMouse: false, screenPosition: screenPosition);
    }

    /// <summary>
    /// Show a vehicle-part tooltip with its runtime condition appended.
    /// V4.5 preserves the legacy V11 signature.
    /// </summary>
    public static void Show(ItemDefinition itemDef, Voidborne.Vehicles.VehiclePartCondition condition)
    {
        if (Instance == null || itemDef == null) return;
        Instance.ShowItem(itemDef, useMouse: true, screenPosition: default);
        if (Instance._statsText != null)
        {
            float pct = condition.Ratio * 100f;
            string stage = condition.stage.ToString();
            Instance._statsText.enabled = true;
            string existing = Instance._statsText.text ?? string.Empty;
            string suffix = $"Condition: {pct:F0}% [{stage}]";
            Instance._statsText.text = string.IsNullOrEmpty(existing)
                ? suffix
                : existing + "\n" + suffix;
        }
    }

    /// <summary>
    /// Show a machine-family explainer at <paramref name="screenPosition"/>.
    /// Used by MachineUI's processType badge hover.
    /// </summary>
    public static void Show(MachineDefinition machine, Vector2 screenPosition)
    {
        if (Instance == null || machine == null) return;
        Instance.ShowMachineFamily(machine, screenPosition);
    }

    /// <summary>
    /// Show a property-badge tooltip at <paramref name="screenPosition"/>.
    /// Body text comes from <see cref="PropertyDescriptions"/>.
    /// </summary>
    public static void Show(MaterialProperties property, Vector2 screenPosition)
    {
        if (Instance == null) return;
        Instance.ShowProperty(property, screenPosition);
    }

    /// <summary>Generic title + body fallback at an explicit screen position.</summary>
    public static void Show(string title, string body, Vector2 screenPosition)
    {
        if (Instance == null) return;
        Instance.ShowGeneric(title, body, screenPosition);
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance.HideInternal();
    }

    /// <summary>Read-only view of the tooltip's visibility (test introspection).</summary>
    public bool IsVisible => _visible;

    /// <summary>TMP component carrying the item / panel title (test introspection).</summary>
    public TextMeshProUGUI NameText => _nameText;

    /// <summary>TMP component carrying the description body (test introspection).</summary>
    public TextMeshProUGUI DescText => _descText;

    /// <summary>TMP component carrying the machine prose (test introspection).</summary>
    public TextMeshProUGUI MachineProse => _machineProse;

    /// <summary>TMP component carrying the recipe summary line (test introspection).</summary>
    public TextMeshProUGUI RecipeSummary => _recipeSummary;

    /// <summary>TMP component carrying tool/weapon stats (test introspection).</summary>
    public TextMeshProUGUI StatsText => _statsText;

    /// <summary>Row of category / kind badges (test introspection).</summary>
    public RectTransform BadgesRow => _badgesRow;

    /// <summary>Row of property tags (test introspection).</summary>
    public RectTransform PropsRow => _propsRow;

    // ---------------------------------------------------------------
    //  Internal — item path
    // ---------------------------------------------------------------

    private void ShowItem(ItemDefinition item, bool useMouse, Vector2 screenPosition)
    {
        EnsureBuilt();

        // Name
        SetText(_nameText, !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.itemId);
        _nameText.enabled = true;

        // Badges — kind + (source if Source) + categories
        ClearChildren(_badgesRow);
        AddBadge(_badgesRow, item.kind.ToString(), UIStyle.AccentDim);
        if (item.kind == ItemKind.Source && item.source != ItemSource.None)
        {
            AddBadge(_badgesRow, item.source.ToString(), UIStyle.AccentDim);
        }
        if (item.categories != null)
        {
            for (int i = 0; i < item.categories.Length; i++)
            {
                string cat = item.categories[i];
                if (string.IsNullOrEmpty(cat)) continue;
                AddBadge(_badgesRow, cat, UIStyle.AccentDim);
            }
        }
        _badgesRow.gameObject.SetActive(_badgesRow.childCount > 0);

        // Properties — color-coded
        ClearChildren(_propsRow);
        if (item.properties != null && item.properties.Length > 0)
        {
            for (int i = 0; i < item.properties.Length; i++)
            {
                MaterialProperties p = item.properties[i];
                Color tint = PropertyTint(p);
                AddBadge(_propsRow, p.ToString(), tint);
            }
            _propsRow.gameObject.SetActive(true);
        }
        else
        {
            _propsRow.gameObject.SetActive(false);
        }

        // Description / role
        if (string.IsNullOrEmpty(item.description))
        {
            _descText.text = string.Empty;
            _descText.enabled = false;
            _descText.gameObject.SetActive(false);
        }
        else
        {
            _descText.text = item.description;
            _descText.enabled = true;
            _descText.gameObject.SetActive(true);
        }

        // Machine prose (only when this item IS a machine)
        bool isMachine = item.kind == ItemKind.Machine;
        if (isMachine && !string.IsNullOrEmpty(item.itemId))
        {
            MachineDefinition machine = ResolveMachine(item.itemId);
            if (machine != null)
            {
                ConfigureMachineProse(machine);
                _machineRow.gameObject.SetActive(true);
            }
            else
            {
                _machineRow.gameObject.SetActive(false);
            }
        }
        else
        {
            _machineRow.gameObject.SetActive(false);
        }

        // Recipe summary
        int recipeCount = CountRecipes(item.itemId);
        if (recipeCount >= 1)
        {
            _recipeSummary.text = recipeCount == 1
                ? "Made via 1 recipe"
                : $"Made via {recipeCount} recipes";
            _recipeSummary.enabled = true;
            _recipeSummary.gameObject.SetActive(true);
        }
        else
        {
            _recipeSummary.text = string.Empty;
            _recipeSummary.gameObject.SetActive(false);
        }

        // Stats — tool / weapon / mining tier / damage / etc.
        string stats = BuildStatsLine(item);
        if (!string.IsNullOrEmpty(stats))
        {
            _statsText.text = stats;
            _statsText.enabled = true;
            _statsText.gameObject.SetActive(true);
        }
        else
        {
            _statsText.text = string.Empty;
            _statsText.gameObject.SetActive(false);
        }

        Reveal(useMouse, screenPosition);
    }

    // ---------------------------------------------------------------
    //  Internal — machine family explainer path
    // ---------------------------------------------------------------

    private void ShowMachineFamily(MachineDefinition machine, Vector2 screenPosition)
    {
        EnsureBuilt();

        SetText(_nameText, !string.IsNullOrEmpty(machine.displayName) ? machine.displayName : machine.itemId);
        _nameText.enabled = true;

        ClearChildren(_badgesRow);
        AddBadge(_badgesRow, machine.processType.ToString(), ProcessTypeTint(machine.processType));
        _badgesRow.gameObject.SetActive(true);

        ClearChildren(_propsRow);
        _propsRow.gameObject.SetActive(false);

        string family = ProcessTypeFamilyExplanation(machine.processType);
        _descText.text = family;
        _descText.enabled = true;
        _descText.gameObject.SetActive(!string.IsNullOrEmpty(family));

        ConfigureMachineProse(machine);
        _machineRow.gameObject.SetActive(true);

        int recipeCount = CountRecipes(machine.itemId);
        if (recipeCount >= 1)
        {
            _recipeSummary.text = recipeCount == 1 ? "1 recipe" : $"{recipeCount} recipes";
            _recipeSummary.gameObject.SetActive(true);
        }
        else
        {
            _recipeSummary.gameObject.SetActive(false);
        }

        _statsText.text = string.Empty;
        _statsText.gameObject.SetActive(false);

        Reveal(useMouse: false, screenPosition: screenPosition);
    }

    // ---------------------------------------------------------------
    //  Internal — property tooltip path
    // ---------------------------------------------------------------

    private void ShowProperty(MaterialProperties property, Vector2 screenPosition)
    {
        EnsureBuilt();

        SetText(_nameText, property.ToString());
        _nameText.enabled = true;
        _nameText.color = PropertyTint(property);

        ClearChildren(_badgesRow);
        _badgesRow.gameObject.SetActive(false);

        ClearChildren(_propsRow);
        _propsRow.gameObject.SetActive(false);

        _descText.text = PropertyDescriptions.GetDescription(property);
        _descText.enabled = true;
        _descText.gameObject.SetActive(true);

        _machineRow.gameObject.SetActive(false);
        _recipeSummary.gameObject.SetActive(false);
        _statsText.gameObject.SetActive(false);

        Reveal(useMouse: false, screenPosition: screenPosition);

        // Restore name colour for subsequent Show paths.
        _nameText.color = UIStyle.Text;
    }

    // ---------------------------------------------------------------
    //  Internal — generic title/body path
    // ---------------------------------------------------------------

    private void ShowGeneric(string title, string body, Vector2 screenPosition)
    {
        EnsureBuilt();

        SetText(_nameText, title ?? string.Empty);
        _nameText.enabled = !string.IsNullOrEmpty(title);

        ClearChildren(_badgesRow);
        _badgesRow.gameObject.SetActive(false);

        ClearChildren(_propsRow);
        _propsRow.gameObject.SetActive(false);

        _descText.text = body ?? string.Empty;
        _descText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        _descText.enabled = !string.IsNullOrEmpty(body);

        _machineRow.gameObject.SetActive(false);
        _recipeSummary.gameObject.SetActive(false);
        _statsText.gameObject.SetActive(false);

        Reveal(useMouse: false, screenPosition: screenPosition);
    }

    // ---------------------------------------------------------------
    //  Internal — common helpers
    // ---------------------------------------------------------------

    private void Reveal(bool useMouse, Vector2 screenPosition)
    {
        gameObject.SetActive(true);
        _visible    = true;
        _fadeTimer  = 0f;
        _group.alpha = 0f;
        _followMouse = useMouse;

        if (useMouse) FollowMouse();
        else PositionAt(screenPosition);
    }

    private void HideInternal()
    {
        _visible = false;
        _followMouse = false;
        gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;
        PositionAt(Mouse.current.position.ReadValue());
    }

    private void PositionAt(Vector2 screenPosition)
    {
        Vector2 target = screenPosition + new Vector2(CursorOffsetX, CursorOffsetY);
        Vector2 size = _rectTransform.sizeDelta;

        float maxX = Screen.width  - size.x - 5f;
        float maxY = Screen.height - 5f;
        float minX = 5f;
        float minY = size.y + 5f;

        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.y = Mathf.Clamp(target.y, minY, maxY);

        _rectTransform.position = new Vector3(target.x, target.y, 0f);
    }

    private static MachineDefinition ResolveMachine(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        MachineRegistry reg = MachineRegistry.Instance;
        return reg != null ? reg.GetById(itemId) : null;
    }

    private void ConfigureMachineProse(MachineDefinition machine)
    {
        if (machine == null) return;

        string sentence = FirstSentence(machine.howItWorks);
        _machineProse.text = sentence ?? string.Empty;
        _machineProse.enabled = !string.IsNullOrEmpty(sentence);
        _machineProse.color = UIStyle.TextDim;
    }

    private static int CountRecipes(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        RecipeRegistry reg = RecipeRegistry.Instance;
        if (reg == null) return 0;

        int n = 0;
        foreach (var _ in reg.ByOutput(itemId)) n++;
        return n;
    }

    private static string FirstSentence(string prose)
    {
        if (string.IsNullOrEmpty(prose)) return string.Empty;
        // Truncate at the first period / em-dash / newline so the tooltip
        // stays compact even when the howItWorks paragraph is multi-clause.
        for (int i = 0; i < prose.Length; i++)
        {
            char c = prose[i];
            if (c == '.' || c == '\n')
            {
                return prose.Substring(0, i + 1).Trim();
            }
        }
        return prose.Length > 120 ? prose.Substring(0, 120).Trim() + "..." : prose;
    }

    private static Color PropertyTint(MaterialProperties p)
    {
        string name = p.ToString();
        if (name.StartsWith("Combustible_", StringComparison.Ordinal))
            // Warm orange — lerp Accent toward red so the tag reads as "fire".
            return Color.Lerp(UIStyle.Accent, new Color(1f, 0.4f, 0.2f, 1f), 0.7f);
        if (name.StartsWith("Liquid_", StringComparison.Ordinal))
            return new Color(0.45f, 0.75f, 1f, 1f);   // blue
        if (name.StartsWith("Organic_", StringComparison.Ordinal))
            return UIStyle.TextSuccess;               // green
        if (name.StartsWith("Solid_", StringComparison.Ordinal))
            return UIStyle.Text;                      // neutral
        if (p == MaterialProperties.Conducts_Electric
            || p == MaterialProperties.Crystalline
            || p == MaterialProperties.Magical)
            return UIStyle.Accent;
        return UIStyle.Text;
    }

    private static Color ProcessTypeTint(MachineProcessType type)
    {
        if (type == MachineProcessType.Hybrid_Crafting) return UIStyle.Accent;
        string label = type.ToString();
        if (label.StartsWith("Forgiving_", StringComparison.Ordinal)) return UIStyle.TextSuccess;
        if (label.StartsWith("Picky_",     StringComparison.Ordinal)) return UIStyle.TextError;
        return UIStyle.Text;
    }

    private static string ProcessTypeFamilyExplanation(MachineProcessType type)
    {
        string label = type.ToString();
        if (type == MachineProcessType.Hybrid_Crafting)
            return "Hybrid: specific recipes plus improvised property-matched substitutes (output capped at 0.7x).";
        if (label.StartsWith("Forgiving_", StringComparison.Ordinal))
            return "Forgiving: accepts any input whose properties match, with efficiency scaled to how good the fit is.";
        if (label.StartsWith("Picky_", StringComparison.Ordinal))
            return "Picky: refuses substitutes. Only hand-authored recipes work here.";
        return string.Empty;
    }

    private static string BuildStatsLine(ItemDefinition item)
    {
        // Tool / weapon / vehicle / segment / etc. — preserves the V11 surface
        // while letting non-typed items fall through to a generic line.
        switch (item)
        {
            case Voidborne.Vehicles.VehiclePartItem vpi:
                return $"Part: {vpi.partType}   Section: {vpi.fitsSection}   Max HP: {vpi.maxCondition:0}";

            case Voidborne.Automation.SegmentItem seg:
            {
                var parts = new List<string>(4);
                if (!string.IsNullOrEmpty(seg.speedLabel)) parts.Add($"Speed: {seg.speedLabel}");
                parts.Add($"Max length: {seg.maxLength:0} m");
                if (seg.powerDrawWatts > 0f) parts.Add($"Draw: {seg.powerDrawWatts:0} W");
                if (seg.capacity > 0)        parts.Add($"Buffer: {seg.capacity} items");
                return string.Join("   ", parts);
            }

            case Voidborne.Automation.AutomationItem ai:
            {
                var parts = new List<string>(4);
                if (!string.IsNullOrEmpty(ai.speedLabel)) parts.Add($"Speed: {ai.speedLabel}");
                else if (ai.tickInterval > 0f)           parts.Add($"Tick: {ai.tickInterval:0.00} s");
                if (ai.powerDrawWatts > 0f) parts.Add($"Draw: {ai.powerDrawWatts:0} W");
                if (ai.capacity > 0)        parts.Add($"Buffer: {ai.capacity} items");
                return parts.Count > 0 ? string.Join("   ", parts) : "Automation device";
            }

            case DevBackpackItem _:
                return "INFINITE   All items   Never depletes";

            case BackpackItem bpDef:
            {
                int slots = bpDef.extraRows * bpDef.extraColumns;
                return $"Storage: {bpDef.extraColumns}x{bpDef.extraRows}  ({slots} slots)   Worn slot or Hotbar";
            }

            case StorageItem sti:
            {
                var parts = new List<string>(3);
                parts.Add($"Storage: {sti.cols}x{sti.rows} ({sti.SlotCount} slots)");
                if (sti.stackMultiplier > 1) parts.Add($"Stacks to {sti.stackMultiplier}x");
                if (sti.powerDrawWatts > 0f) parts.Add($"Draw: {sti.powerDrawWatts:0} W");
                return string.Join("   ", parts);
            }

            case PlaceableItem plc:
                return $"{plc.categoryLabel}   Select + Click to place";

            case Voidborne.Building.Electricity.ElectricityItem ei:
            {
                string output = ei.outputWatts > 0f ? $"Output: {ei.outputWatts:0} W   " : "";
                string draw   = ei.drawWatts   > 0f ? $"Draw: {ei.drawWatts:0} W" : "";
                string s = (output + draw).Trim();
                return string.IsNullOrEmpty(s) ? "Passive device" : s;
            }

            case Voidborne.Automation.NetworkCableItem nci:
                return $"Data cable   Max length: {nci.maxLength:0} m   Stack: {nci.maxStackSize}";

            case Voidborne.Building.Electricity.WireItem wri:
            {
                string throughput = wri.maxThroughputWatts > 0
                    ? $"Max throughput: {wri.maxThroughputWatts:0} W   "
                    : "";
                return $"{throughput}Max length: {wri.maxLength:0} m   Stack: {wri.maxStackSize}";
            }

            case Voidborne.Building.Electricity.PowerProbeItem ppi:
                return $"Scan range: {ppi.scanRange:0} m";

            case BuildingPieceItem bpi when bpi.pieceData != null:
            {
                var data = bpi.pieceData;
                int tierIdx = (int)bpi.materialTier;
                float hp = (data.healthPerTier != null && tierIdx < data.healthPerTier.Length)
                    ? data.healthPerTier[tierIdx] : 150f;
                return $"Type: {data.pieceType}   Tier: {bpi.materialTier}   HP: {hp:0}";
            }

            case ToolDefinition toolDef:
                return $"Tier: {toolDef.toolTier}   Speed: {toolDef.miningSpeedMultiplier:0.0}x   Durability: {toolDef.maxDurability}";

            case WeaponItem wi when wi.meleeDefinition != null:
            {
                var m = wi.meleeDefinition;
                string pen = m.armorPenetration > 0f ? $"   Pen: {m.armorPenetration * 100f:0}%" : "";
                return $"DMG: {m.damage:0}   Range: {m.range:0.0}m   Speed: {m.attackSpeed:0.0}x{pen}";
            }

            case WeaponItem gwi when gwi.gunDefinition != null:
            {
                var g = gwi.gunDefinition;
                return $"DMG: {g.damage:0}   RPM: {g.fireRate:0}   Mag: {g.magazineSize}";
            }

            case WeaponItem bwi when bwi.bowDefinition != null:
            {
                var b = bwi.bowDefinition;
                return $"Draw: {b.fullDrawTime:0.0}s   Spd: {b.minVelocity:0}-{b.maxVelocity:0} m/s   Zoom: {b.drawZoom:0.00}x";
            }

            case WeaponItem twi when twi.throwableDefinition != null:
            {
                var t = twi.throwableDefinition;
                string spin = t.tumbles ? "Tumbles" : "Stable";
                return $"Windup: {t.windupTime:0.0}s   Spd: {t.throwVelocity:0} m/s   {spin}";
            }
        }

        // Generic — weight + stack only when not redundant with everything else.
        if (item.weight > 0f || item.maxStackSize > 0)
        {
            return $"Weight: {item.weight:0.0}   Max Stack: {item.maxStackSize}";
        }
        return string.Empty;
    }

    private static void SetText(TextMeshProUGUI tmp, string content)
    {
        if (tmp == null) return;
        tmp.text = content ?? string.Empty;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null) GameObject.DestroyImmediate(child.gameObject);
        }
    }

    private static void AddBadge(RectTransform parent, string label, Color borderColor)
    {
        if (parent == null || string.IsNullOrEmpty(label)) return;

        GameObject badge = new GameObject($"Badge_{label}",
            typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(parent, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        badge.layer = uiLayer >= 0 ? uiLayer : 5;

        Image bg = badge.GetComponent<Image>();
        bg.color = UIStyle.PanelLight;
        bg.raycastTarget = false;

        RectTransform brt = badge.GetComponent<RectTransform>();
        brt.sizeDelta = new Vector2(60f, 16f);

        UIBuilder.Border(brt, borderColor);

        TextMeshProUGUI tmp = UIBuilder.Text(
            badge.transform, label,
            UIStyle.FontSizeSmall, borderColor, "Label");
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = false;
        RectTransform lrt = tmp.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(4f, 0f);
        lrt.offsetMax = new Vector2(-4f, 0f);

        // Let the badge sized by content via preferred-width.
        ContentSizeFitter csf = badge.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement le = badge.AddComponent<LayoutElement>();
        le.preferredHeight = 16f;
        le.minHeight       = 16f;
        // Allow the layout group to measure the TMP preferredWidth.
        le.minWidth = 24f;
    }

    // ---------------------------------------------------------------
    //  One-time scaffolding (idempotent — also usable from EditMode tests)
    // ---------------------------------------------------------------

    /// <summary>
    /// Idempotent build hook. EditMode tests that <c>AddComponent</c> this
    /// MonoBehaviour directly can call EnsureBuilt to drive scaffolding
    /// without triggering Awake.
    /// </summary>
    public void EnsureBuilt()
    {
        // Idempotent — also claim the singleton if no other instance owns it.
        // Awake does the same, but EditMode AddComponent does not invoke Awake.
        if (Instance == null) Instance = this;
        if (_rectTransform != null) return;
        Build();
    }

    private void Build()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot     = Vector2.zero;
        _rectTransform.sizeDelta = new Vector2(PanelWidth, PanelMinHeight);

        // Panel background + border.
        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = UIStyle.Panel;
        bg.raycastTarget = false;
        UIBuilder.Border(_rectTransform, UIStyle.Border);

        // Vertical content layout — auto-sized by ContentSizeFitter.
        _layout = gameObject.AddComponent<VerticalLayoutGroup>();
        _layout.childControlWidth      = true;
        _layout.childControlHeight     = true;
        _layout.childForceExpandWidth  = true;
        _layout.childForceExpandHeight = false;
        _layout.spacing = 4f;
        _layout.padding = new RectOffset(
            UIStyle.PanelPadding, UIStyle.PanelPadding,
            UIStyle.PanelPadding, UIStyle.PanelPadding);

        _fitter = gameObject.AddComponent<ContentSizeFitter>();
        _fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        _fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Name.
        _nameText = UIBuilder.Text(transform, string.Empty,
            UIStyle.FontSizeHeader, UIStyle.Text, "Name");
        _nameText.alignment = TextAlignmentOptions.Left;
        _nameText.fontStyle = FontStyles.Bold;
        AddLayout(_nameText.gameObject, 18f);

        // Badges row.
        _badgesRow = BuildBadgesRow("BadgesRow");

        // Properties row.
        _propsRow  = BuildBadgesRow("PropsRow");

        // Description.
        _descText = UIBuilder.Text(transform, string.Empty,
            UIStyle.FontSizeBody, UIStyle.TextDim, "Desc");
        _descText.alignment = TextAlignmentOptions.TopLeft;
        _descText.textWrappingMode = TextWrappingModes.Normal;
        AddLayout(_descText.gameObject, 30f);

        // Machine row (badge + prose) — built as a vertical mini-stack.
        _machineRow = BuildMachineRow();

        // Recipe summary.
        _recipeSummary = UIBuilder.Text(transform, string.Empty,
            UIStyle.FontSizeSmall, UIStyle.TextDim, "RecipeSummary");
        _recipeSummary.alignment = TextAlignmentOptions.Left;
        _recipeSummary.fontStyle = FontStyles.Italic;
        AddLayout(_recipeSummary.gameObject, 14f);

        // Stats.
        _statsText = UIBuilder.Text(transform, string.Empty,
            UIStyle.FontSizeSmall, UIStyle.TextDim, "Stats");
        _statsText.alignment = TextAlignmentOptions.Left;
        _statsText.textWrappingMode = TextWrappingModes.Normal;
        AddLayout(_statsText.gameObject, 14f);

        // All optional sections start hidden — Show* paths flip them on.
        _badgesRow.gameObject.SetActive(false);
        _propsRow.gameObject.SetActive(false);
        _descText.gameObject.SetActive(false);
        _machineRow.gameObject.SetActive(false);
        _recipeSummary.gameObject.SetActive(false);
        _statsText.gameObject.SetActive(false);
    }

    private RectTransform BuildBadgesRow(string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        row.layer = uiLayer >= 0 ? uiLayer : 5;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 4f;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight       = 16f;
        le.preferredHeight = 16f;
        le.flexibleHeight  = 0f;

        return row.GetComponent<RectTransform>();
    }

    private RectTransform BuildMachineRow()
    {
        GameObject row = new GameObject("MachineRow", typeof(RectTransform));
        row.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        row.layer = uiLayer >= 0 ? uiLayer : 5;

        VerticalLayoutGroup vlg = row.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight       = 14f;
        le.preferredHeight = 30f;

        _machineProse = UIBuilder.Text(row.transform, string.Empty,
            UIStyle.FontSizeSmall, UIStyle.TextDim, "HowItWorks");
        _machineProse.alignment = TextAlignmentOptions.TopLeft;
        _machineProse.textWrappingMode = TextWrappingModes.Normal;
        AddLayout(_machineProse.gameObject, 24f);

        return row.GetComponent<RectTransform>();
    }

    private static void AddLayout(GameObject go, float preferredHeight)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;
        le.flexibleHeight  = 0f;
    }
}
