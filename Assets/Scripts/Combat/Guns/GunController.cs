using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Combat
{
    /// <summary>
    /// Main shooting system. Attach to the Player GameObject alongside a CharacterController.
    /// Handles fire input, spread, recoil, hitscan raycasts, muzzle flash, and audio.
    /// Requires the Unity Input System package.
    /// </summary>
    public class GunController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector references
        // -----------------------------------------------------------------------

        [Tooltip("Component that applies and recovers camera recoil.")]
        [SerializeField] private RecoilSystem recoilSystem;

        [Tooltip("Camera transform used as raycast origin and for spread calculation.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Layers that can be hit by bullets.")]
        [SerializeField] private LayerMask shootableLayers;

        [Tooltip("AudioSource used to play fire and reload sounds.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional gun to equip automatically on Start (useful for testing).")]
        [SerializeField] private GunDefinition defaultGun;

        [Tooltip("Seconds after the last shot before recoil recovery begins.")]
        [SerializeField] private float recoveryDelay = 0.15f;

        [Tooltip("ReloadSystem that manages magazine refills.")]
        [SerializeField] private ReloadSystem reloadSystem;

        [Tooltip("ADSController that manages aim-down-sights FOV and spread.")]
        [SerializeField] private ADSController adsController;

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        /// <summary>The currently equipped gun, or null if none.</summary>
        public GunInstance CurrentGun { get; private set; }

        /// <summary>
        /// The gun's effective spread angle (degrees) for the current frame,
        /// updated every Update() before firing. Read by CrosshairUI.
        /// </summary>
        public float CurrentSpread { get; private set; }

        private bool _isFiring;
        private float _lastShotTime = -999f;
        private int _burstShotsRemaining;

        // Cached component references resolved once in Start.
        private CharacterController _characterController;
        private IPlayerState _playerState;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Start()
        {
            // Try to find the CharacterController on this GameObject or any parent.
            _characterController = GetComponentInParent<CharacterController>();
            _playerState = GetComponentInParent<IPlayerState>();

            if (defaultGun != null)
                EquipGun(defaultGun);
        }

        private void Update()
        {
            if (CurrentGun == null)
                return;

            // Block shooting when any UI is open.
            if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
                return;

            GunDefinition gun = CurrentGun.Definition;

            // R key — trigger reload
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                reloadSystem?.StartReload(CurrentGun);

            // --- Update CurrentSpread every frame so CrosshairUI can read it ---
            {
                bool isGrounded = true;
                bool isCrouching = false;
                float speed = 0f;

                if (_characterController != null)
                {
                    isGrounded = _characterController.isGrounded;
                    Vector3 hv = _characterController.velocity;
                    hv.y = 0f;
                    speed = hv.magnitude;
                }

                if (_playerState != null)
                    isCrouching = _playerState.IsCrouching;

                float spreadMultiplier = adsController?.GetSpreadMultiplier() ?? 1f;
                CurrentSpread = SpreadCalculator.Calculate(gun, isGrounded, isCrouching, speed)
                                * spreadMultiplier;
            }

            // Allow recoil recovery after the delay since the last shot.
            // RecoilSystem recovers in its own Update; we just need to gate it.
            // (RecoilSystem.Update always runs — recovery is implicit once recoilOffset > 0.)

            float fireInterval = 60f / gun.fireRate;

            switch (gun.fireMode)
            {
                case FireMode.Auto:
                    _isFiring = Mouse.current != null && Mouse.current.leftButton.isPressed;
                    if (_isFiring && Time.time >= _lastShotTime + fireInterval)
                        TryFire();
                    break;

                case FireMode.Semi:
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        TryFire();
                    break;

                case FireMode.Burst:
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        _burstShotsRemaining = 3;

                    if (_burstShotsRemaining > 0 && Time.time >= _lastShotTime + fireInterval)
                    {
                        TryFire();
                        _burstShotsRemaining--;
                    }
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Equips the given gun definition, replacing any currently equipped gun.
        /// </summary>
        public void EquipGun(GunDefinition definition)
        {
            CurrentGun = new GunInstance(definition);
            _burstShotsRemaining = 0;
            _lastShotTime = -999f;

            if (recoilSystem != null)
                recoilSystem.ResetInstant();
        }

        /// <summary>
        /// Unequips the current gun (e.g. when a non-weapon hotbar item is selected).
        /// </summary>
        public void UnequipGun()
        {
            CurrentGun = null;
            _burstShotsRemaining = 0;

            if (recoilSystem != null)
                recoilSystem.ResetInstant();
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void TryFire()
        {
            if (!CurrentGun.CanFire())
                return;

            CurrentGun.ConsumeAmmo();

            GunDefinition gun = CurrentGun.Definition;

            // --- Recoil ---
            Vector2 recoilStep = CurrentGun.GetNextRecoil();
            if (recoilSystem != null)
                recoilSystem.ApplyRecoil(recoilStep);

            // --- Hitscan raycast ---
            // CurrentSpread is already calculated for this frame in Update().
            // For shotguns (pelletCount > 1) each pellet gets its own spread-offset ray.
            // Ammo, recoil, and muzzle flash are consumed once per shot regardless of pellet count.
            if (gun.bulletType == BulletType.Hitscan && cameraTransform != null)
            {
                int pellets = gun.pelletCount > 1 ? gun.pelletCount : 1;
                for (int i = 0; i < pellets; i++)
                {
                    Vector3 direction = ApplySpread(cameraTransform.forward, CurrentSpread);
                    if (Physics.Raycast(cameraTransform.position, direction, out RaycastHit hit,
                                        gun.range, shootableLayers, QueryTriggerInteraction.Ignore))
                    {
                        HitResult result = HitDetection.ProcessHit(hit, gun.damage, gameObject);

                        if (result.HitEnemy)
                        {
                            Voidborne.UI.HitmarkerUI.Instance?.ShowHit(result.WasKill);
                        }
                    }
                }
            }

            // --- Muzzle flash ---
            if (gun.muzzleFlashPrefab != null && cameraTransform != null)
            {
                GameObject flash = Instantiate(gun.muzzleFlashPrefab,
                                               cameraTransform.position,
                                               cameraTransform.rotation);
                // Auto-destroy after a short delay so it does not linger.
                Destroy(flash, 0.05f);
            }

            // --- Fire sound ---
            // Use the gun's assigned clip, or fall back to a procedural shot if none is set.
            if (audioSource != null)
            {
                AudioClip shotClip = RuntimeGunSounds.GetShot(gun);
                if (shotClip != null)
                    audioSource.PlayOneShot(shotClip);
            }

            _lastShotTime = Time.time;
        }

        /// <summary>
        /// Offsets <paramref name="forward"/> by a random vector within a cone of
        /// half-angle <paramref name="spreadDegrees"/> degrees.
        /// </summary>
        private Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
        {
            if (spreadDegrees <= 0f)
                return forward;

            float radius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            Vector2 randomCircle = Random.insideUnitCircle * radius;

            if (cameraTransform == null)
                return forward;

            Vector3 offset = cameraTransform.right   * randomCircle.x
                           + cameraTransform.up      * randomCircle.y;

            return (forward + offset).normalized;
        }
    }

    // -----------------------------------------------------------------------
    // Minimal interface for reading player state without a hard dependency.
    // FirstPersonController (or any player script) can implement this.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Optional interface that player controller scripts can implement so that
    /// GunController can read crouching state without a hard type dependency.
    /// </summary>
    public interface IPlayerState
    {
        bool IsCrouching { get; }
    }
}
