using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Crafting;
using Voidborne.UI.Style;

namespace Voidborne.UI
{
    /// <summary>
    /// Volume 4.4 — horizontal strip of buttons, one per recipe registered
    /// for the host machine. Used by <see cref="MachineUI"/> when the active
    /// machine has more than one recipe.
    ///
    /// Each tab is a small button whose label is the recipe's output display
    /// name (via <see cref="ItemDatabase.GetItem(string)"/>) with a thin
    /// border tinted to one of the <see cref="RecipeColors"/> palette entries
    /// (cycled by index, mirroring the HTML's RECIPE_COLORS list).
    ///
    /// Selection is purely visual for V4.4 — clicking a tab fires
    /// <see cref="OnRecipeSelected"/>; the host (MachineUI) decides what to
    /// do with the selection. The real "highlight inputs in the grid"
    /// behaviour is V6.1 / V9.1 scope.
    /// </summary>
    public class RecipeTabsUI : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Palette — mirrors voidborne-flowchart-v3.html RECIPE_COLORS
        // ---------------------------------------------------------------

        /// <summary>Cycled per-tab border colour. Matches the HTML reference's
        /// RECIPE_COLORS array (green, cyan, magenta, orange).</summary>
        public static readonly Color[] RecipeColors = new[]
        {
            HexRGB(0xb6, 0xf7, 0x3e), // green  (matches UIStyle.Accent)
            HexRGB(0x56, 0xd3, 0xff), // cyan
            HexRGB(0xf7, 0x78, 0xba), // magenta
            HexRGB(0xff, 0xb4, 0x54), // orange
        };

        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        private readonly List<Button> _tabs       = new List<Button>();
        private readonly List<Image>  _tabBorders = new List<Image>();
        private readonly List<RecipeDefinition> _recipes = new List<RecipeDefinition>();

        private int _selectedIndex = -1;

        /// <summary>Fired when a tab is clicked. Argument is the recipe the
        /// tab represents.</summary>
        public event Action<RecipeDefinition> OnRecipeSelected;

        /// <summary>Currently selected recipe, or null if no selection.</summary>
        public RecipeDefinition Selected =>
            (_selectedIndex >= 0 && _selectedIndex < _recipes.Count) ? _recipes[_selectedIndex] : null;

        /// <summary>Read-only view of the tab buttons (test introspection).</summary>
        public IReadOnlyList<Button> Tabs => _tabs;

        /// <summary>Read-only view of the recipes wired to the tabs.</summary>
        public IReadOnlyList<RecipeDefinition> Recipes => _recipes;

        // ---------------------------------------------------------------
        //  Build
        // ---------------------------------------------------------------

        /// <summary>
        /// Constructs a tab for each recipe. Safe to call multiple times —
        /// previous tab GameObjects are destroyed first.
        /// </summary>
        public void Build(IReadOnlyList<RecipeDefinition> recipes)
        {
            // Tear down any previous build.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null) GameObject.DestroyImmediate(child.gameObject);
            }
            _tabs.Clear();
            _tabBorders.Clear();
            _recipes.Clear();
            _selectedIndex = -1;

            if (recipes == null || recipes.Count == 0) return;

            // Ensure a HorizontalLayoutGroup so tabs flow left-to-right.
            HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing                = UIStyle.SlotGap;
            hlg.padding                = new RectOffset(0, 0, 0, 0);

            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe == null) continue;

                int capturedIndex = _recipes.Count;
                _recipes.Add(recipe);

                Color tabColor = RecipeColors[capturedIndex % RecipeColors.Length];
                Button tab = BuildTab(recipe, tabColor, () => HandleClick(capturedIndex));
                _tabs.Add(tab);
            }

            // Default-select the first recipe so the panel has a coherent
            // initial state. The host MachineUI can call SetSelected to override.
            if (_recipes.Count > 0)
            {
                _selectedIndex = 0;
                UpdateSelectionVisuals();
            }
        }

        /// <summary>Selects the tab by index without firing the event.</summary>
        public void SetSelected(int index)
        {
            if (index < 0 || index >= _recipes.Count) return;
            _selectedIndex = index;
            UpdateSelectionVisuals();
        }

        // ---------------------------------------------------------------
        //  Internals
        // ---------------------------------------------------------------

        private Button BuildTab(RecipeDefinition recipe, Color borderColor, Action onClick)
        {
            GameObject tabGo = new GameObject(
                $"RecipeTab_{recipe.OutputId}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            tabGo.transform.SetParent(transform, false);

            int uiLayer = LayerMask.NameToLayer("UI");
            tabGo.layer = uiLayer >= 0 ? uiLayer : 5;

            RectTransform rt = tabGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 26f);

            Image bg = tabGo.GetComponent<Image>();
            bg.color = UIStyle.PanelLight;
            bg.raycastTarget = true;

            // Coloured border child (drawn behind the bg)
            Image border = UIBuilder.Border(rt, borderColor);
            // Border is set as first sibling by UIBuilder — visible 1px around
            // the bg child.

            Button btn = tabGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // V4.5 — hovering a tab surfaces the recipe's output ItemDefinition
            // tooltip (rich content, recipe summary, properties).
            TooltipRecipeHover hover = tabGo.AddComponent<TooltipRecipeHover>();
            hover.Recipe = recipe;

            // Resolve a display label for the recipe's output. The
            // ItemDatabase lookup is best-effort — in tests without a
            // populated database we fall back to the raw item ID.
            string label = recipe.OutputId ?? "?";
            ItemDatabase db = ItemDatabase.Instance;
            if (db != null)
            {
                ItemDefinition def = db.GetItem(recipe.OutputId);
                if (def != null && !string.IsNullOrEmpty(def.displayName))
                    label = def.displayName;
            }

            TextMeshProUGUI labelTmp = UIBuilder.Text(
                tabGo.transform, label,
                UIStyle.FontSizeSmall, UIStyle.Text, "Label");
            labelTmp.alignment = TextAlignmentOptions.Center;
            RectTransform labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 2f);
            labelRt.offsetMax = new Vector2(-4f, -2f);

            _tabBorders.Add(border);
            return btn;
        }

        private void HandleClick(int index)
        {
            if (index < 0 || index >= _recipes.Count) return;
            _selectedIndex = index;
            UpdateSelectionVisuals();
            OnRecipeSelected?.Invoke(_recipes[index]);
        }

        private void UpdateSelectionVisuals()
        {
            // The selected tab's background brightens to UIStyle.Border so the
            // selection reads at a glance; unselected tabs keep PanelLight.
            for (int i = 0; i < _tabs.Count; i++)
            {
                Button tab = _tabs[i];
                if (tab == null) continue;
                Image bg = tab.targetGraphic as Image;
                if (bg == null) continue;
                bg.color = (i == _selectedIndex) ? UIStyle.Border : UIStyle.PanelLight;
            }
        }

        private static Color HexRGB(byte r, byte g, byte b)
        {
            const float inv = 1f / 255f;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }
    }
}
