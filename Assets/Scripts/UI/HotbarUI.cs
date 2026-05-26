using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible 5-slot hotbar on the Index bracer face.
/// Scroll wheel or 1-5 keys select the active slot (gold highlight).
/// Subscribes to PlayerInventory.OnInventoryChanged to refresh.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------
    private const float SlotSize     = 50f;
    private const float SlotSpacing  = 4f;
    private const float BarPaddingH  = 8f;
    private const float BarPaddingV  = 6f;
    private const int   HotbarCount  = 5;

    // ---------------------------------------------------------------
    //  References
    // ---------------------------------------------------------------
    private PlayerInventory _playerInventory;
    private readonly List<SlotUI> _slots = new List<SlotUI>();

    // ---------------------------------------------------------------
    //  Init (called by UIManager)
    // ---------------------------------------------------------------

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
        BuildBar();
        RefreshAll();
        UpdateSelection(_playerInventory.SelectedHotbarIndex);
        playerInventory.OnInventoryChanged += OnInventoryChanged;
    }

    private void OnDestroy()
    {
        if (_playerInventory != null)
            _playerInventory.OnInventoryChanged -= OnInventoryChanged;
    }

    // ---------------------------------------------------------------
    //  Unity Update — hotbar selection input
    // ---------------------------------------------------------------

    private void Update()
    {
        // Input is handled exclusively by WeaponSwitcher.
        // HotbarUI just keeps the highlight in sync with the current selection.
        if (_playerInventory == null) return;
        UpdateSelection(_playerInventory.SelectedHotbarIndex);
    }

    // ---------------------------------------------------------------
    //  Active slot
    // ---------------------------------------------------------------

    private int _prevSelectedIndex = -1;

    private void UpdateSelection(int selectedIndex)
    {
        if (selectedIndex == _prevSelectedIndex) return;

        // Deselect previous, select new (2 updates instead of 9)
        if (_prevSelectedIndex >= 0 && _prevSelectedIndex < _slots.Count)
            _slots[_prevSelectedIndex].SetSelected(false);
        if (selectedIndex >= 0 && selectedIndex < _slots.Count)
            _slots[selectedIndex].SetSelected(true);

        _prevSelectedIndex = selectedIndex;
    }

    // ---------------------------------------------------------------
    //  Refresh
    // ---------------------------------------------------------------

    private void OnInventoryChanged()
    {
        RefreshAll();
        UpdateSelection(_playerInventory.SelectedHotbarIndex);
    }

    private void RefreshAll()
    {
        foreach (SlotUI s in _slots) s.Refresh();
    }

    // ---------------------------------------------------------------
    //  Build the bar
    // ---------------------------------------------------------------

    private void BuildBar()
    {
        float totalWidth  = HotbarCount * SlotSize + (HotbarCount - 1) * SlotSpacing + BarPaddingH * 2;
        float totalHeight = SlotSize + BarPaddingV * 2;

        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(totalWidth, totalHeight);

        // Background
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 0.88f);

        // Thin top border line
        GameObject topBorder = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        topBorder.transform.SetParent(transform, false);
        topBorder.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        RectTransform topRT = topBorder.GetComponent<RectTransform>();
        topRT.anchorMin  = Vector2.zero;
        topRT.anchorMax  = new Vector2(1, 1);
        topRT.offsetMin  = Vector2.zero;
        topRT.offsetMax  = new Vector2(0, -(totalHeight - 2));

        // Grid container — GridLayoutGroup ensures even spacing
        GameObject gridGO = new GameObject("SlotGrid", typeof(RectTransform));
        gridGO.transform.SetParent(transform, false);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin        = new Vector2(0, 0);
        gridRT.anchorMax        = new Vector2(0, 0);
        gridRT.pivot            = new Vector2(0, 0);
        gridRT.anchoredPosition = new Vector2(BarPaddingH, BarPaddingV);
        gridRT.sizeDelta        = new Vector2(
            HotbarCount * SlotSize + (HotbarCount - 1) * SlotSpacing,
            SlotSize);

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(SlotSize, SlotSize);
        glg.spacing         = new Vector2(SlotSpacing, SlotSpacing);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.MiddleLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = HotbarCount;

        // Slots
        for (int i = 0; i < HotbarCount; i++)
        {
            GameObject slotGO = new GameObject($"HotbarSlot_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(gridGO.transform, false);

            SlotUI slot = slotGO.AddComponent<SlotUI>();
            slot.Init(_playerInventory.Hotbar, i);
            _slots.Add(slot);

            // Number label (1-9) top-left of slot
            AddKeyLabel(slotGO.transform, (i + 1).ToString());
        }
    }

    private void AddKeyLabel(Transform parent, string text)
    {
        GameObject lbl = new GameObject("KeyHint",
            typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        lbl.transform.SetParent(parent, false);
        TMPro.TMP_Text t = lbl.GetComponent<TMPro.TMP_Text>();
        t.text            = text;
        t.color           = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        t.fontSize        = 9f;
        t.alignment       = TMPro.TextAlignmentOptions.TopLeft;
        t.raycastTarget   = false;
        RectTransform rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = new Vector2(3, 0);
        rt.offsetMax  = new Vector2(-2, -2);
    }

}
