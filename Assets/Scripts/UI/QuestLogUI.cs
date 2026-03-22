using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full quest log panel opened with J.
/// Three tabs: Active | Completed | Available.
/// Each quest shows: name (bold for main story), description, all objectives with progress, rewards.
/// Managed and built by UIManager. Call Show()/Hide() to toggle.
/// </summary>
public class QuestLogUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Layout constants
    // ---------------------------------------------------------------

    private const float PanelW    = 680f;
    private const float PanelH    = 520f;
    private const float TabH      = 36f;
    private const float HeaderH   = 48f;
    private const float EntryH    = 90f;
    private const float EntryPad  = 8f;

    // ---------------------------------------------------------------
    //  Runtime
    // ---------------------------------------------------------------

    public bool IsOpen { get; private set; }

    private GameObject _root;
    private int        _activeTab; // 0=Active, 1=Completed, 2=Available
    private GameObject _contentRoot;
    private Button[]   _tabButtons;
    private Text[]     _tabLabels;

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    public void Show()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        RefreshTab(_activeTab);
    }

    public void Hide()
    {
        IsOpen = false;
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        BuildPanel();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        QuestManager.OnQuestAccepted    += _ => RefreshTab(_activeTab);
        QuestManager.OnQuestCompleted   += _ => RefreshTab(_activeTab);
        QuestManager.OnObjectiveUpdated += (_, __) => { if (_activeTab == 0) RefreshTab(0); };
    }

    private void OnDisable()
    {
        QuestManager.OnQuestAccepted    -= _ => RefreshTab(_activeTab);
        QuestManager.OnQuestCompleted   -= _ => RefreshTab(_activeTab);
        QuestManager.OnObjectiveUpdated -= (_, __) => { if (_activeTab == 0) RefreshTab(0); };
    }

    // ---------------------------------------------------------------
    //  Build
    // ---------------------------------------------------------------

    private void BuildPanel()
    {
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(PanelW, PanelH);

        // Dark background
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.12f, 0.95f);

        // Title bar
        var titleGO  = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(transform, false);
        var titleRt  = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0, 1);
        titleRt.anchorMax        = new Vector2(1, 1);
        titleRt.pivot            = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = Vector2.zero;
        titleRt.sizeDelta        = new Vector2(0, HeaderH);
        var titleBg  = titleGO.AddComponent<Image>();
        titleBg.color = new Color(0.1f, 0.05f, 0.2f, 1f);
        var titleTxt = new GameObject("Text", typeof(RectTransform));
        titleTxt.transform.SetParent(titleGO.transform, false);
        var tRt = titleTxt.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(12, 0); tRt.offsetMax = Vector2.zero;
        var tText = titleTxt.AddComponent<Text>();
        tText.text      = "QUEST LOG";
        tText.fontSize  = 18;
        tText.fontStyle = FontStyle.Bold;
        tText.color     = new Color(0.85f, 0.65f, 1f);
        tText.alignment = TextAnchor.MiddleLeft;

        // Close button
        BuildCloseButton();

        // Tabs
        BuildTabs();

        // Scroll content area
        BuildScrollArea();
    }

    private void BuildCloseButton()
    {
        var btnGO = new GameObject("CloseBtn", typeof(RectTransform));
        btnGO.transform.SetParent(transform, false);
        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(1, 1);
        btnRt.anchorMax        = new Vector2(1, 1);
        btnRt.pivot            = new Vector2(1, 1);
        btnRt.anchoredPosition = new Vector2(-6, -6);
        btnRt.sizeDelta        = new Vector2(36, 36);
        var btnBg  = btnGO.AddComponent<Image>();
        btnBg.color = new Color(0.5f, 0.1f, 0.1f, 0.9f);
        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(Hide);
        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblRt = lblGO.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        var lbl = lblGO.AddComponent<Text>();
        lbl.text = "X"; lbl.fontSize = 16; lbl.fontStyle = FontStyle.Bold;
        lbl.color = Color.white; lbl.alignment = TextAnchor.MiddleCenter;
    }

    private void BuildTabs()
    {
        string[] labels = { "Active", "Completed", "Available" };
        _tabButtons = new Button[3];
        _tabLabels  = new Text[3];

        var tabBar = new GameObject("TabBar", typeof(RectTransform));
        tabBar.transform.SetParent(transform, false);
        var tbRt = tabBar.GetComponent<RectTransform>();
        tbRt.anchorMin        = new Vector2(0, 1);
        tbRt.anchorMax        = new Vector2(1, 1);
        tbRt.pivot            = new Vector2(0, 1);
        tbRt.anchoredPosition = new Vector2(0, -HeaderH);
        tbRt.sizeDelta        = new Vector2(0, TabH);

        for (int i = 0; i < 3; i++)
        {
            int idx = i; // capture for closure
            var tabGO = new GameObject($"Tab{i}", typeof(RectTransform));
            tabGO.transform.SetParent(tabBar.transform, false);
            var tRt = tabGO.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(i / 3f, 0);
            tRt.anchorMax = new Vector2((i + 1) / 3f, 1);
            tRt.offsetMin = new Vector2(2, 0);
            tRt.offsetMax = new Vector2(-2, 0);

            var img = tabGO.AddComponent<Image>();
            img.color = i == 0 ? new Color(0.2f, 0.15f, 0.35f, 1f) : new Color(0.1f, 0.08f, 0.18f, 1f);
            var btn = tabGO.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectTab(idx));
            _tabButtons[i] = btn;

            var lblGO = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(tabGO.transform, false);
            var lblRt = lblGO.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            var lbl = lblGO.AddComponent<Text>();
            lbl.text      = labels[i];
            lbl.fontSize  = 13;
            lbl.color     = Color.white;
            lbl.alignment = TextAnchor.MiddleCenter;
            _tabLabels[i] = lbl;
        }
    }

    private void BuildScrollArea()
    {
        float topOffset = HeaderH + TabH;

        // Scroll view root
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
        scrollGO.transform.SetParent(transform, false);
        var scrollRt = scrollGO.GetComponent<RectTransform>();
        scrollRt.anchorMin  = Vector2.zero;
        scrollRt.anchorMax  = new Vector2(1, 1);
        scrollRt.offsetMin  = new Vector2(0, 0);
        scrollRt.offsetMax  = new Vector2(0, -topOffset);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Viewport
        var vpGO = new GameObject("Viewport", typeof(RectTransform));
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRt = vpGO.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
        var vpMask = vpGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        vpGO.AddComponent<Image>().color = Color.clear;
        scrollRect.viewport = vpRt;

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentRoot = contentGO;
        var cRt = contentGO.GetComponent<RectTransform>();
        cRt.anchorMin        = new Vector2(0, 1);
        cRt.anchorMax        = new Vector2(1, 1);
        cRt.pivot            = new Vector2(0, 1);
        cRt.anchoredPosition = Vector2.zero;
        scrollRect.content   = cRt;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight   = true;
        vlg.childControlWidth    = true;
        vlg.childForceExpandWidth = true;
        vlg.spacing = EntryPad;
        vlg.padding = new RectOffset(8, 8, 8, 8);
    }

    // ---------------------------------------------------------------
    //  Tab selection & content refresh
    // ---------------------------------------------------------------

    private void SelectTab(int idx)
    {
        _activeTab = idx;
        // Update tab highlight
        for (int i = 0; i < 3; i++)
        {
            var img = _tabButtons[i].GetComponent<Image>();
            img.color = i == idx
                ? new Color(0.2f, 0.15f, 0.35f, 1f)
                : new Color(0.1f, 0.08f, 0.18f, 1f);
        }
        RefreshTab(idx);
    }

    private void RefreshTab(int idx)
    {
        if (_contentRoot == null || QuestManager.Instance == null) return;

        // Clear existing entries
        foreach (Transform child in _contentRoot.transform)
            Destroy(child.gameObject);

        switch (idx)
        {
            case 0: PopulateActive();    break;
            case 1: PopulateCompleted(); break;
            case 2: PopulateAvailable(); break;
        }
    }

    private void PopulateActive()
    {
        if (QuestManager.Instance.ActiveQuests.Count == 0)
        {
            AddEmptyLabel("No active quests.");
            return;
        }
        foreach (var kvp in QuestManager.Instance.ActiveQuests)
            AddQuestEntry(kvp.Value.Definition, kvp.Value, showAcceptButton: false);
    }

    private void PopulateCompleted()
    {
        var defs = new List<QuestDefinition>();
        foreach (var def in QuestManager.Instance.AllQuests)
            if (QuestManager.Instance.IsCompleted(def.questId)) defs.Add(def);

        if (defs.Count == 0) { AddEmptyLabel("No completed quests."); return; }
        foreach (var def in defs) AddQuestEntry(def, null, showAcceptButton: false, completed: true);
    }

    private void PopulateAvailable()
    {
        var available = QuestManager.Instance.GetAvailableQuests();
        if (available.Count == 0) { AddEmptyLabel("No quests available."); return; }
        foreach (var def in available) AddQuestEntry(def, null, showAcceptButton: true);
    }

    // ---------------------------------------------------------------
    //  Entry building
    // ---------------------------------------------------------------

    private void AddEmptyLabel(string msg)
    {
        var go  = new GameObject("Empty", typeof(RectTransform));
        go.transform.SetParent(_contentRoot.transform, false);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40;
        var txt = go.AddComponent<Text>();
        txt.text      = msg;
        txt.fontSize  = 13;
        txt.color     = new Color(0.6f, 0.6f, 0.6f);
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private void AddQuestEntry(QuestDefinition def, QuestInstance inst, bool showAcceptButton, bool completed = false)
    {
        var entryGO = new GameObject($"Entry_{def.questId}", typeof(RectTransform));
        entryGO.transform.SetParent(_contentRoot.transform, false);

        var bg = entryGO.AddComponent<Image>();
        bg.color = def.isMainStory
            ? new Color(0.12f, 0.06f, 0.22f, 0.85f)
            : new Color(0.08f, 0.08f, 0.14f, 0.85f);

        var vlg = entryGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding    = new RectOffset(10, 10, 8, 8);
        vlg.spacing    = 4;
        vlg.childControlHeight = true;
        vlg.childControlWidth  = true;
        vlg.childForceExpandWidth = true;

        var le = entryGO.AddComponent<LayoutElement>();
        le.minHeight = EntryH;

        var csf = entryGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Name row
        AddLabel(entryGO,
            (def.isMainStory ? "[Story] " : "") + def.questName,
            14, def.isMainStory ? new Color(0.85f, 0.65f, 1f) : Color.white,
            FontStyle.Bold);

        // Description
        if (!string.IsNullOrEmpty(def.description))
            AddLabel(entryGO, def.description, 11, new Color(0.75f, 0.75f, 0.75f));

        // Separator
        AddSeparator(entryGO);

        if (completed)
        {
            AddLabel(entryGO, "COMPLETED", 11, new Color(0.4f, 0.9f, 0.4f), FontStyle.Bold);
        }
        else if (inst != null)
        {
            // Objectives with progress
            for (int i = 0; i < inst.Objectives.Length; i++)
            {
                var obj = inst.Objectives[i];
                string prefix = obj.isCompleted ? "✓ " : "• ";
                Color  col    = obj.isCompleted ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.75f, 0.9f, 0.75f);
                AddLabel(entryGO, prefix + obj.GetProgressText(), 11, col);
            }
        }

        // Rewards
        if (def.rewards.Count > 0 && !completed)
        {
            AddSeparator(entryGO);
            string rewardStr = "Rewards: ";
            for (int i = 0; i < def.rewards.Count; i++)
            {
                var r = def.rewards[i];
                if (!r.IsEmpty)
                    rewardStr += (i > 0 ? ", " : "") + r.ToString();
            }
            AddLabel(entryGO, rewardStr, 10, new Color(1f, 0.85f, 0.4f));
        }

        // Accept button (Available tab)
        if (showAcceptButton)
        {
            var btnGO  = new GameObject("AcceptBtn", typeof(RectTransform));
            btnGO.transform.SetParent(entryGO.transform, false);
            var btnLE  = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 28;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.45f, 0.15f, 0.9f);
            var btn    = btnGO.AddComponent<Button>();
            var capturedDef = def;
            btn.onClick.AddListener(() =>
            {
                QuestManager.Instance?.AcceptQuest(capturedDef);
                SelectTab(0); // Switch to Active tab
            });
            var lblGO  = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRt  = lblGO.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            var lbl    = lblGO.AddComponent<Text>();
            lbl.text      = "Accept Quest";
            lbl.fontSize  = 12;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color     = Color.white;
            lbl.alignment = TextAnchor.MiddleCenter;
        }
    }

    // ---------------------------------------------------------------
    //  Layout helpers
    // ---------------------------------------------------------------

    private static Text AddLabel(GameObject parent, string text, int fontSize,
                                  Color color, FontStyle style = FontStyle.Normal)
    {
        var go  = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var le  = go.AddComponent<LayoutElement>();
        le.minHeight = fontSize + 6;
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.fontSize  = fontSize;
        txt.color     = color;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleLeft;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return txt;
    }

    private static void AddSeparator(GameObject parent)
    {
        var go  = new GameObject("Sep", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f, 0.6f);
    }
}
