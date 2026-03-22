using UnityEngine;
using UnityEngine.UI;
using Voidborne.Vehicles;

namespace Voidborne.UI
{
    /// <summary>
    /// Self-building vehicle HUD. Creates a circular instrument panel in the bottom-right
    /// showing speed, RPM gauge, gear indicator, and fuel bar.
    /// Built entirely in code — no prefab wiring needed.
    /// </summary>
    public class VehicleHUD : MonoBehaviour
    {
        private VehicleBase _vehicle;
        private VehicleDamageSystem _damageSystem;
        private VehicleFuel _fuel;
        private VehicleDrivetrain _drivetrain;
        private Rigidbody _rb;

        // Built UI elements
        private GameObject _panel;
        private Text _speedText;
        private Text _speedUnitText;
        private Text _gearText;
        private Text _rpmText;
        private Image _rpmFill;
        private Image _fuelFill;
        private bool _built;

        // Cached color constants to avoid per-frame allocation
        private static readonly Color ColRpmHigh  = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ColRpmNorm  = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color ColFuelLow  = new Color(0.9f, 0.3f, 0.2f);
        private static readonly Color ColFuelNorm = new Color(0.3f, 0.7f, 0.9f);

        // Cached previous values to skip redundant text updates
        private int _prevSpeedKmh = -1;
        private int _prevRpm = -1;
        private string _prevGear;

        // Cached font resource
        private static Font _cachedFont;

        // Panel config
        private const float PanelSize = 180f;
        private const float PanelMargin = 20f;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void AttachToVehicle(VehicleBase vehicle)
        {
            _vehicle = vehicle;
            _damageSystem = vehicle.GetComponent<VehicleDamageSystem>();
            _fuel = vehicle.GetComponent<VehicleFuel>();
            _drivetrain = vehicle.GetComponent<VehicleDrivetrain>();
            _rb = vehicle.GetComponent<Rigidbody>();

            gameObject.SetActive(true);

            if (!_built) BuildPanel();
            if (_panel != null) _panel.SetActive(true);
        }

        public void Detach()
        {
            _vehicle = null;
            _damageSystem = null;
            _fuel = null;
            _drivetrain = null;
            _rb = null;

            if (_panel != null) _panel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_vehicle == null) return;

            // Speed (only update text when integer value changes)
            if (_rb != null && _speedText != null)
            {
                int kmh = Mathf.RoundToInt(_rb.linearVelocity.magnitude * 3.6f);
                if (kmh != _prevSpeedKmh)
                {
                    _prevSpeedKmh = kmh;
                    _speedText.text = kmh.ToString();
                }
            }

            // RPM gauge + gear
            if (_drivetrain != null && _drivetrain.IsActive)
            {
                if (_rpmFill != null)
                {
                    _rpmFill.fillAmount = _drivetrain.RPMNormalized * 0.75f; // 270 deg arc
                    _rpmFill.color = _drivetrain.RPMNormalized > 0.85f ? ColRpmHigh : ColRpmNorm;
                }
                if (_gearText != null)
                {
                    string gear = _drivetrain.GearDisplay;
                    if (gear != _prevGear) { _prevGear = gear; _gearText.text = gear; }
                }
                if (_rpmText != null)
                {
                    int rpm = Mathf.RoundToInt(_drivetrain.CurrentRPM);
                    if (rpm != _prevRpm) { _prevRpm = rpm; _rpmText.text = rpm.ToString(); }
                }
            }
            else
            {
                if (_rpmFill != null) _rpmFill.fillAmount = 0f;
                if (_gearText != null && _prevGear != "") { _prevGear = ""; _gearText.text = ""; }
                if (_rpmText != null && _prevRpm != 0) { _prevRpm = 0; _rpmText.text = ""; }
            }

            // Fuel bar
            if (_fuel != null && _fuelFill != null)
            {
                _fuelFill.fillAmount = _fuel.FuelRatio;
                _fuelFill.color = _fuel.FuelRatio < 0.2f ? ColFuelLow : ColFuelNorm;
            }
        }

        // ─── Build the instrument panel in code ─────────────────────────

        private void BuildPanel()
        {
            _built = true;

            // Panel container — bottom-right, circular cluster
            _panel = new GameObject("InstrumentPanel", typeof(RectTransform), typeof(CanvasGroup));
            _panel.transform.SetParent(transform, false);
            var panelRT = _panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1, 0);
            panelRT.anchorMax = new Vector2(1, 0);
            panelRT.pivot = new Vector2(1, 0);
            panelRT.anchoredPosition = new Vector2(-PanelMargin, PanelMargin);
            panelRT.sizeDelta = new Vector2(PanelSize, PanelSize);

