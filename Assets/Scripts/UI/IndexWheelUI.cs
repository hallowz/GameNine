using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Radial ability-selection wheel opened by holding Tab.
///
/// Layout:
///   6 ability nodes arranged in a circle, spaced 60° apart.
///   Module 0 at top (12 o'clock), proceeding clockwise.
///
///   A direction line is anchored at screen centre and rotates to follow
///   the mouse cursor. The node nearest to the mouse direction is
///   highlighted. Releasing Tab confirms the selection.
///
/// Time slow:
///   While the wheel is open Time.timeScale is set to WheelTimeScale.
///   This is independent of The Interval ability — they don't stack
///   (wheel scale takes priority while open).
///
/// Locked nodes (module not yet installed) are shown greyed-out and
/// cannot be selected.
/// </summary>
public class IndexWheelUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------

    public const float WheelTimeScale      = 0.12f;  // "significant" slow while wheel open
    private const float NodeRadius         = 130f;   // px from screen centre to node centre
    private const float LineLength         = NodeRadius - 10f;
    private const float LineWidth          = 3f;
    private const int   NodeCount          = 6;
    private const float NodeSize           = 80f;    // px

    // Module names and keys shown in the wheel.
    private static readonly string[] ModuleNames =
    {
        "TETHER",        // 0
        "SHARD INDEX",   // 1
        "INTERVAL",      // 2
        "CONDUCTOR",     // 3
        "THRESHOLD",     // 4
        "THE FOLD",      // 5
    };

    private static readonly string[] ModuleKeys =
    {
        "grapple", "x-ray scan", "time slow", "arc bolt", "phase out", "open portal"
    };

    // ---------------------------------------------------------------
    //  Colors
    // ---------------------------------------------------------------

    private static readonly Color ColBackground    = new(0f,    0f,    0f,    0.55f);
    private static readonly Color ColNodeActive    = new(0.15f, 0.45f, 0.65f, 0.92f);
    private static readonly Color ColNodeLocked    = new(0.12f, 0.12f, 0.15f, 0.7f);
    private static readonly Color ColNodeSelected  = new(0.25f, 0.75f, 1f,    1f);
    private static readonly Color ColNodeOnCD      = new(0.08f, 0.25f, 0.38f, 0.85f);
    private static readonly Color ColLine          = new(0.35f, 0.9f,  1f,    0.95f);
    private static readonly Color ColText          = new(0.85f, 0.92f, 1f,    1f);
    private static readonly Color ColTextLocked    = new(0.35f, 0.35f, 0.4f,  0.8f);
    private static readonly Color ColCooldownFill  = new(0.55f, 0.8f,  1f,    0.35f);
    private static readonly Color ColSelected      = new(0.4f,  0.95f, 1f,    1f);

    // ---------------------------------------------------------------
    //  Runtime references
    // ---------------------------------------------------------------

    private IndexDevice    _device;
    private IndexAbilities _abilities;

    private Canvas          _canvas;
    private GameObject      _root;
    private RectTransform   _lineRect;
    private GameObject[]    _nodes     = new GameObject[NodeCount];
    private Image[]         _nodeBgs   = new Image[NodeCount];
    private Image[]         _cdFills   = new Image[NodeCount];
    private Text[]          _nameTexts = new Text[NodeCount];
    private Text[]          _keyTexts  = new Text[NodeCount];
    private Image           _lineDot;   // small circle at centre

    private bool            _open;
    private int             _hoveredModule = -1;
    private float           _savedTimeScale;

    // ---------------------------------------------------------------
    //  Events
    // ---------------------------------------------------------------

    /// <summary>Fired when the player confirms a selection (module index, or -1 if none).</summary>
    public event Action<int> OnModuleSelected;

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    public bool IsOpen => _open;
    public int  HoveredModule => _hoveredModule;

    public void Initialize(IndexDevice device, IndexAbilities abilities)
    {
        _device    = device;
        _abilities = abilities;
        BuildUI();
        _root.SetActive(false);
    }

    /// <summary>Open the wheel and slow time.</summary>
    public void Open()
    {
        if (_open) return;
        _open = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale  = WheelTimeScale;
        Time.fixedDeltaTime = 0.02f * WheelTimeScale;
        _root.SetActive(true);
        RefreshNodes();
        UnityEngine.Cursor.visible   = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>Close the wheel, restore time, and return the selected module (-1 if cancelled).</summary>
    public int Close()
    {
        if (!_open) return -1;
        _open = false;
        _root.SetActive(false);
        Time.timeScale  = _savedTimeScale;
        Time.fixedDeltaTime = 0.02f * _savedTimeScale;
        UnityEngine.Cursor.visible   = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        int selected = _hoveredModule;
        _hoveredModule = -1;
        return selected;
    }

    /// <summary>Call every frame while the wheel is open to update direction line and hovered node.</summary>
    public void Tick()
    {
        if (!_open) return;
        UpdateDirectionLine();
        RefreshHighlight();
    }

    // ---------------------------------------------------------------
    //  Directional selection
    // ---------------------------------------------------------------

    private void UpdateDirectionLine()
    {
        Vector2 screenCentre = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mousePos     = Mouse.current != null ? Mouse.current.position.ReadValue() : screenCentre;
        Vector2 delta        = mousePos - screenCentre;

        // Dead-zone: if mouse is very close to centre, no selection.
        if (delta.sqrMagnitude < 20f * 20f)
        {
            _hoveredModule = -1;
            SetLineActive(false);
            return;
        }

        SetLineActive(true);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Rotate the line rect: UI's default right = 0°; we want up=0°.
        _lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Determine which 60° sector the mouse falls in.
        // Module 0 is at top (90° in Unity screen space), then clockwise.
        // Convert angle to "clockwise from top" convention.
        float clockwiseFromTop = (90f - angle + 360f) % 360f;
        int   sector           = Mathf.RoundToInt(clockwiseFromTop / 60f) % NodeCount;
        _hoveredModule = sector;
    }

    private void SetLineActive(bool active)
    {
        if (_lineRect != null) _lineRect.gameObject.SetActive(active);
    }

    // ---------------------------------------------------------------
    //  Node refresh
    // ---------------------------------------------------------------

    private void RefreshNodes()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            bool installed = _device.IsModuleInstalled(i);
            bool onCd      = installed && _abilities.IsOnCooldown(i);
            bool selected  = i == _device.ActiveModuleIndex;

            _nodeBgs[i].color  = !installed ? ColNodeLocked
                               : selected    ? ColNodeActive
                               : onCd        ? ColNodeOnCD
                                             : ColNodeActive;

            _nameTexts[i].color = installed ? ColText : ColTextLocked;
            _keyTexts[i].color  = installed ? new Color(0.6f, 0.85f, 1f) : ColTextLocked;

            // Cooldown arc fill.
            if (_cdFills[i] != null)
            {
                _cdFills[i].fillAmount = onCd ? _abilities.GetCooldownRatio(i) : 0f;
            }
        }
    }

    private void RefreshHighlight()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            bool isHovered    = i == _hoveredModule;
            bool installed    = _device.IsModuleInstalled(i);
            bool canSelect    = isHovered && installed;

            _nodeBgs[i].color = canSelect                  ? ColNodeSelected
                              : i == _device.ActiveModuleIndex && installed ? ColNodeActive
                              : !installed                 ? ColNodeLocked
                                                           : ColNodeOnCD;
        }
    }

    // ---------------------------------------------------------------
    //  Build UI
    // ---------------------------------------------------------------

    private void BuildUI()
    {
        // Find or create a canvas for the wheel.
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        Transform canvasRoot  = existingCanvas != null ? existingCanvas.transform : MakeCanvas();

        // Root panel — full-screen, no raycast block (so it doesn't eat clicks).
        _root = new GameObject("IndexWheelUI", typeof(RectTransform));
        _root.transform.SetParent(canvasRoot, false);
        var rootRect      = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Dim overlay.
        var overlay     = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(_root.transform, false);
        var or          = overlay.GetComponent<RectTransform>();
        or.anchorMin    = Vector2.zero;
        or.anchorMax    = Vector2.one;
        or.offsetMin    = Vector2.zero;
        or.offsetMax    = Vector2.zero;
        overlay.GetComponent<Image>().color = ColBackground;

        // Centre anchor — used to position everything.
        var centreGO    = new GameObject("Centre", typeof(RectTransform));
        centreGO.transform.SetParent(_root.transform, false);
        var centreRect  = centreGO.GetComponent<RectTransform>();
        centreRect.anchorMin = new Vector2(0.5f, 0.5f);
        centreRect.anchorMax = new Vector2(0.5f, 0.5f);
        centreRect.pivot     = new Vector2(0.5f, 0.5f);
        centreRect.sizeDelta = Vector2.zero;

        // Direction line — a thin white rect rotated by angle.
        BuildDirectionLine(centreRect);

        // Centre dot.
        BuildCentreDot(centreRect);

        // 6 ability nodes.
        for (int i = 0; i < NodeCount; i++)
            BuildNode(i, centreRect);

        // "THE INDEX" label at top of wheel.
        BuildLabel(centreRect);
    }

    private void BuildDirectionLine(RectTransform parent)
    {
        var go         = new GameObject("DirectionLine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        _lineRect      = go.GetComponent<RectTransform>();
        // Pivot at left edge so rotation anchors at screen centre.
        _lineRect.pivot      = new Vector2(0f, 0.5f);
        _lineRect.anchorMin  = new Vector2(0.5f, 0.5f);
        _lineRect.anchorMax  = new Vector2(0.5f, 0.5f);
        _lineRect.sizeDelta  = new Vector2(LineLength, LineWidth);
        _lineRect.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = ColLine;
    }

    private void BuildCentreDot(RectTransform parent)
    {
        var go         = new GameObject("CentreDot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r          = go.GetComponent<RectTransform>();
        r.anchorMin    = new Vector2(0.5f, 0.5f);
        r.anchorMax    = new Vector2(0.5f, 0.5f);
        r.sizeDelta    = new Vector2(12f, 12f);
        r.anchoredPosition = Vector2.zero;
        _lineDot       = go.GetComponent<Image>();
        _lineDot.color = ColLine;
    }

    private void BuildNode(int index, RectTransform parent)
    {
        // Angle: module 0 at top (90°), clockwise → subtract 60° per index.
        float angleDeg  = 90f - index * 60f;
        float angleRad  = angleDeg * Mathf.Deg2Rad;
        Vector2 nodePos = new(Mathf.Cos(angleRad) * NodeRadius, Mathf.Sin(angleRad) * NodeRadius);

        // Node container.
        var nodeGO         = new GameObject($"Node_{index}", typeof(RectTransform));
        nodeGO.transform.SetParent(parent, false);
        var nodeRect       = nodeGO.GetComponent<RectTransform>();
        nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
        nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
        nodeRect.sizeDelta = new Vector2(NodeSize, NodeSize);
        nodeRect.anchoredPosition = nodePos;

        // Background circle.
        var bgGO           = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(nodeGO.transform, false);
        var bgRect         = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin   = Vector2.zero;
        bgRect.anchorMax   = Vector2.one;
        bgRect.offsetMin   = Vector2.zero;
        bgRect.offsetMax   = Vector2.zero;
        _nodeBgs[index]    = bgGO.GetComponent<Image>();
        _nodeBgs[index].color = ColNodeLocked;

        // Cooldown fill (radial, drawn over bg).
        var cdGO           = new GameObject("CooldownFill", typeof(RectTransform), typeof(Image));
        cdGO.transform.SetParent(nodeGO.transform, false);
        var cdRect         = cdGO.GetComponent<RectTransform>();
        cdRect.anchorMin   = Vector2.zero;
        cdRect.anchorMax   = Vector2.one;
        cdRect.offsetMin   = new Vector2(3f, 3f);
        cdRect.offsetMax   = new Vector2(-3f, -3f);
        _cdFills[index]    = cdGO.GetComponent<Image>();
        _cdFills[index].color     = ColCooldownFill;
        _cdFills[index].type      = Image.Type.Filled;
        _cdFills[index].fillMethod = Image.FillMethod.Radial360;
        _cdFills[index].fillAmount = 0f;

        // Module name text.
        var nameGO         = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGO.transform.SetParent(nodeGO.transform, false);
        var nameRect       = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.45f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(2f, 0f);
        nameRect.offsetMax = new Vector2(-2f, -4f);
        _nameTexts[index]  = nameGO.GetComponent<Text>();
        _nameTexts[index].text      = ModuleNames[index];
        _nameTexts[index].font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _nameTexts[index].fontSize  = 11;
        _nameTexts[index].fontStyle = FontStyle.Bold;
        _nameTexts[index].alignment = TextAnchor.MiddleCenter;
        _nameTexts[index].color     = ColTextLocked;

        // Sub-key text.
        var keyGO          = new GameObject("Key", typeof(RectTransform), typeof(Text));
        keyGO.transform.SetParent(nodeGO.transform, false);
        var keyRect        = keyGO.GetComponent<RectTransform>();
        keyRect.anchorMin  = new Vector2(0f, 0f);
        keyRect.anchorMax  = new Vector2(1f, 0.5f);
        keyRect.offsetMin  = new Vector2(2f, 2f);
        keyRect.offsetMax  = new Vector2(-2f, 0f);
        _keyTexts[index]   = keyGO.GetComponent<Text>();
        _keyTexts[index].text      = ModuleKeys[index];
        _keyTexts[index].font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _keyTexts[index].fontSize  = 9;
        _keyTexts[index].alignment = TextAnchor.MiddleCenter;
        _keyTexts[index].color     = ColTextLocked;

        _nodes[index] = nodeGO;
    }

    private void BuildLabel(RectTransform parent)
    {
        var go         = new GameObject("WheelLabel", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var r          = go.GetComponent<RectTransform>();
        r.anchorMin    = new Vector2(0.5f, 0.5f);
        r.anchorMax    = new Vector2(0.5f, 0.5f);
        r.sizeDelta    = new Vector2(160f, 20f);
        r.anchoredPosition = Vector2.zero;

        var txt        = go.GetComponent<Text>();
        txt.text       = "THE INDEX";
        txt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize   = 11;
        txt.color      = new Color(0.45f, 0.65f, 0.75f, 0.7f);
        txt.alignment  = TextAnchor.MiddleCenter;
        txt.fontStyle  = FontStyle.Bold;
    }

    private Transform MakeCanvas()
    {
        var go = new GameObject("WheelCanvas");
        var c  = go.AddComponent<Canvas>();
        c.renderMode  = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 150;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }
}
