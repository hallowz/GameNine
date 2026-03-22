using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Voidborne.Combat.Projectiles
{
    /// <summary>
    /// Lives on the Player GameObject.
    /// Handles wind-up and throwing of throwable weapons (spears, axes).
    ///
    /// Controls:
    ///   Hold LMB  — wind up the throw (brief delay, progress bar fills)
    ///   Release   — throw if windupTime has elapsed
    ///
    /// On throw: removes 1× the weapon item from the player's inventory.
    /// When the projectile embeds in terrain (not an enemy): spawns a WorldItem
    /// at the embed point so the player can walk over and retrieve it.
    /// </summary>
    public class ThrowController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [SerializeField] private Transform       cameraTransform;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ProjectilePool  projectilePool;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private ThrowableDefinition _def;
        private WeaponItem          _weaponItem;
        private bool                _equipped;
        private bool                _windingUp;
        private float               _windupTimer;

        // UI
        private Canvas _canvas;
        private Image  _barBg;
        private Image  _barFill;

        private const float BarMaxWidth = 196f;
        private const float BarHeight   = 8f;
        private const float BarOffsetY  = -80f; // slightly below the bow bar

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Equip a throwable weapon and the inventory item that represents it.</summary>
        public void Equip(ThrowableDefinition def, WeaponItem item)
        {
            _def         = def;
            _weaponItem  = item;
            _equipped    = true;
            _windingUp   = false;
            _windupTimer = 0f;

            if (_canvas != null)
                _canvas.gameObject.SetActive(true);
        }

        /// <summary>Unequip — cancels any active wind-up.</summary>
        public void Unequip()
        {
            _def         = null;
            _weaponItem  = null;
            _equipped    = false;
            _windingUp   = false;
            _windupTimer = 0f;

            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            if (projectilePool == null)
                projectilePool = FindFirstObjectByType<ProjectilePool>();

            CreateUI();
        }

        private void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (!_equipped) return;

            // Block throw input when any UI panel is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            bool lmbPressed  = mouse.leftButton.wasPressedThisFrame;
            bool lmbHeld     = mouse.leftButton.isPressed;
            bool lmbReleased = mouse.leftButton.wasReleasedThisFrame;

            if (lmbPressed)
            {
                _windingUp   = true;
                _windupTimer = 0f;
            }

            if (lmbHeld && _windingUp)
                _windupTimer += Time.deltaTime;

            if (lmbReleased && _windingUp)
            {
                if (_windupTimer >= _def.windupTime)
                    Throw();

                _windingUp   = false;
                _windupTimer = 0f;
            }

            UpdateUI();
        }

        // -----------------------------------------------------------------------
        // Throw
        // -----------------------------------------------------------------------

        private void Throw()
        {
            if (_def == null || _def.projectileDefinition == null)
            {
                Debug.LogWarning("[ThrowController] Cannot throw — ThrowableDefinition or projectileDefinition is null.");
                return;
            }
            if (_weaponItem == null)
            {
                Debug.LogWarning("[ThrowController] Cannot throw — WeaponItem reference is null.");
                return;
            }

            playerInventory?.RemoveItem(_weaponItem.itemId, 1);

            WeaponItem thrownItem = _weaponItem;

            Vector3 spawnPos  = cameraTransform.position + cameraTransform.forward * 0.5f;
            Vector3 direction = cameraTransform.forward;

            Projectile p = projectilePool.Spawn(
                _def.projectileDefinition,
                spawnPos,
                direction,
                gameObject,
                _def.throwVelocity);

            if (p != null)
            {
                p.OnEmbedded += (Vector3 pos, bool isEnemy) =>
                    SpawnRecoveryItem(pos, isEnemy, thrownItem);
            }
        }

        // -----------------------------------------------------------------------
        // Recovery WorldItem spawning
        // -----------------------------------------------------------------------

        private static void SpawnRecoveryItem(Vector3 position, bool isEnemy, WeaponItem item)
        {
            if (isEnemy || item == null) return;

            GameObject go = new GameObject("RecoveredThrowable");
            go.transform.position = position + Vector3.up * 0.3f;

            WorldItem worldItem = go.AddComponent<WorldItem>();
            worldItem.itemStack = new ItemStack(item, 1);
        }

        // -----------------------------------------------------------------------
        // UI
        // -----------------------------------------------------------------------

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("ThrowWindupCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Bar background
            GameObject bgGO = new GameObject("WindupBarBg");
            bgGO.transform.SetParent(canvasGO.transform, false);
            _barBg = bgGO.AddComponent<Image>();
            _barBg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
            bgRect.pivot            = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta        = new Vector2(BarMaxWidth + 4f, BarHeight + 4f);
            bgRect.anchoredPosition = new Vector2(0f, BarOffsetY);

            // Bar fill (anchored to left edge of bg)
            GameObject fillGO = new GameObject("WindupBarFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            _barFill = fillGO.AddComponent<Image>();
            _barFill.color = new Color(0.9f, 0.6f, 0.1f, 1f); // orange windup colour

            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin        = new Vector2(0f, 0.5f);
            fillRect.anchorMax        = new Vector2(0f, 0.5f);
            fillRect.pivot            = new Vector2(0f, 0.5f);
            fillRect.sizeDelta        = new Vector2(0f, BarHeight);
            fillRect.anchoredPosition = new Vector2(2f, 0f);

            canvasGO.SetActive(false);
        }

        private void UpdateUI()
        {
            if (_barBg == null) return;

            bool showBar = _windingUp && _def != null;
            _barBg.gameObject.SetActive(showBar);

            if (!showBar) return;

            float ratio = _def.windupTime > 0f
                ? Mathf.Clamp01(_windupTimer / _def.windupTime)
                : 1f;

            _barFill.rectTransform.sizeDelta = new Vector2(ratio * BarMaxWidth, BarHeight);

            // Flash white when fully wound up and ready to throw
            _barFill.color = ratio >= 1f
                ? Color.white
                : new Color(0.9f, 0.6f, 0.1f, 1f);
        }
    }
}
