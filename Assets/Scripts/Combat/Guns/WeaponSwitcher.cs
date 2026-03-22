using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Combat.Melee;
using Voidborne.Combat.Projectiles;

namespace Voidborne.Combat
{
    /// <summary>
    /// Manages switching between weapon slots.
    ///
    /// Two modes:
    ///   useInventoryHotbar = true  — Digit keys / scroll update PlayerInventory.SelectedHotbarIndex;
    ///                                Update() reads ActiveHotbarItem each frame and calls
    ///                                EquipGunDirect() when the active WeaponItem changes.
    ///   useInventoryHotbar = false — Legacy behaviour: digit keys / scroll cycle through the
    ///                                inspector-assigned weaponSlots[] array via SwitchToSlot().
    ///
    /// Interrupts any in-progress reload before swapping guns.
    /// </summary>
    public class WeaponSwitcher : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------

        [Tooltip("The GunController that fires and tracks the currently active gun.")]
        [SerializeField] private GunController gunController;

        [Tooltip("The ADSController — receives SetGun() whenever the slot changes.")]
        [SerializeField] private ADSController adsController;

        [Tooltip("The ReloadSystem — reload is interrupted before every weapon swap.")]
        [SerializeField] private ReloadSystem reloadSystem;

        [Tooltip("Up to three weapon definitions (index 0 = slot 1, 1 = slot 2, 2 = slot 3 / melee). " +
                 "Only used when useInventoryHotbar = false.")]
        [SerializeField] private GunDefinition[] weaponSlots = new GunDefinition[3];

        [Tooltip("Optional AudioSource for weapon-switch sounds (not required).")]
        [SerializeField] private AudioSource audioSource;

        [Header("Inventory Integration")]
        [Tooltip("Reference to the PlayerInventory. Assign in the Inspector.")]
        [SerializeField] private PlayerInventory playerInventory;

        [Tooltip("Optional MeleeController. When assigned, melee WeaponItems equip it automatically.")]
        [SerializeField] private MeleeController meleeController;

        [Tooltip("Optional BowController. When assigned, bow WeaponItems equip it automatically.")]
        [SerializeField] private BowController bowController;

        [Tooltip("Optional ThrowController. When assigned, throwable WeaponItems equip it automatically.")]
        [SerializeField] private ThrowController throwController;

        [Tooltip("Optional TorchView. When assigned, torch items show a held torch with fire light.")]
        [SerializeField] private TorchView torchView;

        [Tooltip("Optional FlareGunView. When assigned, flare gun items show a held flare gun.")]
        [SerializeField] private FlareGunView flareGunView;

        [Tooltip("When true, the hotbar drives weapon equipping. " +
                 "Digit keys and scroll wheel change SelectedHotbarIndex; " +
                 "the Update loop reads ActiveHotbarItem to determine which gun to equip.")]
        [SerializeField] private bool useInventoryHotbar = true;

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        private int _currentSlot = 0;
        private float _lastSwitchTime = -999f;
        private bool _isSwitching = false;

        // Tracks the last definitions we equipped via the inventory path to
        // avoid calling Equip every single frame.
        private GunDefinition        _lastEquippedDef;
        private MeleeDefinition      _lastEquippedMeleeDef;
        private BowDefinition        _lastEquippedBowDef;
        private ThrowableDefinition  _lastEquippedThrowableDef;

        /// <summary>Zero-based index of the currently active weapon slot (legacy mode).</summary>
        public int CurrentSlot => _currentSlot;

        /// <summary>True when any weapon or held item (gun, melee, bow, throwable, torch, or flare gun) is currently equipped.</summary>
        public bool HasActiveWeapon =>
            _lastEquippedDef != null ||
            _lastEquippedMeleeDef != null ||
            _lastEquippedBowDef != null ||
            _lastEquippedThrowableDef != null ||
            (torchView != null && torchView.IsEquipped) ||
            (flareGunView != null && flareGunView.IsEquipped);

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Start()
        {
            // Auto-resolve controllers from the same GameObject if not assigned.
            if (meleeController == null)
                meleeController = GetComponent<MeleeController>();
            if (bowController == null)
                bowController = GetComponent<BowController>();
            if (throwController == null)
                throwController = GetComponent<ThrowController>();
            if (torchView == null)
                torchView = GetComponentInChildren<TorchView>();
            if (flareGunView == null)
                flareGunView = GetComponentInChildren<FlareGunView>();

            if (useInventoryHotbar)
            {
                // Inventory mode: do an initial equip from the hotbar if possible.
                TryEquipFromHotbar();
            }
            else
            {
                // Legacy mode: equip the first valid weapon on startup.
                if (weaponSlots != null && weaponSlots.Length > 0 && weaponSlots[0] != null)
                    SwitchToSlot(0);
            }
        }

        private void Update()
        {
            if (useInventoryHotbar && playerInventory != null)
            {
                HandleInventoryInput();
                TryEquipFromHotbar();
            }
            else
            {
                HandleLegacyInput();
            }
        }

