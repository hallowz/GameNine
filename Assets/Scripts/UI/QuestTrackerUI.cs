using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent HUD panel — top-right corner.
/// Shows up to 3 tracked active quests: quest name + current objective + progress.
/// Subscribes to QuestManager events to refresh automatically.
/// Built procedurally by UIManager.
/// </summary>
public class QuestTrackerUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Layout constants
    // ---------------------------------------------------------------

    private const int   MaxTracked   = 3;
    private const float EntryHeight  = 72f;
    private const float EntryPadding = 6f;
    private const float PanelWidth   = 300f;
    private const float PanelPadX    = 10f;
    private const float PanelPadY    = 8f;

    // ---------------------------------------------------------------
    //  Runtime
    // ---------------------------------------------------------------

    private readonly List<TrackerEntry> _entries = new();

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void OnEnable()
    {
        QuestManager.OnQuestAccepted  += HandleQuestAccepted;
        QuestManager.OnObjectiveUpdated += HandleObjectiveUpdated;
        QuestManager.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        QuestManager.OnQuestAccepted  -= HandleQuestAccepted;
        QuestManager.OnObjectiveUpdated -= HandleObjectiveUpdated;
        QuestManager.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void Start()
    {
        // Rebuild in case quests were already active when this UI spawned
        RebuildAll();
    }

    // ---------------------------------------------------------------
    //  Event handlers
    // ---------------------------------------------------------------

    private void HandleQuestAccepted(QuestDefinition def)   => RebuildAll();
    private void HandleQuestCompleted(QuestDefinition def)  => RebuildAll();

    private void HandleObjectiveUpdated(QuestDefinition def, int objIndex)
    {
        // Refresh the entry for this quest without full rebuild
        foreach (var entry in _entries)
        {
            if (entry.questId == def.questId)
            {
                entry.Refresh(def, QuestManager.Instance?.GetActiveInstance(def.questId));
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    //  Build
    // ---------------------------------------------------------------

    private void RebuildAll()
    {
        // Clear old entries
        foreach (var e in _entries) Destroy(e.root);
        _entries.Clear();

        if (QuestManager.Instance == null) return;

        int shown = 0;
        foreach (var kvp in QuestManager.Instance.ActiveQuests)
        {
            if (shown >= MaxTracked) break;
            var entry = CreateEntry(kvp.Value, shown);
            _entries.Add(entry);
            shown++;
        }

        // Resize panel background to fit entries
        float totalH = shown * (EntryHeight + EntryPadding) + PanelPadY * 2f;
        var rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(PanelWidth, totalH);
    }

    private TrackerEntry CreateEntry(QuestInstance inst, int index)
    {
        var entry = new TrackerEntry();
        entry.questId = inst.Definition.questId;

        // Container
        entry.root = new GameObject($"Entry_{inst.Definition.questId}", typeof(RectTransform));
        entry.root.transform.SetParent(transform, false);

        var rt = entry.root.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(1, 1);
        rt.pivot            = new Vector2(0, 1);
        float yOffset       = PanelPadY + index * (EntryHeight + EntryPadding);
        rt.anchoredPosition = new Vector2(PanelPadX, -yOffset);
        rt.sizeDelta        = new Vector2(-PanelPadX * 2f, EntryHeight);

        // Background tint for main-story quests
        var bg = entry.root.AddComponent<Image>();
        bg.color = inst.Definition.isMainStory
            ? new Color(0.15f, 0.08f, 0.25f, 0.75f)
            : new Color(0.05f, 0.05f, 0.10f, 0.65f);

        // Quest name
        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(entry.root.transform, false);
        var nameTxt = nameGO.AddComponent<Text>();
        nameTxt.text      = inst.Definition.questName;
        nameTxt.fontSize  = 13;
        nameTxt.fontStyle = inst.Definition.isMainStory ? FontStyle.Bold : FontStyle.Normal;
        nameTxt.color     = inst.Definition.isMainStory ? new Color(0.85f, 0.65f, 1f) : Color.white;
        nameTxt.alignment = TextAnchor.UpperLeft;
        var nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin        = Vector2.zero;
        nameRt.anchorMax        = new Vector2(1, 1);
        nameRt.offsetMin        = new Vector2(6, 28);
        nameRt.offsetMax        = new Vector2(-4, -4);

        // Current objective
        var objGO = new GameObject("Objective", typeof(RectTransform));
        objGO.transform.SetParent(entry.root.transform, false);
        var objTxt = objGO.AddComponent<Text>();
        objTxt.fontSize  = 11;
        objTxt.color     = new Color(0.75f, 0.9f, 0.75f);
        objTxt.alignment = TextAnchor.LowerLeft;
        var objRt = objGO.GetComponent<RectTransform>();
        objRt.anchorMin  = Vector2.zero;
        objRt.anchorMax  = new Vector2(1, 1);
        objRt.offsetMin  = new Vector2(6, 6);
        objRt.offsetMax  = new Vector2(-4, -24);

        entry.nameTxt = nameTxt;
        entry.objTxt  = objTxt;
        entry.Refresh(inst.Definition, inst);
        return entry;
    }

    // ---------------------------------------------------------------
    //  Inner types
    // ---------------------------------------------------------------

    private class TrackerEntry
    {
        public string questId;
        public GameObject root;
        public Text nameTxt;
        public Text objTxt;

        public void Refresh(QuestDefinition def, QuestInstance inst)
        {
            if (inst == null) return;
            // Find first incomplete objective
            string objText = "";
            foreach (var obj in inst.Objectives)
            {
                if (!obj.isCompleted)
                {
                    objText = obj.GetProgressText();
                    break;
                }
            }
            if (string.IsNullOrEmpty(objText))
                objText = "Return to quest giver";
            if (objTxt != null) objTxt.text = objText;
        }
    }
}
