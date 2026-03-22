using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Voidborne.UI
{
    /// <summary>
    /// Singleton UI component that flashes a hitmarker crosshair overlay on screen
    /// whenever a shot connects. White flash for a normal hit; red flash for a kill.
    ///
    /// Attach to any persistent GameObject. If <see cref="hitmarkerImage"/> is left
    /// unassigned in the Inspector, a Screen-Space Overlay canvas with a simple
    /// Image is created programmatically at Start as a fallback.
    /// </summary>
    public class HitmarkerUI : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static HitmarkerUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Hide immediately so there is no stale visible hitmarker at boot.
            SetAlpha(0f);
        }

        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------

        [Tooltip("Image used as the hitmarker overlay. Built programmatically if null.")]
        [SerializeField] private Image hitmarkerImage;

        [Tooltip("Color of the hitmarker for a normal (non-kill) hit.")]
        [SerializeField] private Color normalHitColor = Color.white;

        [Tooltip("Color of the hitmarker for a kill-confirmed hit.")]
        [SerializeField] private Color killColor = Color.red;

        [Tooltip("Seconds the hitmarker is displayed at full opacity.")]
        [SerializeField] private float flashDuration = 0.1f;

        [Tooltip("Seconds it takes for the hitmarker to fade out after the flash.")]
        [SerializeField] private float fadeOutTime = 0.3f;

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        private Coroutine _fadeCoroutine;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Start()
        {
            if (hitmarkerImage == null)
                BuildFallbackHitmarker();
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Triggers the hitmarker flash. Call after a confirmed shot connection.
        /// </summary>
        /// <param name="isKill">When true the hitmarker uses the kill colour (red).</param>
        public void ShowHit(bool isKill)
        {
            if (hitmarkerImage == null)
                return;

            // Stop any in-progress fade so we can restart cleanly.
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            Color baseColor = isKill ? killColor : normalHitColor;
            baseColor.a = 1f;
            hitmarkerImage.color = baseColor;

            _fadeCoroutine = StartCoroutine(FadeOut(baseColor));
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private IEnumerator FadeOut(Color baseColor)
        {
            // Hold at full opacity for the flash duration.
            yield return new WaitForSeconds(flashDuration);

            // Lerp alpha from 1 → 0 over fadeOutTime.
            float elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutTime);
                SetAlpha(alpha);
                yield return null;
            }

            SetAlpha(0f);
            _fadeCoroutine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (hitmarkerImage == null)
                return;

            Color c = hitmarkerImage.color;
            c.a = alpha;
            hitmarkerImage.color = c;
        }

        /// <summary>
        /// Builds a minimal hitmarker Image on a Screen-Space Overlay Canvas
        /// when no prefab reference was provided in the Inspector.
        /// The hitmarker is a small white square (tinted per hit type at runtime).
        /// </summary>
        private void BuildFallbackHitmarker()
        {
            // Create a dedicated canvas so this overlay is always on top.
            GameObject canvasGO = new GameObject("HitmarkerCanvas_Auto");
            canvasGO.transform.SetParent(transform);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Image object centred on screen — a simple + shape approximated by a
            // small white square until a proper sprite is assigned.
            GameObject imgGO = new GameObject("HitmarkerImage_Auto");
            imgGO.transform.SetParent(canvasGO.transform, false);

            hitmarkerImage = imgGO.AddComponent<Image>();
            hitmarkerImage.color = new Color(1f, 1f, 1f, 0f); // fully transparent initially

            RectTransform rt = hitmarkerImage.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(24f, 24f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
