using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Voidborne.UI.Style
{
    /// <summary>
    /// Helpers for building styled Canvas children. Every UI script in
    /// Volume 4 uses these so visual changes are global.
    ///
    /// All helpers:
    /// <list type="bullet">
    ///   <item>Are pure static — no instance state.</item>
    ///   <item>Set the new GameObject's layer to the "UI" layer (falls back to 5).</item>
    ///   <item>Parent the new GameObject to the supplied transform (worldPositionStays = false).</item>
    ///   <item>Return the built component so callers can chain.</item>
    /// </list>
    ///
    /// Usable from both runtime and editor code (no <c>#if UNITY_EDITOR</c> guards).
    /// </summary>
    public static class UIBuilder
    {
        // ---------------------------------------------------------------
        //  Public helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Creates a GameObject with a <see cref="RectTransform"/> and a
        /// background <see cref="Image"/> tinted <see cref="UIStyle.Panel"/>.
        /// </summary>
        public static RectTransform Panel(Transform parent, string name = "Panel")
        {
            var go = NewUIObject(name, parent);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = UIStyle.Panel;
            img.raycastTarget = true;
            return rt;
        }

        /// <summary>
        /// Creates a GameObject with a <see cref="TextMeshProUGUI"/> using
        /// <see cref="UIStyle.Font"/>. Text is centred by default.
        /// </summary>
        public static TextMeshProUGUI Text(
            Transform parent,
            string content,
            int fontSize,
            Color color,
            string name = "Text")
        {
            var go = NewUIObject(name, parent);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = UIStyle.Font;
            tmp.text = content ?? string.Empty;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Creates a slot-sized (50x50) GameObject with an <see cref="Image"/>
        /// tinted <see cref="UIStyle.PanelLight"/>. Used for hotbar and
        /// inventory slot backgrounds in V4.2-V4.3.
        /// </summary>
        public static Image SlotBg(Transform parent, string name = "Slot")
        {
            var go = NewUIObject(name, parent);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(UIStyle.SlotSize, UIStyle.SlotSize);
            var img = go.AddComponent<Image>();
            img.color = UIStyle.PanelLight;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>
        /// Creates a Button with a TMP label child. The label uses the body
        /// font size and the primary text colour; the background uses
        /// <see cref="UIStyle.PanelLight"/>.
        /// </summary>
        public static Button Btn(
            Transform parent,
            string label,
            UnityAction onClick,
            string name = "Btn")
        {
            var go = NewUIObject(name, parent);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = UIStyle.PanelLight;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            // Label child, stretched to fill the button.
            var labelTmp = Text(go.transform, label, UIStyle.FontSizeBody, UIStyle.Text, "Label");
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return btn;
        }

        /// <summary>
        /// Adds a 1-pixel border child to <paramref name="target"/>. The child
        /// stretches to fill the target and is tinted <paramref name="color"/>.
        /// Returns the child Image. The border is drawn as a solid rectangle
        /// behind any sibling content; callers that want a hollow frame should
        /// place this above a panel of <see cref="UIStyle.Panel"/> colour and
        /// inset their content by <see cref="UIStyle.BorderWidth"/>.
        /// </summary>
        public static Image Border(RectTransform target, Color color)
        {
            var go = NewUIObject("Border", target);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ---------------------------------------------------------------
        //  Internals
        // ---------------------------------------------------------------

        private static int UILayer
        {
            get
            {
                var layer = LayerMask.NameToLayer("UI");
                return layer >= 0 ? layer : 5;
            }
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "UIElement" : name);
            go.layer = UILayer;
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            return go;
        }
    }
}
