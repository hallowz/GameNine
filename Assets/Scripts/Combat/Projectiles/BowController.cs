using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Voidborne.Player;

namespace Voidborne.Combat.Projectiles
{
    /// <summary>
    /// Lives on the Player GameObject.
    /// Handles bow draw, fire, stamina drain, aim wobble, and a runtime draw UI
    /// (draw-power bar + accuracy ring) created procedurally in Awake.
    ///
    /// Controls:
    ///   Hold RMB  — draw the bow (stamina drains, power builds)
    ///   Release   — fire (if drawn past minDrawTime)
    ///   Over-draw — aim wobbles after wobbleStartDelay seconds past full draw
    /// </summary>
    public class BowController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [SerializeField] private Transform cameraTransform;
        [SerializeField] private FirstPersonController fpsController;
        [SerializeField] private ProjectilePool projectilePool;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private BowDefinition _def;
        private bool          _equipped;
        private bool          _drawing;
        private float         _drawTime;

        // UI
        private Canvas _bowCanvas;
        private Image  _drawBarBg;
        private Image  _drawBarFill;
        private Image  _accuracyRing;

        private const float BarMaxWidth = 196f;
        private const float BarHeight   = 8f;
        private const float BarOffsetY  = -60f;
        private const float RingBaseSize = 40f;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>0–1 draw fraction (0 when not drawn or no bow equipped).</summary>
        public float DrawRatio => (_def == null || _def.fullDrawTime <= 0f)
            ? 0f : Mathf.Clamp01(_drawTime / _def.fullDrawTime);

        /// <summary>True while the player is holding RMB to draw the bow.</summary>
        public bool IsDrawing => _drawing;

        /// <summary>Equip a bow definition — shows the UI and enables drawing.</summary>
        public void Equip(BowDefinition def)
        {
            _def      = def;
            _equipped = true;
            _drawing  = false;
            _drawTime = 0f;

            if (_bowCanvas != null)
                _bowCanvas.gameObject.SetActive(true);
        }

        /// <summary>Unequip the bow — hides the UI and stops any active draw.</summary>
        public void Unequip()
        {
            _def      = null;
            _equipped = false;
            _drawing  = false;
            _drawTime = 0f;

            if (_bowCanvas != null)
                _bowCanvas.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (fpsController == null)
                fpsController = GetComponent<FirstPersonController>();

            if (projectilePool == null)
                projectilePool = FindFirstObjectByType<ProjectilePool>();

            CreateUI();
        }

        private void OnDestroy()
        {
            // Canvas lives at scene root — destroy it when this component is removed
            if (_bowCanvas != null)
                Destroy(_bowCanvas.gameObject);
        }

        private void Update()
        {
            if (!_equipped) return;

            // Block bow input when any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            bool rmbPressed  = mouse.leftButton.wasPressedThisFrame;
            bool rmbHeld     = mouse.leftButton.isPressed;
            bool rmbReleased = mouse.leftButton.wasReleasedThisFrame;

            if (rmbPressed && !_drawing)
            {
                _drawing  = true;
                _drawTime = 0f;
            }

            if (rmbHeld && _drawing)
            {
                _drawTime += Time.deltaTime;

                if (_def != null)
                    fpsController?.ConsumeStamina(_def.staminaDrainPerSecond * Time.deltaTime);
            }

            if (rmbReleased && _drawing)
            {
                TryFire();
                _drawing  = false;
                _drawTime = 0f;
            }

            UpdateUI();
        }

        // -----------------------------------------------------------------------
        // Firing
        // -----------------------------------------------------------------------

        private void TryFire()
        {
            if (_def == null || _def.arrowDefinition == null) return;
            if (_drawTime < _def.minDrawTime) return; // not drawn enough

            float drawRatio = Mathf.Clamp01(_drawTime / _def.fullDrawTime);
            float speed     = Mathf.Lerp(_def.minVelocity, _def.maxVelocity, drawRatio);
            Vector3 dir     = GetAimDirection(drawRatio);

            if (projectilePool == null)
            {
                Debug.LogWarning("[BowController] Cannot fire — ProjectilePool is null.");
                return;
            }

            Vector3 spawnPos = cameraTransform.position + cameraTransform.forward * 0.5f;
            projectilePool.Spawn(_def.arrowDefinition, spawnPos, dir, gameObject, speed);
        }

