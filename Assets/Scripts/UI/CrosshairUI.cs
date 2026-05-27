using UnityEngine;
using UnityEngine.UI;
using Voidborne.Combat;
using Voidborne.UI.Style;

namespace Voidborne.UI
{
    /// <summary>
    /// Dynamic crosshair that expands and contracts in real time to reflect the
    /// gun's current spread angle. Reads GunController.CurrentSpread each frame
    /// and moves four line RectTransforms outward from the centre.
    ///
    /// Tuning constants are NOT serialized so that code changes take effect
    /// immediately without needing to reset existing scene components.
    ///
    /// GunController is resolved automatically at Start if not wired in the Inspector.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector fields — crosshair line references (wired by CombatSetup or manually)
        // -----------------------------------------------------------------------

        [Tooltip("RectTransform for the top crosshair line.")]
        [SerializeField] private RectTransform topLine;

        [Tooltip("RectTransform for the bottom crosshair line.")]
        [SerializeField] private RectTransform bottomLine;

        [Tooltip("RectTransform for the left crosshair line.")]
        [SerializeField] private RectTransform leftLine;

        [Tooltip("RectTransform for the right crosshair line.")]
        [SerializeField] private RectTransform rightLine;

        [Tooltip("The GunController whose CurrentSpread drives the crosshair size. " +
                 "If null, resolved automatically via FindFirstObjectByType at Start.")]
        [SerializeField] private GunController gunController;

        // -----------------------------------------------------------------------
        // Tuning — NOT serialized so code changes always take effect.
        //
        // With spreadToGapScale = 12 and the starter guns:
        //   spread 0.00°  → gap 4 px   (sniper ADS)
        //   spread 0.10°  → gap 5.2 px (sniper standing)
        //   spread 0.50°  → gap 10 px  (revolver standing)
        //   spread 0.80°  → gap 14 px  (AR standing)
        //   spread 1.50°  → gap 22 px  (SMG standing)
        //   spread 4.00°  → gap 52 px  (shotgun standing)
        //   spread 9.00°  → 80 px cap  (shotgun airborne)
        // -----------------------------------------------------------------------

        private const float MinGap          = 4f;    // px from centre at zero spread
        private const float MaxGap          = 80f;   // px cap at maximum spread
        private const float SpreadToGap     = 12f;   // px added per degree of spread
        private const float SmoothSpeed     = 12f;   // Lerp speed toward target gap

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        private float _currentGap;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Start()
        {
            // Auto-find GunController so the crosshair works even if CombatSetup
            // wasn't re-run after changes.
            if (gunController == null)
                gunController = FindFirstObjectByType<GunController>();

            _currentGap = MinGap;

            if (topLine == null || bottomLine == null || leftLine == null || rightLine == null)
                BuildFallbackCrosshair();

            ApplyGap(_currentGap);
        }

        private void Update()
        {
            float spread = (gunController != null) ? gunController.CurrentSpread : 0f;

            float targetGap = Mathf.Clamp(MinGap + spread * SpreadToGap, MinGap, MaxGap);

            _currentGap = Mathf.Lerp(_currentGap, targetGap, SmoothSpeed * Time.deltaTime);

            ApplyGap(_currentGap);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void ApplyGap(float gap)
        {
            if (topLine    != null) topLine.anchoredPosition    = new Vector2( 0f,  gap);
            if (bottomLine != null) bottomLine.anchoredPosition = new Vector2( 0f, -gap);
            if (leftLine   != null) leftLine.anchoredPosition   = new Vector2(-gap,  0f);
            if (rightLine  != null) rightLine.anchoredPosition  = new Vector2( gap,  0f);
        }

        /// <summary>
        /// Creates a Screen-Space Overlay canvas with four white line Images arranged
        /// as a + crosshair, then assigns them to the four RectTransform fields.
        /// </summary>
        private void BuildFallbackCrosshair()
        {
            GameObject canvasGO = new GameObject("CrosshairCanvas_Auto");
            canvasGO.transform.SetParent(transform);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            canvasGO.AddComponent<CanvasScaler>();   // default = ConstantPixelSize, 1:1 pixels
            canvasGO.AddComponent<GraphicRaycaster>();

            topLine    = CreateLine(canvasGO.transform, "Top",    new Vector2(2f, 8f));
            bottomLine = CreateLine(canvasGO.transform, "Bottom", new Vector2(2f, 8f));
            leftLine   = CreateLine(canvasGO.transform, "Left",   new Vector2(8f, 2f));
            rightLine  = CreateLine(canvasGO.transform, "Right",  new Vector2(8f, 2f));
        }

        private static RectTransform CreateLine(Transform parent, string lineName, Vector2 size)
        {
            GameObject go = new GameObject("Crosshair_" + lineName);
            go.transform.SetParent(parent, false);

            Image img  = go.AddComponent<Image>();
            // V4.2 restyle: line tint sourced from UIStyle.Text rather than
            // hardcoded white so a future palette tweak propagates here too.
            img.color  = UIStyle.Text;

            RectTransform rt = img.rectTransform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = Vector2.zero;

            return rt;
        }
    }
}
