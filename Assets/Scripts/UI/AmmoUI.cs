using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Voidborne.Combat;

namespace Voidborne.UI
{
    /// <summary>
    /// HUD panel (bottom-right) showing:
    ///   - Current magazine count  (large, white → red when low)
    ///   - Reserve ammo count      (smaller, grey)
    ///   - Reload progress bar     (appears only while reloading)
    ///
    /// Self-builds its UI hierarchy in Start(). Hidden entirely when no gun is equipped.
    /// UIManager positions the RectTransform before Start() runs.
    /// </summary>
    public class AmmoUI : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Config
        // -----------------------------------------------------------------------

        private const float PanelWidth    = 180f;
        private const float AmmoRowHeight = 42f;
        private const float ReloadHeight  = 20f;

        private static readonly Color ColNormal   = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ColLowAmmo  = new Color(0.95f, 0.25f, 0.15f);
        private static readonly Color ColReserve  = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ColReload   = new Color(0.90f, 0.78f, 0.20f);
        private static readonly Color ColBg       = new Color(0.06f, 0.06f, 0.08f, 0.72f);
        private static readonly Color ColBarBg    = new Color(0.20f, 0.20f, 0.22f, 0.90f);

        /// <summary>Fraction of full magazine below which the ammo number turns red.</summary>
        private const float LowAmmoFraction = 0.25f;

        // -----------------------------------------------------------------------
        // Runtime references (filled by BuildPanel)
        // -----------------------------------------------------------------------

        private GunController _gunController;
        private ReloadSystem  _reloadSystem;

        private TMP_Text _currentAmmoText;
        private TMP_Text _reserveAmmoText;

        private GameObject    _reloadGroup;
        private Image         _reloadBarFill;
        private RectTransform _reloadBarBgRT;  // used to measure bar width

        // Cached previous values to avoid per-frame string allocations
        private int _prevCurrentAmmo = -1;
        private int _prevReserveAmmo = -1;
        private float _cachedBarWidth;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Start()
        {
            _gunController = FindFirstObjectByType<GunController>();
            _reloadSystem  = FindFirstObjectByType<ReloadSystem>();

            BuildPanel();
        }

        private void Update()
        {
            GunInstance gun = _gunController != null ? _gunController.CurrentGun : null;

            if (gun == null)
            {
                // No gun equipped — hide the panel.
                SetCanvasAlpha(0f);
                return;
            }

            SetCanvasAlpha(1f);

            // ---- Ammo counts (only update text when value changes) ----
            if (gun.CurrentAmmo != _prevCurrentAmmo)
            {
                _prevCurrentAmmo = gun.CurrentAmmo;
                _currentAmmoText.text = gun.CurrentAmmo.ToString();
            }
            if (gun.ReserveAmmo != _prevReserveAmmo)
            {
                _prevReserveAmmo = gun.ReserveAmmo;
                _reserveAmmoText.text = gun.ReserveAmmo.ToString();
            }

            float fraction = gun.Definition.magazineSize > 0
                ? (float)gun.CurrentAmmo / gun.Definition.magazineSize
                : 1f;
            _currentAmmoText.color = fraction <= LowAmmoFraction ? ColLowAmmo : ColNormal;

            // ---- Reload section ----
            bool reloading = gun.IsReloading;
            _reloadGroup.SetActive(reloading);

            if (reloading && _reloadBarBgRT != null)
            {
                float progress  = _reloadSystem != null ? _reloadSystem.ReloadProgress : 0f;
                RectTransform fillRT = _reloadBarFill.rectTransform;
                // Anchor left edge fixed, scale width by progress.
                if (_cachedBarWidth == 0f) _cachedBarWidth = _reloadBarBgRT.rect.width;
                fillRT.sizeDelta = new Vector2(_cachedBarWidth * progress, fillRT.sizeDelta.y);
            }
        }

        // -----------------------------------------------------------------------
        // Panel construction
        // -----------------------------------------------------------------------

