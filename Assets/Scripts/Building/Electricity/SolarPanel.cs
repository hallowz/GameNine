using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Surface-only solar generator. Outputs 75W during daytime, 0W at night.
    ///
    /// Placement rules:
    ///   • Y must be &gt; <see cref="minimumSurfaceY"/> (default 0).
    ///   • A raycast upward must not hit terrain within <see cref="skyCheckDistance"/> units.
    ///
    /// Day/night detection:
    ///   Uses <see cref="DayNightCycle.IsDaytime"/> if a <see cref="DayNightCycle"/> singleton
    ///   is present; otherwise falls back to a sun directional light intensity check, then
    ///   finally a time-based sine wave so the panel works even without a dedicated cycle system.
    /// </summary>
    public class SolarPanel : PowerGenerator
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Solar Panel")]
        public float outputWatts       = 75f;
        public float minimumSurfaceY   = 0f;
        [Tooltip("Distance to check upward for terrain obstruction.")]
        public float skyCheckDistance  = 200f;
        [Tooltip("Light intensity below which the panel considers it night (fallback only).")]
        public float nightThreshold    = 0.05f;

        [Header("FX")]
        public Renderer panelRenderer;
        public Color    activeColor  = new Color(0.05f, 0.4f, 0.9f);
        public Color    inactiveColor = new Color(0.1f, 0.1f, 0.15f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private bool _validPlacement;
        private Light _sunLight;

        protected override void Awake()
        {
            base.Awake();
            powerOutput = outputWatts;
        }

        protected override void Start()
        {
            _validPlacement = CheckPlacementValid();

            if (!_validPlacement)
            {
                _isGenerating = false;
                Debug.LogWarning("[SolarPanel] Invalid placement: must be on surface (Y > " +
                                 minimumSurfaceY + ") with clear sky.");
            }

            // Cache directional light for fallback day-check.
            _sunLight = FindSunLight();

            base.Start();
            UpdateOutput();
        }

        private void Update()
        {
            if (!_validPlacement) return;
            UpdateOutput();
        }

        // ── Day/night evaluation ──────────────────────────────────────────────

        private void UpdateOutput()
        {
            bool day = IsDaytime();
            _isGenerating = day;
            UpdateFX();
        }

        private bool IsDaytime()
        {
            // 1. Prefer a DayNightCycle singleton if present.
            if (DayNightCycle.Instance != null)
                return DayNightCycle.Instance.IsDaytime;

            // 2. Fall back to directional light intensity.
            if (_sunLight != null)
                return _sunLight.intensity >= nightThreshold;

            // 3. Last resort: sine-wave simulation (24-minute real-time day).
            float dayProgress = Mathf.Sin(Time.time * Mathf.PI / 720f); // 720s = 12-min half-cycle
            return dayProgress > 0f;
        }

        // ── Placement validation ──────────────────────────────────────────────

        private bool CheckPlacementValid()
        {
            if (transform.position.y <= minimumSurfaceY) return false;

            // Clear sky check — no terrain hit directly above.
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                                Vector3.up, skyCheckDistance, ~0,
                                QueryTriggerInteraction.Ignore))
                return false;

            return true;
        }

        // ── FX ────────────────────────────────────────────────────────────────

        private void UpdateFX()
        {
            if (panelRenderer == null) return;
            panelRenderer.material.color = _isGenerating ? activeColor : inactiveColor;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Light FindSunLight()
        {
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional)
                    return l;
            return null;
        }

        public bool IsValidPlacement => _validPlacement;
        public bool CurrentlyGenerating => _isGenerating;
    }
}
