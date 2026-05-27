using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.UI.Style;

/// <summary>
/// Always-visible hotbar at the bottom of the screen.
/// Scroll wheel or 1-N keys select the active slot (Accent-coloured frame).
/// Subscribes to PlayerInventory.OnInventoryChanged to refresh.
///
/// V4.2 restyle: visuals now read from UIStyle (Panel, PanelLight, Accent,
/// Border, Text, SlotSize, SlotGap, FontSizeSmall). Slot backgrounds are
/// built via UIBuilder.SlotBg. The selected-slot frame uses UIBuilder.Border
/// tinted UIStyle.Accent with an inset PanelLight panel for the hollow-frame
/// look the HTML reference uses.
///
/// Slot count is driven by the bound PlayerInventory.Hotbar.SlotCount so
/// the hotbar layout adapts if the inventory model changes; input bindings
/// (1-9 keys + scroll wheel) are owned by WeaponSwitcher and unchanged.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------
    private const float BarPaddingH = 8f;
    private const float BarPaddingV = 6f;

    // ---------------------------------------------------------------
    //  References
    // ---------------------------------------------------------------
    private PlayerInventory _playerInventory;
    private readonly List<SlotUI> _slots = new List<SlotUI>();
    private int _hotbarCount;

    // ---------------------------------------------------------------
    //  Init (called by UIManager)
    // ---------------------------------------------------------------

    public void Init(PlayerInventory playerInventory)
    {
        _playerInventory = playerInventory;
        _hotbarCount = playerInventory.Hotbar.SlotCount;
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
        float slotSize = UIStyle.SlotSize;
        float slotGap  = UIStyle.SlotGap;

        float totalWidth  = _hotbarCount * slotSize + (_hotbarCount - 1) * slotGap + BarPaddingH * 2f;
        float totalHeight = slotSize + BarPaddingV * 2f;

        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(totalWidth, totalHeight);

        // Background — UIStyle.Panel
        Image bg = gameObject.AddComponent<Image>();
        bg.color = UIStyle.Panel;
        bg.raycastTarget = false;

        // Thin top border line — UIStyle.Border
        GameObject topBorder = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
        topBorder.transform.SetParent(transform, false);
        Image topImg = topBorder.GetComponent<Image>();
        topImg.color = UIStyle.Border;
        topImg.raycastTarget = false;
        RectTransform topRT = topBorder.GetComponent<RectTransform>();
        topRT.anchorMin  = new Vector2(0f, 1f);
        topRT.anchorMax  = new Vector2(1f, 1f);
        topRT.pivot      = new Vector2(0.5f, 1f);
        topRT.sizeDelta  = new Vector2(0f, UIStyle.BorderWidth);
        topRT.anchoredPosition = Vector2.zero;

        // Grid container — GridLayoutGroup ensures even spacing
        GameObject gridGO = new GameObject("SlotGrid", typeof(RectTransform));
        gridGO.transform.SetParent(transform, false);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin        = new Vector2(0f, 0f);
        gridRT.anchorMax        = new Vector2(0f, 0f);
        gridRT.pivot            = new Vector2(0f, 0f);
        gridRT.anchoredPosition = new Vector2(BarPaddingH, BarPaddingV);
        gridRT.sizeDelta        = new Vector2(
            _hotbarCount * slotSize + (_hotbarCount - 1) * slotGap,
            slotSize);

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(slotSize, slotSize);
        glg.spacing         = new Vector2(slotGap, slotGap);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.MiddleLeft;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = _hotbarCount;

        // Slots
        for (int i = 0; i < _hotbarCount; i++)
        {
            GameObject slotGO = new GameObject($"HotbarSlot_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(gridGO.transform, false);

            SlotUI slot = slotGO.AddComponent<SlotUI>();
            slot.Init(_playerInventory.Hotbar, i);
            _slots.Add(slot);

            // Number label (1-N) top-left of slot — UIStyle.TextDim + FontSizeSmall
            AddKeyLabel(slotGO.transform, (i + 1).ToString());
        }
    }

    private void AddKeyLabel(Transform parent, string text)
    {
        GameObject lbl = new GameObject("KeyHint",
            typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        lbl.transform.SetParent(parent, false);
        TMPro.TMP_Text t = lbl.GetComponent<TMPro.TMP_Text>();
        t.text          = text;
        t.font          = UIStyle.Font;
        t.color         = UIStyle.TextDim;
        t.fontSize      = UIStyle.FontSizeSmall;
        t.alignment     = TMPro.TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        RectTransform rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 0f);
        rt.offsetMax = new Vector2(-2f, -2f);
    }
}