        // -----------------------------------------------------------------------
        // Inventory-driven input handling
        // -----------------------------------------------------------------------

        /// <summary>
        /// Reads digit keys and scroll wheel to update PlayerInventory.SelectedHotbarIndex.
        /// The actual equip is driven by TryEquipFromHotbar().
        /// </summary>
        private void HandleInventoryInput()
        {
            int requested = playerInventory.SelectedHotbarIndex;

            if (Keyboard.current != null)
            {
                if      (Keyboard.current.digit1Key.wasPressedThisFrame) requested = 0;
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) requested = 1;
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) requested = 2;
                else if (Keyboard.current.digit4Key.wasPressedThisFrame) requested = 3;
                else if (Keyboard.current.digit5Key.wasPressedThisFrame) requested = 4;
                else if (Keyboard.current.digit6Key.wasPressedThisFrame) requested = 5;
                else if (Keyboard.current.digit7Key.wasPressedThisFrame) requested = 6;
                else if (Keyboard.current.digit8Key.wasPressedThisFrame) requested = 7;
                else if (Keyboard.current.digit9Key.wasPressedThisFrame) requested = 8;
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll < -0.01f)
                    requested = (playerInventory.SelectedHotbarIndex + 1) % 9;
                else if (scroll > 0.01f)
                    requested = (playerInventory.SelectedHotbarIndex - 1 + 9) % 9;
            }

            playerInventory.SelectedHotbarIndex = requested;
        }

        /// <summary>
        /// Reads ActiveHotbarItem from PlayerInventory; equips the corresponding gun
        /// or melee weapon if it has changed since the last frame.
        /// </summary>
        private void TryEquipFromHotbar()
        {
            if (playerInventory == null) return;

            ItemStack active = playerInventory.ActiveHotbarItem;

            // Torch — check by itemId before weapon routing
            if (!active.IsEmpty && active.item != null && active.item.itemId == "torch")
            {
                UnequipAllWeapons();
                UnequipFlareGun();
                if (torchView != null && !torchView.IsEquipped)
                    torchView.Equip();
                return;
            }

            // Flare gun — check by itemId before weapon routing
            if (!active.IsEmpty && active.item != null && active.item.itemId == "flare_gun")
            {
                UnequipAllWeapons();
                UnequipTorch();
                if (flareGunView != null && !flareGunView.IsEquipped)
                    flareGunView.Equip();
                return;
            }

            // Unequip torch / flare gun when switching away
            UnequipTorch();
            UnequipFlareGun();

            if (!active.IsEmpty && active.item is WeaponItem wi)
            {
                if (wi.gunDefinition != null)
                {
                    UnequipNonGun();
                    if (wi.gunDefinition != _lastEquippedDef)
                        EquipGunDirect(wi.gunDefinition);
                }
                else if (wi.meleeDefinition != null)
                {
                    UnequipNonMelee();
                    if (wi.meleeDefinition != _lastEquippedMeleeDef)
                        EquipMeleeDirect(wi.meleeDefinition);
                }
                else if (wi.bowDefinition != null)
                {
                    UnequipNonBow();
                    if (wi.bowDefinition != _lastEquippedBowDef)
                        EquipBowDirect(wi.bowDefinition);
                }
                else if (wi.throwableDefinition != null)
                {
                    UnequipNonThrowable();
                    if (wi.throwableDefinition != _lastEquippedThrowableDef)
                        EquipThrowableDirect(wi.throwableDefinition, wi);
                }
            }
            else
            {
                UnequipAll();
            }
        }

        private void UnequipAll()
        {
            UnequipAllWeapons();
            UnequipTorch();
            UnequipFlareGun();
        }

        private void UnequipTorch()
        {
            if (torchView != null && torchView.IsEquipped)
                torchView.Unequip();
        }

        private void UnequipFlareGun()
        {
            if (flareGunView != null && flareGunView.IsEquipped)
                flareGunView.Unequip();
        }

        private void UnequipAllWeapons()
        {
            if (_lastEquippedDef != null)
            {
                reloadSystem?.InterruptReload(gunController?.CurrentGun);
                gunController?.UnequipGun();
                adsController?.SetGun(null);
                _lastEquippedDef = null;
            }
            if (_lastEquippedMeleeDef != null)
            {
                meleeController?.Unequip();
                _lastEquippedMeleeDef = null;
            }
            if (_lastEquippedBowDef != null)
            {
                bowController?.Unequip();
                _lastEquippedBowDef = null;
            }
            if (_lastEquippedThrowableDef != null)
            {
                throwController?.Unequip();
                _lastEquippedThrowableDef = null;
            }
        }

        private void UnequipNonGun()
        {
            if (_lastEquippedMeleeDef != null) { meleeController?.Unequip(); _lastEquippedMeleeDef = null; }
            if (_lastEquippedBowDef != null)   { bowController?.Unequip();   _lastEquippedBowDef   = null; }
            if (_lastEquippedThrowableDef != null) { throwController?.Unequip(); _lastEquippedThrowableDef = null; }
        }

        private void UnequipNonMelee()
        {
            if (_lastEquippedDef != null) { reloadSystem?.InterruptReload(gunController?.CurrentGun); gunController?.UnequipGun(); adsController?.SetGun(null); _lastEquippedDef = null; }
            if (_lastEquippedBowDef != null)   { bowController?.Unequip();   _lastEquippedBowDef   = null; }
            if (_lastEquippedThrowableDef != null) { throwController?.Unequip(); _lastEquippedThrowableDef = null; }
        }

        private void UnequipNonBow()
        {
            if (_lastEquippedDef != null) { reloadSystem?.InterruptReload(gunController?.CurrentGun); gunController?.UnequipGun(); adsController?.SetGun(null); _lastEquippedDef = null; }
            if (_lastEquippedMeleeDef != null) { meleeController?.Unequip(); _lastEquippedMeleeDef = null; }
            if (_lastEquippedThrowableDef != null) { throwController?.Unequip(); _lastEquippedThrowableDef = null; }
        }

        private void UnequipNonThrowable()
        {
            if (_lastEquippedDef != null) { reloadSystem?.InterruptReload(gunController?.CurrentGun); gunController?.UnequipGun(); adsController?.SetGun(null); _lastEquippedDef = null; }
            if (_lastEquippedMeleeDef != null) { meleeController?.Unequip(); _lastEquippedMeleeDef = null; }
            if (_lastEquippedBowDef != null) { bowController?.Unequip(); _lastEquippedBowDef = null; }
        }

        // -----------------------------------------------------------------------
        // Legacy input handling (useInventoryHotbar = false)
        // -----------------------------------------------------------------------

        private void HandleLegacyInput()
        {
            if (weaponSlots == null || weaponSlots.Length == 0) return;

            int requestedSlot = _currentSlot;

            if (Keyboard.current != null)
            {
                if      (Keyboard.current.digit1Key.wasPressedThisFrame) requestedSlot = 0;
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) requestedSlot = 1;
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) requestedSlot = 2;
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll < -0.01f)
                    requestedSlot = (_currentSlot + 1) % weaponSlots.Length;
                else if (scroll > 0.01f)
                    requestedSlot = (_currentSlot - 1 + weaponSlots.Length) % weaponSlots.Length;
            }

            if (requestedSlot != _currentSlot)
                SwitchToSlot(requestedSlot);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Switches to the weapon at <paramref name="slot"/> in the legacy weaponSlots array.
        /// Silently does nothing if the slot index is out of range or the slot is empty.
        /// </summary>
        public void SwitchToSlot(int slot)
        {
            if (weaponSlots == null || slot < 0 || slot >= weaponSlots.Length)
                return;

            if (weaponSlots[slot] == null)
                return;

            // Interrupt any reload that is currently in progress on the outgoing gun.
            if (reloadSystem != null && gunController != null)
                reloadSystem.InterruptReload(gunController.CurrentGun);

            // Equip the new gun.
            if (gunController != null)
                gunController.EquipGun(weaponSlots[slot]);

            // Notify ADS of the new gun's zoom multiplier.
            if (adsController != null)
                adsController.SetGun(weaponSlots[slot]);

            _currentSlot = slot;
            _lastSwitchTime = Time.time;
            _isSwitching = false;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Equips a gun directly by GunDefinition reference (inventory-driven path).
        /// Interrupts any in-progress reload, equips the gun, and notifies ADS.
        /// </summary>
        private void EquipGunDirect(GunDefinition def)
        {
            if (reloadSystem != null && gunController != null)
                reloadSystem.InterruptReload(gunController.CurrentGun);

            if (gunController != null)
                gunController.EquipGun(def);

            if (adsController != null)
                adsController.SetGun(def);

            _lastEquippedDef = def;
            _lastSwitchTime = Time.time;
            _isSwitching = false;
        }

        /// <summary>
        /// Equips a melee weapon directly by MeleeDefinition reference (inventory-driven path).
        /// </summary>
        private void EquipMeleeDirect(MeleeDefinition def)
        {
            meleeController?.Equip(def);
            _lastEquippedMeleeDef = def;
            _lastSwitchTime = Time.time;
            _isSwitching = false;
        }

        /// <summary>
        /// Equips a bow directly by BowDefinition reference (inventory-driven path).
        /// </summary>
        private void EquipBowDirect(BowDefinition def)
        {
            bowController?.Equip(def);
            _lastEquippedBowDef = def;
            _lastSwitchTime = Time.time;
            _isSwitching = false;
        }

        /// <summary>
        /// Equips a throwable weapon (inventory-driven path).
        /// Passes both definition and WeaponItem so ThrowController can respawn the item.
        /// </summary>
        private void EquipThrowableDirect(ThrowableDefinition def, WeaponItem item)
        {
            throwController?.Equip(def, item);
            _lastEquippedThrowableDef = def;
            _lastSwitchTime = Time.time;
            _isSwitching = false;
        }
    }
}
