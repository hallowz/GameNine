using UnityEngine;
using UnityEngine.UI;
using Voidborne.Combat.Melee;

namespace Voidborne.UI
{
    /// <summary>
    /// Mordhau-style 4-arrow directional indicator that rings the crosshair
    /// when a melee weapon is equipped.
    ///
    /// Arrow layout (pointing outward from centre):
    ///   Up    = AttackDirection.Overhead
    ///   Down  = AttackDirection.Stab
    ///   Left  = AttackDirection.Left
    ///   Right = AttackDirection.Right
    ///
    /// Colour states:
    ///   Dim white  — melee weapon selected, idle
    ///   Orange     — this direction is being wound up or released
    ///   Blue       — this direction is being parried
    ///
    /// Fades out when a gun or empty slot is active.
    /// References are resolved lazily every frame — no manual wiring required.
    /// </summary>
    public class MeleeDirectionUI : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Tooltip("Force the overlay to stay visible regardless of equipped weapon. " +
                 "Useful for checking the UI is working.")]
        [SerializeField] private bool debugAlwaysVisible = false;

        // -------------------------------------------------------------------------
        // Colours
        // -------------------------------------------------------------------------

        private static readonly Color IdleColor   = new Color(1.00f, 1.00f, 1.00f, 0.45f);
        private static readonly Color AttackColor = new Color(1.00f, 0.55f, 0.10f, 1.00f);
        private static readonly Color ParryColor  = new Color(0.30f, 0.72f, 1.00f, 1.00f);

        // -------------------------------------------------------------------------
        // Tuning
        // -------------------------------------------------------------------------

        private const float ArrowRadius    = 28f;
        private const float ArrowSize      = 14f;
        private const float ColorLerpSpeed = 16f;
        private const float FadeSpeed      =  8f;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        // All refs resolved lazily in Update — no Start() dependency ordering issues.
        private MeleeController _melee;
        private ParrySystem     _parry;
        private PlayerInventory _inventory;

        private CanvasGroup _group;
        private Image _upArrow, _downArrow, _leftArrow, _rightArrow;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            if (_group == null) return;

            // Lazy-resolve every frame until all refs are found.
            // This handles any Start() execution-order issues and works even
            // if MeleeSetup is run while in Play mode.
            if (_melee     == null) _melee     = FindFirstObjectByType<MeleeController>();
            if (_parry     == null) _parry     = FindFirstObjectByType<ParrySystem>();
            if (_inventory == null) _inventory = FindFirstObjectByType<PlayerInventory>();

            bool meleeActive = debugAlwaysVisible || IsMeleeWeaponSelected();
            float targetAlpha = meleeActive ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, Time.deltaTime * FadeSpeed);

            if (!meleeActive) return;

            // Determine attack highlight direction.
            bool isAttacking = _melee != null &&
                               (_melee.Phase == AttackPhase.Windup ||
                                _melee.Phase == AttackPhase.Release);
            AttackDirection attackDir = (_melee != null) ? _melee.CurrentDirection : default;

            // Determine parry highlight direction.
            bool isParrying = _parry != null && _parry.IsParrying;
            AttackDirection parryDir = isParrying ? _parry.ParryDirection : default;

            TickArrow(_upArrow,    AttackDirection.Overhead, isAttacking, attackDir, isParrying, parryDir);
            TickArrow(_downArrow,  AttackDirection.Stab,     isAttacking, attackDir, isParrying, parryDir);
            TickArrow(_leftArrow,  AttackDirection.Left,     isAttacking, attackDir, isParrying, parryDir);
            TickArrow(_rightArrow, AttackDirection.Right,    isAttacking, attackDir, isParrying, parryDir);
        }

        // -------------------------------------------------------------------------
        // Visibility check
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true if the player currently has a melee weapon active.
        /// Uses two independent checks so the overlay works even before
        /// WeaponSwitcher has had a chance to call MeleeController.Equip().
        /// </summary>
        private bool IsMeleeWeaponSelected()
        {
            // Primary: MeleeController already has a weapon equipped.
            if (_melee != null && _melee.EquippedWeapon != null)
                return true;

            // Fallback: active hotbar slot contains a melee WeaponItem.
            if (_inventory != null)
            {
                ItemStack slot = _inventory.ActiveHotbarItem;
                if (!slot.IsEmpty && slot.item is WeaponItem wi && wi.meleeDefinition != null)
                    return true;
            }

            return false;
        }

        // -------------------------------------------------------------------------
        // Per-arrow colour logic
        // -------------------------------------------------------------------------

        private void TickArrow(Image arrow, AttackDirection thisDir,
            bool isAttacking, AttackDirection attackDir,
            bool isParrying,  AttackDirection parryDir)
        {
            if (arrow == null) return;

            Color target;
            if      (isParrying  && parryDir  == thisDir) target = ParryColor;
            else if (isAttacking && attackDir == thisDir) target = AttackColor;
            else                                          target = IdleColor;

            arrow.color = Color.Lerp(arrow.color, target, Time.deltaTime * ColorLerpSpeed);
        }

        // -------------------------------------------------------------------------
        // Canvas + arrow construction (called from Awake)
        // -------------------------------------------------------------------------

        private void Build()
        {
            // Canvas — Screen Space Overlay so it always draws on top.
            var canvasGO        = new GameObject("MeleeDirectionCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas          = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;          // above CrosshairUI (99)

            canvasGO.AddComponent<CanvasScaler>();

            var raycaster           = canvasGO.AddComponent<GraphicRaycaster>();
            raycaster.blockingMask  = 0;        // never consume input

            _group                  = canvasGO.AddComponent<CanvasGroup>();
            _group.alpha            = 0f;
            _group.blocksRaycasts   = false;
            _group.interactable     = false;

            // Single shared sprite — rotated per arrow.
            Sprite sprite = BuildArrowSprite();

            _upArrow    = MakeArrow(canvasGO.transform, sprite, "Up",
                new Vector2(0f,  ArrowRadius),    0f);

            _downArrow  = MakeArrow(canvasGO.transform, sprite, "Down",
                new Vector2(0f, -ArrowRadius),  180f);

            _leftArrow  = MakeArrow(canvasGO.transform, sprite, "Left",
                new Vector2(-ArrowRadius, 0f),   90f);

            _rightArrow = MakeArrow(canvasGO.transform, sprite, "Right",
                new Vector2( ArrowRadius, 0f),  -90f);
        }

        private static Image MakeArrow(Transform parent, Sprite sprite,
            string label, Vector2 anchoredPos, float zRotation)
        {
            var go              = new GameObject("Arrow_" + label);
            go.transform.SetParent(parent, false);

            var img             = go.AddComponent<Image>();
            img.sprite          = sprite;
            img.color           = IdleColor;
            img.preserveAspect  = true;

            var rt              = img.rectTransform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(ArrowSize, ArrowSize);
            rt.anchoredPosition = anchoredPos;
            rt.localRotation    = Quaternion.Euler(0f, 0f, zRotation);

            return img;
        }

        // -------------------------------------------------------------------------
        // Procedural arrow sprite — upward-pointing filled triangle, 20 × 20 px
        // -------------------------------------------------------------------------

        private static Sprite BuildArrowSprite()
        {
            const int S  = 20;
            const int CX = S / 2;

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };

            // Start fully transparent.
            var pixels = new Color[S * S];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            tex.SetPixels(pixels);

            // Fill isoceles triangle: base at y=0 (full width), apex at y=S-1 (1 px).
            for (int y = 0; y < S; y++)
            {
                float t  = (float)y / (S - 1);          // 0 = base, 1 = apex
                int   hw = Mathf.RoundToInt((1f - t) * CX);

                for (int x = CX - hw; x <= CX + hw; x++)
                    if (x >= 0 && x < S)
                        tex.SetPixel(x, y, Color.white);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        }
    }
}