        private void BuildPanel()
        {
            float totalHeight = AmmoRowHeight + ReloadHeight;

            // Size the root RectTransform (UIManager sets anchors/pivot/position).
            RectTransform rootRT = GetComponent<RectTransform>();
            if (rootRT == null) rootRT = gameObject.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(PanelWidth, totalHeight);

            // ---- Background ----
            Image bg = gameObject.AddComponent<Image>();
            bg.color = ColBg;

            // ================================================================
            // RELOAD ROW — top section, only active during reload
            // ================================================================
            _reloadGroup = new GameObject("ReloadGroup", typeof(RectTransform));
            _reloadGroup.transform.SetParent(transform, false);

            RectTransform reloadRT = _reloadGroup.GetComponent<RectTransform>();
            reloadRT.anchorMin        = new Vector2(0f, 1f);
            reloadRT.anchorMax        = new Vector2(1f, 1f);
            reloadRT.pivot            = new Vector2(0f, 1f);
            reloadRT.anchoredPosition = Vector2.zero;
            reloadRT.sizeDelta        = new Vector2(0f, ReloadHeight);

            // "RELOADING" label — left side
            GameObject labelGO = new GameObject("ReloadLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(_reloadGroup.transform, false);
            TMP_Text label = labelGO.GetComponent<TMP_Text>();
            label.text          = "RELOADING";
            label.color         = ColReload;
            label.fontSize      = 9f;
            label.fontStyle     = FontStyles.Bold;
            label.alignment     = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin        = new Vector2(0f, 0f);
            labelRT.anchorMax        = new Vector2(0.4f, 1f);
            labelRT.offsetMin        = new Vector2(8f, 2f);
            labelRT.offsetMax        = new Vector2(0f, -2f);

            // Progress bar background
            GameObject barBgGO = new GameObject("ReloadBarBg", typeof(RectTransform), typeof(Image));
            barBgGO.transform.SetParent(_reloadGroup.transform, false);
            barBgGO.GetComponent<Image>().color = ColBarBg;
            _reloadBarBgRT = barBgGO.GetComponent<RectTransform>();
            _reloadBarBgRT.anchorMin        = new Vector2(0.38f, 0.15f);
            _reloadBarBgRT.anchorMax        = new Vector2(1f,    0.85f);
            _reloadBarBgRT.offsetMin        = new Vector2(4f,  0f);
            _reloadBarBgRT.offsetMax        = new Vector2(-8f, 0f);

            // Progress bar fill — anchored to left edge of barBg, width driven in Update
            GameObject barFillGO = new GameObject("ReloadBarFill", typeof(RectTransform), typeof(Image));
            barFillGO.transform.SetParent(barBgGO.transform, false);
            _reloadBarFill = barFillGO.GetComponent<Image>();
            _reloadBarFill.color = ColReload;
            RectTransform fillRT = barFillGO.GetComponent<RectTransform>();
            // Anchor to left edge; we drive width via sizeDelta.x in Update.
            fillRT.anchorMin        = new Vector2(0f, 0f);
            fillRT.anchorMax        = new Vector2(0f, 1f);
            fillRT.pivot            = new Vector2(0f, 0.5f);
            fillRT.anchoredPosition = Vector2.zero;
            fillRT.sizeDelta        = new Vector2(0f, 0f);

            _reloadGroup.SetActive(false);

            // ================================================================
            // AMMO ROW — lower section, always visible
            // ================================================================
            GameObject ammoRow = new GameObject("AmmoRow", typeof(RectTransform));
            ammoRow.transform.SetParent(transform, false);

            RectTransform ammoRT = ammoRow.GetComponent<RectTransform>();
            ammoRT.anchorMin        = new Vector2(0f, 0f);
            ammoRT.anchorMax        = new Vector2(1f, 0f);
            ammoRT.pivot            = new Vector2(0f, 0f);
            ammoRT.anchoredPosition = Vector2.zero;
            ammoRT.sizeDelta        = new Vector2(0f, AmmoRowHeight);

            // Current ammo — large, right of centre
            GameObject curGO = new GameObject("CurrentAmmo", typeof(RectTransform), typeof(TextMeshProUGUI));
            curGO.transform.SetParent(ammoRow.transform, false);
            _currentAmmoText = curGO.GetComponent<TMP_Text>();
            _currentAmmoText.color         = ColNormal;
            _currentAmmoText.fontSize      = 30f;
            _currentAmmoText.fontStyle     = FontStyles.Bold;
            _currentAmmoText.alignment     = TextAlignmentOptions.MidlineRight;
            _currentAmmoText.raycastTarget = false;
            RectTransform curRT = curGO.GetComponent<RectTransform>();
            curRT.anchorMin        = new Vector2(0f, 0f);
            curRT.anchorMax        = new Vector2(0.52f, 1f);
            curRT.offsetMin        = new Vector2(4f,  2f);
            curRT.offsetMax        = new Vector2(-2f, -2f);

            // Separator " / "
            GameObject sepGO = new GameObject("Separator", typeof(RectTransform), typeof(TextMeshProUGUI));
            sepGO.transform.SetParent(ammoRow.transform, false);
            TMP_Text sep = sepGO.GetComponent<TMP_Text>();
            sep.text          = "/";
            sep.color         = ColReserve;
            sep.fontSize      = 14f;
            sep.alignment     = TextAlignmentOptions.Midline;
            sep.raycastTarget = false;
            RectTransform sepRT = sepGO.GetComponent<RectTransform>();
            sepRT.anchorMin        = new Vector2(0.50f, 0f);
            sepRT.anchorMax        = new Vector2(0.58f, 1f);
            sepRT.offsetMin        = Vector2.zero;
            sepRT.offsetMax        = Vector2.zero;

            // Reserve ammo — smaller, grey, right side
            GameObject resGO = new GameObject("ReserveAmmo", typeof(RectTransform), typeof(TextMeshProUGUI));
            resGO.transform.SetParent(ammoRow.transform, false);
            _reserveAmmoText = resGO.GetComponent<TMP_Text>();
            _reserveAmmoText.color         = ColReserve;
            _reserveAmmoText.fontSize      = 16f;
            _reserveAmmoText.fontStyle     = FontStyles.Normal;
            _reserveAmmoText.alignment     = TextAlignmentOptions.MidlineLeft;
            _reserveAmmoText.raycastTarget = false;
            RectTransform resRT = resGO.GetComponent<RectTransform>();
            resRT.anchorMin        = new Vector2(0.56f, 0f);
            resRT.anchorMax        = new Vector2(1f,    1f);
            resRT.offsetMin        = new Vector2(2f,   4f);
            resRT.offsetMax        = new Vector2(-6f, -4f);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private CanvasGroup _canvasGroup;

        private void SetCanvasAlpha(float alpha)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetOrAddCanvasGroup();

            _canvasGroup.alpha = alpha;
        }

        private CanvasGroup GetOrAddCanvasGroup()
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            return cg != null ? cg : gameObject.AddComponent<CanvasGroup>();
        }
    }
}
