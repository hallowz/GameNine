using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Voidborne.UI
{
    /// <summary>
    /// UI panel for a ChestBlock. 9×12 grid (108 slots).
    /// Opens alongside the player inventory panel.
    /// Shift-click moves items from chest to player inventory.
    /// Created and owned by UIManager.
    /// </summary>
    public class ChestUI : MonoBehaviour
    {
        // ── Layout ────────────────────────────────────────────────────────────────
        private const float SlotSize     = 50f;
        private const float SlotSpacing  = 4f;
        private const float PanelPadding = 10f;
        private const float SectionGap   = 6f;
        private const int   DefaultColumns = 9;

        // ── State ─────────────────────────────────────────────────────────────────
        private ChestBlock      _chest;
        private PlayerInventory _playerInventory;

        private readonly List<SlotUI> _slots = new List<SlotUI>();
        private GameObject _gridRoot;

        // ── Init (called once by UIManager) ───────────────────────────────────────

        public void Init()
        {
            BuildPanel();
            gameObject.SetActive(false);
        }

        // ── Open / Close ──────────────────────────────────────────────────────────

        public void Open(ChestBlock chest, PlayerInventory playerInv)
        {
            _chest           = chest;
            _playerInventory = playerInv;

            RebuildSlots(chest.ChestInventory);
            chest.ChestInventory.OnInventoryChanged += RefreshAll;
            RefreshAll();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_chest != null)
            {
                _chest.ChestInventory.OnInventoryChanged -= RefreshAll;
                _chest = null;
            }
            _playerInventory = null;
            gameObject.SetActive(false);
        }

        public ChestBlock CurrentChest => _chest;

        // ── Panel construction ────────────────────────────────────────────────────

        private void BuildPanel()
        {
            Image bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.09f, 0.06f, 0.93f);   // warm dark wood tone

            VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = SectionGap;
            vlg.padding = new RectOffset(
                (int)PanelPadding, (int)PanelPadding,
                (int)PanelPadding, (int)PanelPadding);

            ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(transform, false);
            TMP_Text title = titleGO.GetComponent<TMP_Text>();
            title.text      = "CHEST";
            title.color     = new Color(0.85f, 0.70f, 0.45f);
            title.fontSize  = 14f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 22f;
            titleLE.minHeight       = 22f;

            // Grid root (populated in RebuildSlots)
            _gridRoot = new GameObject("ChestGrid", typeof(RectTransform));
            _gridRoot.transform.SetParent(transform, false);
        }

        private void RebuildSlots(Inventory inventory)
        {
            // Clear previous slots
            foreach (var s in _slots)
                if (s != null) Destroy(s.gameObject);
            _slots.Clear();

            for (int i = _gridRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.transform.GetChild(i).gameObject);

            int effectiveCols = Mathf.Min(inventory.Width, _chest != null ? _chest.Cols : DefaultColumns);

            GridLayoutGroup glg = _gridRoot.GetComponent<GridLayoutGroup>();
            if (glg == null) glg = _gridRoot.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(SlotSize, SlotSize);
            glg.spacing         = new Vector2(SlotSpacing, SlotSpacing);
            glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment  = TextAnchor.UpperLeft;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = effectiveCols;

            ContentSizeFitter gridCSF = _gridRoot.GetComponent<ContentSizeFitter>();
            if (gridCSF == null) gridCSF = _gridRoot.AddComponent<ContentSizeFitter>();
            gridCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            if (_gridRoot.GetComponent<LayoutElement>() == null)
                _gridRoot.AddComponent<LayoutElement>();

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                GameObject slotGO = new GameObject($"ChestSlot_{i}",
                    typeof(RectTransform), typeof(Image));
                slotGO.transform.SetParent(_gridRoot.transform, false);

                SlotUI slot = slotGO.AddComponent<SlotUI>();
                slot.Init(inventory, i);
                slot.OnShiftClick += OnSlotShiftClick;
                _slots.Add(slot);
            }
        }

        private void RefreshAll()
        {
            foreach (var slot in _slots)
                slot.Refresh();
        }

        // ── Shift-click: move chest item → player inventory ───────────────────────

        private void OnSlotShiftClick(SlotUI slotUI, ItemStack stack)
        {
            if (_chest == null || _playerInventory == null || stack.IsEmpty) return;

            // Frozen (compression chest unpowered) — view only, no transfers.
            if (_chest.IsFrozen) return;

            int idx = _slots.IndexOf(slotUI);
            if (idx < 0) return;

            bool placed = _playerInventory.AddItem(stack);
            if (placed)
            {
                _chest.ChestInventory.SetSlot(idx, default);
            }
            else
            {
                // Partial: count how many were actually added
                int before = _playerInventory.CountAllItem(stack.item.itemId);
                // (already added in AddItem; this branch means NOT all placed — leave remainder in chest)
            }
        }
    }
}
