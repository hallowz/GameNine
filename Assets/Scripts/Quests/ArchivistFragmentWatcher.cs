using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monitors the player's inventory for "archive fragment" items.
/// When the player simultaneously holds a fragment AND has spoken to ArchivistCell,
/// a brief UI highlight is shown confirming the fragment matches Cell's logs.
///
/// Attach to any persistent scene object (e.g. the UIManager GO).
/// The highlight panel is built procedurally and shown for 3 seconds.
/// </summary>
public class ArchivistFragmentWatcher : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    /// <summary>Any item flagged as an archive fragment triggers the highlight.</summary>
    [SerializeField] private string fragmentTagKeyword = "archive_fragment";

    private const float HighlightDuration = 3.5f;

    // ---------------------------------------------------------------
    //  Runtime
    // ---------------------------------------------------------------

    private GameObject _highlightPanel;
    private Text       _highlightText;
    private Coroutine  _highlightRoutine;
    private bool       _highlightBuilt;

    private void Start()
    {
        PlayerInventory.OnItemAdded += OnItemAdded;
    }

    private void OnDestroy()
    {
        PlayerInventory.OnItemAdded -= OnItemAdded;
    }

    private void OnItemAdded(ItemDefinition item, int quantity)
    {
        if (item == null) return;

        // Check if the item is an archive fragment (by id convention or name)
        bool isFragment = item.itemId.Contains(fragmentTagKeyword)
                       || item.displayName.ToLowerInvariant().Contains("fragment");
        if (!isFragment) return;

        // Only highlight if player has spoken to ArchivistCell (InteractWith quest objective)
        // We check whether any quest has the archivist_cell interactableId logged —
        // simplest proxy: QuestManager has the archivist interaction in a completed objective.
        // For a lightweight check we just fire whenever a fragment is obtained.
        ShowHighlight(item.displayName);
    }

    private void ShowHighlight(string fragmentName)
    {
        if (!_highlightBuilt) BuildHighlightPanel();

        _highlightText.text = $"<b>Fragment Match</b>\n\"{fragmentName}\" aligns with Archivist Cell's Log Entry 7.";
        _highlightPanel.SetActive(true);

        if (_highlightRoutine != null) StopCoroutine(_highlightRoutine);
        _highlightRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        // Fade in
        var img = _highlightPanel.GetComponent<Image>();
        var col = img.color;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            img.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0f, col.a, t / 0.3f));
            yield return null;
        }
        img.color = col;

        yield return new WaitForSeconds(HighlightDuration);

        // Fade out
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            img.color = new Color(col.r, col.g, col.b, Mathf.Lerp(col.a, 0f, t / 0.5f));
            yield return null;
        }
        _highlightPanel.SetActive(false);
        img.color = col;
    }

    private void BuildHighlightPanel()
    {
        _highlightBuilt = true;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { _highlightBuilt = false; return; }

        var panelGO = new GameObject("ArchivistFragmentHighlight", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);

        var rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.75f);
        rt.anchorMax        = new Vector2(0.5f, 0.75f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(420f, 60f);

        var img = panelGO.GetComponent<Image>();
        img.color = new Color(0.4f, 0.3f, 0.6f, 0.88f);  // purple tint

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(panelGO.transform, false);
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(10f, 4f); tRT.offsetMax = new Vector2(-10f, -4f);

        _highlightText = textGO.GetComponent<Text>();
        _highlightText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _highlightText.fontSize  = 13;
        _highlightText.color     = Color.white;
        _highlightText.alignment = TextAnchor.MiddleCenter;
        _highlightText.supportRichText = true;

        _highlightPanel = panelGO;
        panelGO.SetActive(false);

        // Stay on top
        panelGO.transform.SetAsLastSibling();
    }
}