            // Dark circular background
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(_panel.transform, false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // RPM arc (radial fill behind everything)
            var rpmBgGO = new GameObject("RPM_BG", typeof(RectTransform), typeof(Image));
            rpmBgGO.transform.SetParent(_panel.transform, false);
            var rpmBgImg = rpmBgGO.GetComponent<Image>();
            rpmBgImg.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            rpmBgImg.type = Image.Type.Filled;
            rpmBgImg.fillMethod = Image.FillMethod.Radial360;
            rpmBgImg.fillOrigin = (int)Image.Origin360.Bottom;
            rpmBgImg.fillClockwise = true;
            rpmBgImg.fillAmount = 0.75f;
            var rpmBgRT = rpmBgGO.GetComponent<RectTransform>();
            rpmBgRT.anchorMin = new Vector2(0.1f, 0.1f);
            rpmBgRT.anchorMax = new Vector2(0.9f, 0.9f);
            rpmBgRT.offsetMin = Vector2.zero;
            rpmBgRT.offsetMax = Vector2.zero;

            // RPM fill arc (colored)
            var rpmGO = new GameObject("RPM_Fill", typeof(RectTransform), typeof(Image));
            rpmGO.transform.SetParent(_panel.transform, false);
            _rpmFill = rpmGO.GetComponent<Image>();
            _rpmFill.color = new Color(0.3f, 0.85f, 0.4f);
            _rpmFill.type = Image.Type.Filled;
            _rpmFill.fillMethod = Image.FillMethod.Radial360;
            _rpmFill.fillOrigin = (int)Image.Origin360.Bottom;
            _rpmFill.fillClockwise = true;
            _rpmFill.fillAmount = 0f;
            var rpmRT = rpmGO.GetComponent<RectTransform>();
            rpmRT.anchorMin = new Vector2(0.1f, 0.1f);
            rpmRT.anchorMax = new Vector2(0.9f, 0.9f);
            rpmRT.offsetMin = Vector2.zero;
            rpmRT.offsetMax = Vector2.zero;

            // Inner dark circle (center of gauge)
            var innerGO = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGO.transform.SetParent(_panel.transform, false);
            var innerImg = innerGO.GetComponent<Image>();
            innerImg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            var innerRT = innerGO.GetComponent<RectTransform>();
            innerRT.anchorMin = new Vector2(0.2f, 0.2f);
            innerRT.anchorMax = new Vector2(0.8f, 0.8f);
            innerRT.offsetMin = Vector2.zero;
            innerRT.offsetMax = Vector2.zero;

            // Speed number (big, center)
            _speedText = CreateText(_panel.transform, "SpeedText", "",
                new Vector2(0.5f, 0.55f), 28, FontStyle.Bold, Color.white);

            // "km/h" label (small, below speed)
            _speedUnitText = CreateText(_panel.transform, "SpeedUnit", "km/h",
                new Vector2(0.5f, 0.38f), 10, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));

            // Gear indicator (large, bottom-center of gauge)
            _gearText = CreateText(_panel.transform, "GearText", "",
                new Vector2(0.5f, 0.18f), 20, FontStyle.Bold, new Color(0.9f, 0.7f, 0.2f));

            // RPM number (small, top)
            _rpmText = CreateText(_panel.transform, "RPMText", "",
                new Vector2(0.5f, 0.78f), 10, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));

            // RPM label
            CreateText(_panel.transform, "RPMLabel", "RPM",
                new Vector2(0.5f, 0.70f), 8, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f));

            // Fuel bar (horizontal, below the circle)
            var fuelBgGO = new GameObject("FuelBG", typeof(RectTransform), typeof(Image));
            fuelBgGO.transform.SetParent(_panel.transform, false);
            var fuelBgImg = fuelBgGO.GetComponent<Image>();
            fuelBgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
            var fuelBgRT = fuelBgGO.GetComponent<RectTransform>();
            fuelBgRT.anchorMin = new Vector2(0.1f, 0);
            fuelBgRT.anchorMax = new Vector2(0.9f, 0);
            fuelBgRT.pivot = new Vector2(0.5f, 1f);
            fuelBgRT.anchoredPosition = new Vector2(0, -4);
            fuelBgRT.sizeDelta = new Vector2(0, 8);

            var fuelFillGO = new GameObject("FuelFill", typeof(RectTransform), typeof(Image));
            fuelFillGO.transform.SetParent(fuelBgGO.transform, false);
            _fuelFill = fuelFillGO.GetComponent<Image>();
            _fuelFill.color = new Color(0.3f, 0.7f, 0.9f);
            _fuelFill.type = Image.Type.Filled;
            _fuelFill.fillMethod = Image.FillMethod.Horizontal;
            _fuelFill.fillAmount = 1f;
            var fuelFillRT = fuelFillGO.GetComponent<RectTransform>();
            fuelFillRT.anchorMin = Vector2.zero;
            fuelFillRT.anchorMax = Vector2.one;
            fuelFillRT.offsetMin = Vector2.zero;
            fuelFillRT.offsetMax = Vector2.zero;

            // Fuel label
            CreateText(fuelBgGO.transform, "FuelLabel", "FUEL",
                new Vector2(0.5f, 0.5f), 7, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
        }

        private static Text CreateText(Transform parent, string name, string content,
            Vector2 anchorPos, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.font = _cachedFont;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorPos;
            rt.anchorMax = anchorPos;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(120, 30);
            return txt;
        }
    }
}
