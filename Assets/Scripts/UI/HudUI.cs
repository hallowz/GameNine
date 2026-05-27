using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Player;
using Voidborne.UI.Style;

namespace Voidborne.UI
{
    /// <summary>
    /// V4.2 HUD — four stacked status bars anchored to the bottom-left corner:
    ///   1. Health     (red fill, 0..max)
    ///   2. Stamina    (green fill, 0..max)
    ///   3. Temperature (bidirectional gauge with a marker; cold left, hot right)
    ///   4. Corruption (purple fill, 0..max)
    ///
    /// Each bar is built from UIBuilder.Panel + a fill Image (filled radial /
    /// horizontal) + a numeric label. The HUD is client-local: this component
    /// never syncs and never modifies any underlying simulation. It just shows
    /// values written to it from outside.
    ///
    /// Integration with V14/V15 player systems is reflection-probed at runtime;
    /// if a Health / Stamina source can be found on PlayerManager, this HUD
    /// polls it each frame. Otherwise the bars stay at the defaults set in
    /// <see cref="ResetToDefaults"/> and the missing source is logged once.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Layout constants
        // ---------------------------------------------------------------

        private const float BarWidth      = 220f;
        private const float BarHeight     = 16f;
        private const float BarVGap       = 4f;
        private const float MarkerWidth   = 4f;
        private const float MarkerHeight  = 22f;
        private const float LabelOffsetX  = 8f;

        /// <summary>Fill colour for the health bar (red).</summary>
        public static readonly Color HealthColor      = new Color(0.78f, 0.22f, 0.22f, 1f);

        /// <summary>Fill colour for the stamina bar (green).</summary>
        public static readonly Color StaminaColor     = new Color(0.22f, 0.78f, 0.35f, 1f);

        /// <summary>Fill colour for the temperature gauge backdrop (cold-to-hot gradient handled by marker position).</summary>
        public static readonly Color TempColor        = new Color(0.55f, 0.65f, 0.80f, 1f);

        /// <summary>Marker colour on the temperature gauge.</summary>
        public static readonly Color TempMarkerColor  = new Color(0.95f, 0.95f, 0.95f, 1f);

        /// <summary>Fill colour for the corruption bar (purple).</summary>
        public static readonly Color CorruptionColor  = new Color(0.55f, 0.25f, 0.85f, 1f);

        // ---------------------------------------------------------------
        //  Built UI elements (assigned during BuildHud)
        // ---------------------------------------------------------------

        private Image _healthFill;
        private TMP_Text _healthLabel;
        private Image _staminaFill;
        private TMP_Text _staminaLabel;
        private Image _tempFill;
        private RectTransform _tempMarker;
        private RectTransform _tempBarRect;
        private TMP_Text _tempLabel;
        private Image _corruptionFill;
        private TMP_Text _corruptionLabel;

        // ---------------------------------------------------------------
        //  Current values (cached for tests / external reads)
        // ---------------------------------------------------------------

        public float HealthCurrent     { get; private set; } = 100f;
        public float HealthMax         { get; private set; } = 100f;
        public float StaminaCurrent    { get; private set; } = 100f;
        public float StaminaMax        { get; private set; } = 100f;
        public float TemperatureC      { get; private set; } = 20f;
        public float TempMinSafe       { get; private set; } = -10f;
        public float TempMaxSafe       { get; private set; } = 40f;
        public float CorruptionCurrent { get; private set; } = 0f;
        public float CorruptionMax     { get; private set; } = 100f;

        // ---------------------------------------------------------------
        //  Player integration probe (reflection — V14/V15 may add new types)
        // ---------------------------------------------------------------

        private PlayerManager _playerManager;
        private bool _loggedMissingPlayer;

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------

