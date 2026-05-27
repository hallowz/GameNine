using TMPro;
using UnityEngine;

namespace Voidborne.UI.Style
{
    /// <summary>
    /// Voidborne UI style kit (Volume 4 Chunk 4.1).
    ///
    /// Pure static, runtime-visible. Returns the design palette, font, and
    /// standard sizes used by every M2 UI screen. Every UI script built or
    /// restyled in Volume 4.2-4.6 must read its colors, font, and sizes from
    /// this class so visual changes are global.
    ///
    /// Palette is drawn from the HTML reference (voidborne-flowchart-v3.html).
    /// Font is JetBrains Mono Regular (OFL licensed, bundled at
    /// Assets/Resources/Fonts/JetBrainsMono-SDF.asset).
    /// </summary>
    public static class UIStyle
    {
        // ---------------------------------------------------------------
        //  Palette  (hex from voidborne-flowchart-v3.html)
        // ---------------------------------------------------------------

        /// <summary>Deepest screen background (#0a0e14).</summary>
        public static readonly Color Background  = HexRGB(0x0a, 0x0e, 0x14);

        /// <summary>Standard panel fill (#14181f).</summary>
        public static readonly Color Panel       = HexRGB(0x14, 0x18, 0x1f);

        /// <summary>Hover / selected variant of Panel (#1e2430).</summary>
        public static readonly Color PanelLight  = HexRGB(0x1e, 0x24, 0x30);

        /// <summary>Panel border (#2a2f38).</summary>
        public static readonly Color Border      = HexRGB(0x2a, 0x2f, 0x38);

        /// <summary>Primary accent — green from the HTML, used for selected/active (#b6f73e).</summary>
        public static readonly Color Accent      = HexRGB(0xb6, 0xf7, 0x3e);

        /// <summary>Muted accent for inactive accent states (#6b9a25).</summary>
        public static readonly Color AccentDim   = HexRGB(0x6b, 0x9a, 0x25);

        /// <summary>Primary text colour (#c9d1d9).</summary>
        public static readonly Color Text        = HexRGB(0xc9, 0xd1, 0xd9);

        /// <summary>Dim/secondary text colour (#8b949e).</summary>
        public static readonly Color TextDim     = HexRGB(0x8b, 0x94, 0x9e);

        /// <summary>Error text colour (#f85149).</summary>
        public static readonly Color TextError   = HexRGB(0xf8, 0x51, 0x49);

        /// <summary>Success text colour (#56d364).</summary>
        public static readonly Color TextSuccess = HexRGB(0x56, 0xd3, 0x64);

        // ---------------------------------------------------------------
        //  Sizes
        // ---------------------------------------------------------------

        public const int FontSizeSmall  = 11;
        public const int FontSizeBody   = 13;
        public const int FontSizeLabel  = 14;
        public const int FontSizeHeader = 16;
        public const int FontSizeTitle  = 20;

        /// <summary>Hotbar / inventory slot pixel size (square).</summary>
        public const int SlotSize     = 50;

        /// <summary>Gap between adjacent slots in a strip or grid.</summary>
        public const int SlotGap      = 4;

        /// <summary>Default inner padding for panels.</summary>
        public const int PanelPadding = 12;

        /// <summary>Default border width in pixels.</summary>
        public const int BorderWidth  = 1;

        // ---------------------------------------------------------------
        //  Font  (lazy-loaded from Resources/Fonts/JetBrainsMono-SDF)
        // ---------------------------------------------------------------

        private const string FontResourcePath = "Fonts/JetBrainsMono-SDF";
        private static TMP_FontAsset _font;
        private static bool _fontResolved;

        /// <summary>
        /// JetBrains Mono SDF font asset for all UI text.
        ///
        /// Lazy-loaded from <c>Resources/Fonts/JetBrainsMono-SDF</c>. If the
        /// asset is missing, falls back to <see cref="TMP_Settings.defaultFontAsset"/>
        /// with a single Debug.LogWarning. May be null in environments where
        /// TMP settings have not loaded yet (returns null silently after warning).
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_fontResolved)
                {
                    return _font;
                }

                _font = Resources.Load<TMP_FontAsset>(FontResourcePath);
                if (_font == null)
                {
                    Debug.LogWarning(
                        "[UIStyle] JetBrains Mono SDF not found at Resources/" +
                        FontResourcePath +
                        ". Falling back to TMP default font asset. Re-bake the SDF " +
                        "asset via the V4.1 setup instructions if you need the " +
                        "intended monospace look.");
                    _font = TMP_Settings.defaultFontAsset;
                }

                _fontResolved = true;
                return _font;
            }
        }

        /// <summary>
        /// Resets the cached font lookup. Intended for tests; production code
        /// should treat <see cref="Font"/> as effectively immutable after the
        /// first access.
        /// </summary>
        public static void ResetFontCache()
        {
            _font = null;
            _fontResolved = false;
        }

        // ---------------------------------------------------------------
        //  Internals
        // ---------------------------------------------------------------

        private static Color HexRGB(byte r, byte g, byte b)
        {
            const float inv = 1f / 255f;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }
    }
}
