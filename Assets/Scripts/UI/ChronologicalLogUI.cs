using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Readable journal panel opened at The Desk in the HOME pocket dimension.
///
/// Displays all collected Architect log entries in chronological order with timestamps.
/// Unread entries are highlighted. The tone of entries shifts visibly over time —
/// confident/precise early, terse mid, short and frightened late, final entry cuts off.
///
/// Usage:
///   1. Call AddEntry() at runtime whenever the player finds a log fragment.
///   2. Call Show() / Hide() to open or close the panel.
///   3. The panel is built entirely in code — no prefab required.
///
/// The UI is parented to the existing UIManager canvas if found, otherwise to a new canvas.
/// </summary>
public class ChronologicalLogUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Data types
    // ---------------------------------------------------------------

    [Serializable]
    public class ArchitectLogEntry
    {
        public string entryId;
        public string timestamp;      // e.g. "Day 1 — 06:41"
        [TextArea(2, 8)]
        public string text;
        public bool   isFinalEntry;   // final entry text is displayed truncated mid-sentence
        [NonSerialized] public bool isRead;
    }

    // ---------------------------------------------------------------
    //  Inspector — seed entries for hand-authored logs
    // ---------------------------------------------------------------

    [SerializeField] private List<ArchitectLogEntry> seededEntries = new();

    // ---------------------------------------------------------------
    //  Colors / style
    // ---------------------------------------------------------------

    private static readonly Color ColBackground  = new Color(0.04f, 0.04f, 0.06f, 0.96f);
    private static readonly Color ColHeader      = new Color(0.55f, 0.85f, 1f);
    private static readonly Color ColUnread      = new Color(0.9f, 0.95f, 1f);
    private static readonly Color ColRead        = new Color(0.5f, 0.55f, 0.6f);
    private static readonly Color ColTimestamp   = new Color(0.45f, 0.65f, 0.55f);
    private static readonly Color ColTruncated   = new Color(0.8f, 0.3f, 0.3f);   // final entry cut-off indicator

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------

    private readonly List<ArchitectLogEntry> _entries   = new();
    private GameObject    _panel;
    private ScrollRect    _scrollRect;
    private RectTransform _contentRect;
    private bool          _visible;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        BuildPanel();

        // Add seeded entries without marking them as read.
        foreach (var e in seededEntries)
            AddEntry(e, suppressSort: true);

        SortEntries();
        _panel.SetActive(false);
    }

    private void Update()
    {
        try
        {
            if (_visible && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }
        catch { }
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>Add a new log entry. Rebuilds the scroll list.</summary>
    public void AddEntry(ArchitectLogEntry entry, bool suppressSort = false)
    {
        if (_entries.Exists(e => e.entryId == entry.entryId)) return;
        _entries.Add(entry);
        if (!suppressSort) SortEntries();
        if (_visible) RebuildList();
    }

    public void Show()
    {
        _panel.SetActive(true);
        _visible = true;
        RebuildList();
        Debug.Log("[ChronologicalLogUI] Journal opened.");
    }

    public void Hide()
    {
        _panel.SetActive(false);
        _visible = false;
    }

    public bool IsVisible => _visible;

    // ---------------------------------------------------------------
    //  Build panel
    // ---------------------------------------------------------------

    private void BuildPanel()
    {
        // Find or create canvas.
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform canvasT = canvas != null ? canvas.transform : CreateCanvas();

        // Backing panel — full-screen dark overlay.
        _panel                = new GameObject("ChronologicalLogUI", typeof(RectTransform));
        _panel.transform.SetParent(canvasT, false);
        var panelRect         = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin   = new Vector2(0.1f, 0.05f);
        panelRect.anchorMax   = new Vector2(0.9f, 0.95f);
        panelRect.offsetMin   = Vector2.zero;
        panelRect.offsetMax   = Vector2.zero;

        var bg = _panel.AddComponent<Image>();
        bg.color = ColBackground;

        // Header label.
        var headerGO  = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(_panel.transform, false);
        var headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.anchorMin  = new Vector2(0f, 0.92f);
        headerRect.anchorMax  = new Vector2(1f, 1f);
        headerRect.offsetMin  = new Vector2(16f, 0f);
        headerRect.offsetMax  = new Vector2(-16f, 0f);

        var headerText        = headerGO.AddComponent<Text>();
        headerText.text       = "ARCHITECT LOG — CHRONOLOGICAL INDEX";
        headerText.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize   = 18;
        headerText.fontStyle  = FontStyle.Bold;
        headerText.color      = ColHeader;
        headerText.alignment  = TextAnchor.MiddleCenter;

        // Close button.
        BuildCloseButton(_panel.transform);

        // Scroll view.
        BuildScrollView(_panel.transform);
    }

    private void BuildCloseButton(Transform parent)
    {
        var go = new GameObject("CloseBtn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r   = go.GetComponent<RectTransform>();
        r.anchorMin  = new Vector2(0.9f, 0.92f);
        r.anchorMax  = new Vector2(1f, 1f);
        r.offsetMin  = new Vector2(-8f, 4f);
        r.offsetMax  = new Vector2(-8f, -4f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.1f, 0.1f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Hide);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;

        var txt       = label.AddComponent<Text>();
        txt.text      = "CLOSE  [ESC]";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 12;
        txt.color     = ColUnread;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private void BuildScrollView(Transform parent)
    {
        // Viewport.
        var viewportGO     = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewportGO.transform.SetParent(parent, false);
        var viewRect       = viewportGO.GetComponent<RectTransform>();
        viewRect.anchorMin = new Vector2(0f, 0f);
        viewRect.anchorMax = new Vector2(1f, 0.91f);
        viewRect.offsetMin = new Vector2(8f, 8f);
        viewRect.offsetMax = new Vector2(-8f, 0f);
        viewportGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewportGO.GetComponent<Mask>().showMaskGraphic = false;

        // Content container.
        var contentGO      = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        _contentRect       = contentGO.GetComponent<RectTransform>();
        _contentRect.anchorMin  = new Vector2(0f, 1f);
        _contentRect.anchorMax  = new Vector2(1f, 1f);
        _contentRect.pivot      = new Vector2(0.5f, 1f);
        _contentRect.offsetMin  = Vector2.zero;
        _contentRect.offsetMax  = Vector2.zero;

        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.padding    = new RectOffset(12, 12, 8, 8);
        layout.spacing    = 10f;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight     = true;

        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect.
        var scrollGO = new GameObject("ScrollRect", typeof(RectTransform));
        scrollGO.transform.SetParent(parent, false);
        var sr = scrollGO.GetComponent<RectTransform>();
        sr.anchorMin = viewRect.anchorMin;
        sr.anchorMax = viewRect.anchorMax;
        sr.offsetMin = viewRect.offsetMin;
        sr.offsetMax = viewRect.offsetMax;

        _scrollRect              = viewportGO.AddComponent<ScrollRect>();
        _scrollRect.content      = _contentRect;
        _scrollRect.viewport     = viewRect;
        _scrollRect.horizontal   = false;
        _scrollRect.vertical     = true;
        _scrollRect.scrollSensitivity = 30f;
    }

    private Transform CreateCanvas()
    {
        var go = new GameObject("LogCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    // ---------------------------------------------------------------
    //  List rebuild
    // ---------------------------------------------------------------

    private void RebuildList()
    {
        // Clear existing children.
        foreach (Transform child in _contentRect)
            Destroy(child.gameObject);

        if (_entries.Count == 0)
        {
            AddRow("No entries found.", ColRead, 14, false);
            return;
        }

        foreach (var entry in _entries)
        {
            bool isUnread = !entry.isRead;

            // Timestamp line.
            AddRow(entry.timestamp, ColTimestamp, 11, false);

            // Entry text.
            string displayText = entry.isFinalEntry
                ? TruncateFinalEntry(entry.text)
                : entry.text;
            AddRow(displayText, isUnread ? ColUnread : ColRead, 13, true);

            // "—" truncation marker for final entry.
            if (entry.isFinalEntry)
                AddRow("— [SIGNAL LOST]", ColTruncated, 11, false);

            // Separator.
            AddSeparator();

            // Mark as read.
            entry.isRead = true;
        }

        // Scroll to top when opening.
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void AddRow(string text, Color color, int fontSize, bool wrapText)
    {
        var go         = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(_contentRect, false);
        var txt        = go.AddComponent<Text>();
        txt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize   = fontSize;
        txt.color      = color;
        txt.supportRichText = false;
        txt.horizontalOverflow = wrapText ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;
        txt.text       = text;

        go.AddComponent<LayoutElement>().minHeight = fontSize + 4f;
    }

    private void AddSeparator()
    {
        var go         = new GameObject("Sep", typeof(RectTransform));
        go.transform.SetParent(_contentRect, false);
        var img        = go.AddComponent<Image>();
        img.color      = new Color(0.2f, 0.3f, 0.35f, 0.6f);
        var le         = go.AddComponent<LayoutElement>();
        le.minHeight   = 1f;
        le.preferredHeight = 1f;
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private void SortEntries()
    {
        // Entries are stored in insertion order; isFinalEntry entries go last.
        // Stable sort: non-final entries first (preserve order), final entries last.
        _entries.Sort((a, b) =>
        {
            if (a.isFinalEntry && !b.isFinalEntry) return  1;
            if (!a.isFinalEntry && b.isFinalEntry) return -1;
            return 0;
        });
    }

    /// <summary>Cuts the final entry off mid-sentence to convey abrupt termination.</summary>
    private static string TruncateFinalEntry(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Find a natural mid-sentence cut: after roughly 60% of the text.
        int cutIndex = Mathf.RoundToInt(text.Length * 0.62f);

        // Snap to nearest word boundary.
        while (cutIndex < text.Length && text[cutIndex] != ' ' && text[cutIndex] != ',')
            cutIndex++;

        return cutIndex < text.Length
            ? text[..cutIndex]
            : text;
    }
}