        private Vector3 GetAimDirection(float drawRatio)
        {
            Vector3 dir = cameraTransform.forward;

            // Spread: high at min draw, low at full draw
            float spread = Mathf.Lerp(_def.spreadAtMinDraw, _def.spreadAtFullDraw, drawRatio);

            // Over-draw wobble
            float wobbleElapsed = _drawTime - _def.fullDrawTime - _def.wobbleStartDelay;
            if (wobbleElapsed > 0f)
                spread += Mathf.Clamp01(wobbleElapsed / 1f) * _def.wobbleMaxAngle;

            if (spread > 0f)
            {
                float half    = spread * 0.5f;
                float pitchDeg = Random.Range(-half, half);
                float yawDeg   = Random.Range(-half, half);

                dir = Quaternion.AngleAxis(yawDeg,   cameraTransform.up)
                    * Quaternion.AngleAxis(pitchDeg, cameraTransform.right)
                    * dir;
            }

            return dir.normalized;
        }

        // -----------------------------------------------------------------------
        // UI creation
        // -----------------------------------------------------------------------

        private void CreateUI()
        {
            // Root canvas — Screen Space Overlay, at scene root (not parented to Player)
            // so it renders correctly regardless of the Player's transform or layer.
            GameObject canvasGO = new GameObject("BowDrawCanvas");
            _bowCanvas = canvasGO.AddComponent<Canvas>();
            _bowCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _bowCanvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Draw bar — background
            GameObject bgGO = new GameObject("DrawBarBg");
            bgGO.transform.SetParent(canvasGO.transform, false);
            _drawBarBg = bgGO.AddComponent<Image>();
            _drawBarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
            bgRect.pivot            = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta        = new Vector2(BarMaxWidth + 4f, BarHeight + 4f);
            bgRect.anchoredPosition = new Vector2(0f, BarOffsetY);

            // Draw bar — fill (anchored to left edge of bg so width grows rightward)
            GameObject fillGO = new GameObject("DrawBarFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            _drawBarFill = fillGO.AddComponent<Image>();
            _drawBarFill.color = Color.green;

            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin        = new Vector2(0f, 0.5f);
            fillRect.anchorMax        = new Vector2(0f, 0.5f);
            fillRect.pivot            = new Vector2(0f, 0.5f);
            fillRect.sizeDelta        = new Vector2(0f, BarHeight);
            fillRect.anchoredPosition = new Vector2(2f, 0f);

            // Accuracy ring — square image at screen center, scales with spread
            GameObject ringGO = new GameObject("AccuracyRing");
            ringGO.transform.SetParent(canvasGO.transform, false);
            _accuracyRing = ringGO.AddComponent<Image>();
            _accuracyRing.color = Color.white;

            RectTransform ringRect = ringGO.GetComponent<RectTransform>();
            ringRect.anchorMin        = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax        = new Vector2(0.5f, 0.5f);
            ringRect.pivot            = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta        = new Vector2(RingBaseSize, RingBaseSize);
            ringRect.anchoredPosition = Vector2.zero;

            // Hide until equipped
            canvasGO.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // UI update
        // -----------------------------------------------------------------------

        private void UpdateUI()
        {
            if (!_equipped || _def == null)
            {
                SetBarVisible(false);
                SetRingScale(1f, Color.white);
                return;
            }

            if (!_drawing)
            {
                SetBarVisible(false);
                SetRingScale(0.5f, Color.white);
                return;
            }

            // Bar
            SetBarVisible(true);
            float ratio = DrawRatio;
            _drawBarFill.rectTransform.sizeDelta = new Vector2(ratio * BarMaxWidth, BarHeight);

            float wobbleElapsed = _drawTime - _def.fullDrawTime - _def.wobbleStartDelay;
            bool  isWobbling    = wobbleElapsed > 0f;

            Color barColor = isWobbling ? Color.red
                           : ratio >= 1f ? Color.yellow
                           : Color.green;
            _drawBarFill.color = barColor;

            // Accuracy ring — larger = worse accuracy
            float spread = Mathf.Lerp(_def.spreadAtMinDraw, _def.spreadAtFullDraw, ratio);
            if (isWobbling)
                spread += Mathf.Clamp01(wobbleElapsed / 1f) * _def.wobbleMaxAngle;

            float maxSpread = _def.spreadAtMinDraw + _def.wobbleMaxAngle;
            float spreadT   = Mathf.InverseLerp(_def.spreadAtFullDraw, maxSpread, spread);
            float ringScale  = Mathf.Lerp(0.5f, 2.5f, spreadT);

            SetRingScale(ringScale, barColor);
        }

        private void SetBarVisible(bool visible)
        {
            if (_drawBarBg != null)
                _drawBarBg.gameObject.SetActive(visible);
        }

        private void SetRingScale(float scale, Color color)
        {
            if (_accuracyRing == null) return;
            _accuracyRing.rectTransform.sizeDelta = new Vector2(RingBaseSize * scale, RingBaseSize * scale);
            _accuracyRing.color = color;
        }
    }
}