        private bool _built;

        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>
        /// Constructs the four HUD bars and stamps them with default values.
        /// Safe to call more than once: only the first call does the work.
        ///
        /// Production code should not need to call this — <see cref="Awake"/>
        /// runs it automatically. It is public so EditMode tests (which
        /// AddComponent without triggering Awake in-editor) can drive
        /// initialization manually.
        /// </summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildHud();
            ResetToDefaults();
            ApplyAll();
        }

        private void Start()
        {
            // Probe for the player. Reflection-tolerant: PlayerManager is the
            // current placeholder source, V14/V15 may add PlayerHealth /
            // PlayerStamina components later. We use the public API only.
            _playerManager = PlayerManager.Instance;
            if (_playerManager == null)
            {
                _playerManager = FindFirstObjectByType<PlayerManager>();
            }

            if (_playerManager == null && !_loggedMissingPlayer)
            {
                Debug.Log(
                    "[HudUI] No PlayerManager found; HUD will display defaults. " +
                    "Hookup with V14 player health / V15 stamina will land later.");
                _loggedMissingPlayer = true;
            }
        }

        private void Update()
        {
            if (_playerManager != null)
            {
                SetHealth(_playerManager.CurrentHealth, _playerManager.MaxHealth);
                SetStamina(_playerManager.CurrentStamina, _playerManager.MaxStamina);
                // Temperature + corruption have no driving systems in M2;
                // leave them at the default values per V4.2 scope guard.
            }
        }

        // ---------------------------------------------------------------
        //  Public API (read by UIManager / future player systems)
        // ---------------------------------------------------------------

        public void SetHealth(float current, float max)
        {
            HealthMax     = Mathf.Max(0.0001f, max);
            HealthCurrent = Mathf.Clamp(current, 0f, HealthMax);
            ApplyHealth();
        }

        public void SetStamina(float current, float max)
        {
            StaminaMax     = Mathf.Max(0.0001f, max);
            StaminaCurrent = Mathf.Clamp(current, 0f, StaminaMax);
            ApplyStamina();
        }

        /// <summary>
        /// Sets the temperature reading. The gauge marker is centred at
        /// <c>(minSafe + maxSafe) / 2</c>; values below <paramref name="minSafe"/>
        /// pin the marker to the left, values above <paramref name="maxSafe"/>
        /// pin it to the right.
        /// </summary>
        public void SetTemperature(float celsius, float minSafe = -10f, float maxSafe = 40f)
        {
            TempMinSafe   = minSafe;
            TempMaxSafe   = (maxSafe > minSafe) ? maxSafe : (minSafe + 1f);
            TemperatureC  = celsius;
            ApplyTemperature();
        }

        public void SetCorruption(float current, float max)
        {
            CorruptionMax     = Mathf.Max(0.0001f, max);
            CorruptionCurrent = Mathf.Clamp(current, 0f, CorruptionMax);
            ApplyCorruption();
        }

        /// <summary>Reads where the temperature marker is currently positioned: 0 = left, 1 = right, 0.5 = centred.</summary>
        public float TemperatureMarkerNormalized
        {
            get
            {
                float midpoint = (TempMinSafe + TempMaxSafe) * 0.5f;
                float halfRange = (TempMaxSafe - TempMinSafe) * 0.5f;
                if (halfRange <= 0f) return 0.5f;
                float offset = (TemperatureC - midpoint) / halfRange; // -1..+1 across safe range
                offset = Mathf.Clamp(offset, -1f, 1f);
                return 0.5f + offset * 0.5f;
            }
        }

        // ---------------------------------------------------------------
        //  Defaults / apply-all
        // ---------------------------------------------------------------

        public void ResetToDefaults()
        {
            HealthCurrent     = 100f;  HealthMax     = 100f;
            StaminaCurrent    = 100f;  StaminaMax    = 100f;
            TemperatureC      = 20f;   TempMinSafe   = -10f; TempMaxSafe = 40f;
            CorruptionCurrent = 0f;    CorruptionMax = 100f;
        }

        private void ApplyAll()
        {
            ApplyHealth();
            ApplyStamina();
            ApplyTemperature();
            ApplyCorruption();
        }

        private void ApplyHealth()
        {
            if (_healthFill != null)
            {
                _healthFill.fillAmount = HealthCurrent / HealthMax;
            }
            if (_healthLabel != null)
            {
                _healthLabel.text = $"{Mathf.RoundToInt(HealthCurrent)} / {Mathf.RoundToInt(HealthMax)}";
            }
        }

        private void ApplyStamina()
        {
            if (_staminaFill != null)
            {
                _staminaFill.fillAmount = StaminaCurrent / StaminaMax;
            }
            if (_staminaLabel != null)
            {
                _staminaLabel.text = $"{Mathf.RoundToInt(StaminaCurrent)} / {Mathf.RoundToInt(StaminaMax)}";
            }
        }

        private void ApplyTemperature()
        {
            if (_tempMarker != null && _tempBarRect != null)
            {
                float n = TemperatureMarkerNormalized; // 0..1
                float usable = BarWidth - MarkerWidth;
                float x = n * usable;
                _tempMarker.anchoredPosition = new Vector2(x, 0f);
            }
            if (_tempLabel != null)
            {
                _tempLabel.text = $"{Mathf.RoundToInt(TemperatureC)}°C";
            }
        }

        private void ApplyCorruption()
        {
            if (_corruptionFill != null)
            {
                _corruptionFill.fillAmount = CorruptionCurrent / CorruptionMax;
            }
            if (_corruptionLabel != null)
            {
                _corruptionLabel.text = $"{Mathf.RoundToInt(CorruptionCurrent)} / {Mathf.RoundToInt(CorruptionMax)}";
            }
        }

        // ---------------------------------------------------------------
        //  UI construction
        // ---------------------------------------------------------------

        private void BuildHud()
        {
            // Root anchored to bottom-left of canvas.
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);

            // Stack 4 bars from bottom up: corruption, temperature, stamina, health.
            // Reading order from screen-top to screen-bottom remains Health -> Stamina -> Temp -> Corruption.
            // Compute y positions so health sits on top, corruption on bottom.
            float bottomMargin = 16f;
            float labelLineH   = UIStyle.FontSizeBody + 2f;
            float rowHeight    = BarHeight + labelLineH + BarVGap;

            float yCorr = bottomMargin;
            float yTemp = bottomMargin + rowHeight;
            float yStam = bottomMargin + rowHeight * 2f;
            float yHp   = bottomMargin + rowHeight * 3f;

            BuildLabelledBar(
                name: "HealthBar",
                yPos: yHp,
                label: "HP",
                fillColor: HealthColor,
                out _healthFill,
                out _healthLabel);

            BuildLabelledBar(
                name: "StaminaBar",
                yPos: yStam,
                label: "STA",
                fillColor: StaminaColor,
                out _staminaFill,
                out _staminaLabel);

            BuildTemperatureBar(yPos: yTemp);

            BuildLabelledBar(
                name: "CorruptionBar",
                yPos: yCorr,
                label: "CRPT",
                fillColor: CorruptionColor,
                out _corruptionFill,
                out _corruptionLabel);
        }

        private void BuildLabelledBar(
            string name,
            float yPos,
            string label,
            Color fillColor,
            out Image fillOut,
            out TMP_Text labelOut)
        {
            // Container
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(transform, false);
            row.layer = gameObject.layer;
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin        = new Vector2(0f, 0f);
            rowRt.anchorMax        = new Vector2(0f, 0f);
            rowRt.pivot            = new Vector2(0f, 0f);
            rowRt.sizeDelta        = new Vector2(BarWidth, BarHeight + UIStyle.FontSizeBody + 2f);
            rowRt.anchoredPosition = new Vector2(16f, yPos);

            // Bar background (UIStyle.Panel)
            RectTransform bgRt = UIBuilder.Panel(row.transform, name + "_Bg");
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(0f, 0f);
            bgRt.pivot     = new Vector2(0f, 0f);
            bgRt.sizeDelta = new Vector2(BarWidth, BarHeight);
            bgRt.anchoredPosition = Vector2.zero;

            // Border (1px Accent stroke around the bar background)
            // We use a child stretched rect tinted UIStyle.Border, then layer
            // the fill above it so the bar reads as a framed pill.
            UIBuilder.Border(bgRt, UIStyle.Border);

            // Fill image (filled horizontally)
            GameObject fillGO = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(bgRt, false);
            fillGO.layer = gameObject.layer;
            Image fill = fillGO.GetComponent<Image>();
            fill.color         = fillColor;
            fill.raycastTarget = false;
            // No sprite assigned — Unity falls back to the built-in UISprite,
            // which is a solid white quad and supports Image.type = Filled.
            fill.type           = Image.Type.Filled;
            fill.fillMethod     = Image.FillMethod.Horizontal;
            fill.fillOrigin     = (int)Image.OriginHorizontal.Left;
            fill.fillAmount     = 1f;
            RectTransform fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(UIStyle.BorderWidth, UIStyle.BorderWidth);
            fillRt.offsetMax = new Vector2(-UIStyle.BorderWidth, -UIStyle.BorderWidth);

            // Label (above the bar): "<NAME>: current / max"
            TextMeshProUGUI nameTmp = UIBuilder.Text(
                row.transform, label, UIStyle.FontSizeBody, UIStyle.TextDim, name + "_Name");
            nameTmp.alignment = TextAlignmentOptions.Left;
            RectTransform nameRt = nameTmp.rectTransform;
            nameRt.anchorMin        = new Vector2(0f, 1f);
            nameRt.anchorMax        = new Vector2(0f, 1f);
            nameRt.pivot            = new Vector2(0f, 1f);
            nameRt.sizeDelta        = new Vector2(60f, UIStyle.FontSizeBody + 2f);
            nameRt.anchoredPosition = new Vector2(0f, 0f);

            TextMeshProUGUI valueTmp = UIBuilder.Text(
                row.transform, "0 / 0", UIStyle.FontSizeSmall, UIStyle.Text, name + "_Value");
            valueTmp.alignment = TextAlignmentOptions.Right;
            RectTransform valueRt = valueTmp.rectTransform;
            valueRt.anchorMin        = new Vector2(1f, 1f);
            valueRt.anchorMax        = new Vector2(1f, 1f);
            valueRt.pivot            = new Vector2(1f, 1f);
            valueRt.sizeDelta        = new Vector2(BarWidth - 60f - LabelOffsetX, UIStyle.FontSizeBody + 2f);
            valueRt.anchoredPosition = new Vector2(0f, 0f);

            fillOut  = fill;
            labelOut = valueTmp;
        }

        private void BuildTemperatureBar(float yPos)
        {
            GameObject row = new GameObject("TemperatureBar", typeof(RectTransform));
            row.transform.SetParent(transform, false);
            row.layer = gameObject.layer;
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin        = new Vector2(0f, 0f);
            rowRt.anchorMax        = new Vector2(0f, 0f);
            rowRt.pivot            = new Vector2(0f, 0f);
            rowRt.sizeDelta        = new Vector2(BarWidth, BarHeight + UIStyle.FontSizeBody + 2f);
            rowRt.anchoredPosition = new Vector2(16f, yPos);

            // Bar background (UIStyle.Panel with TempColor fill behind the marker).
            RectTransform bgRt = UIBuilder.Panel(row.transform, "TemperatureBar_Bg");
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(0f, 0f);
            bgRt.pivot     = new Vector2(0f, 0f);
            bgRt.sizeDelta = new Vector2(BarWidth, BarHeight);
            bgRt.anchoredPosition = Vector2.zero;
            UIBuilder.Border(bgRt, UIStyle.Border);
            _tempBarRect = bgRt;

            // Fill colour (full-width tinted track to give the bar identity).
            GameObject fillGO = new GameObject("TemperatureBar_Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(bgRt, false);
            fillGO.layer = gameObject.layer;
            Image fill = fillGO.GetComponent<Image>();
            fill.color         = TempColor;
            fill.raycastTarget = false;
            fill.type          = Image.Type.Simple;
            RectTransform fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(UIStyle.BorderWidth, UIStyle.BorderWidth);
            fillRt.offsetMax = new Vector2(-UIStyle.BorderWidth, -UIStyle.BorderWidth);
            _tempFill = fill;

            // Centre tick mark (visual "comfort" indicator).
            GameObject tickGO = new GameObject("TemperatureBar_Centre", typeof(RectTransform), typeof(Image));
            tickGO.transform.SetParent(bgRt, false);
            tickGO.layer = gameObject.layer;
            Image tick = tickGO.GetComponent<Image>();
            tick.color = UIStyle.TextDim;
            tick.raycastTarget = false;
            RectTransform tickRt = tickGO.GetComponent<RectTransform>();
            tickRt.anchorMin = new Vector2(0.5f, 0f);
            tickRt.anchorMax = new Vector2(0.5f, 1f);
            tickRt.pivot     = new Vector2(0.5f, 0.5f);
            tickRt.sizeDelta = new Vector2(1f, 0f);
            tickRt.anchoredPosition = Vector2.zero;

            // Marker (vertical bar that slides along the gauge).
            GameObject markerGO = new GameObject("TemperatureBar_Marker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(bgRt, false);
            markerGO.layer = gameObject.layer;
            Image markerImg = markerGO.GetComponent<Image>();
            markerImg.color         = TempMarkerColor;
            markerImg.raycastTarget = false;
            _tempMarker = markerGO.GetComponent<RectTransform>();
            _tempMarker.anchorMin = new Vector2(0f, 0.5f);
            _tempMarker.anchorMax = new Vector2(0f, 0.5f);
            _tempMarker.pivot     = new Vector2(0f, 0.5f);
            _tempMarker.sizeDelta = new Vector2(MarkerWidth, MarkerHeight);

            // Name label
            TextMeshProUGUI nameTmp = UIBuilder.Text(
                row.transform, "TEMP", UIStyle.FontSizeBody, UIStyle.TextDim, "TemperatureBar_Name");
            nameTmp.alignment = TextAlignmentOptions.Left;
            RectTransform nameRt = nameTmp.rectTransform;
            nameRt.anchorMin        = new Vector2(0f, 1f);
            nameRt.anchorMax        = new Vector2(0f, 1f);
            nameRt.pivot            = new Vector2(0f, 1f);
            nameRt.sizeDelta        = new Vector2(60f, UIStyle.FontSizeBody + 2f);
            nameRt.anchoredPosition = new Vector2(0f, 0f);

            // Value label
            TextMeshProUGUI valueTmp = UIBuilder.Text(
                row.transform, "20°C", UIStyle.FontSizeSmall, UIStyle.Text, "TemperatureBar_Value");
            valueTmp.alignment = TextAlignmentOptions.Right;
            RectTransform valueRt = valueTmp.rectTransform;
            valueRt.anchorMin        = new Vector2(1f, 1f);
            valueRt.anchorMax        = new Vector2(1f, 1f);
            valueRt.pivot            = new Vector2(1f, 1f);
            valueRt.sizeDelta        = new Vector2(BarWidth - 60f - LabelOffsetX, UIStyle.FontSizeBody + 2f);
            valueRt.anchoredPosition = new Vector2(0f, 0f);
            _tempLabel = valueTmp;
        }
    }
}
