using System.Collections;
using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Manages magazine reloading for any GunInstance.
    /// Attach to the Player GameObject (alongside GunController).
    /// </summary>
    public class ReloadSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector references
        // -----------------------------------------------------------------------

        [Tooltip("AudioSource used to play the reload sound clip.")]
        [SerializeField] private AudioSource audioSource;

        // -----------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------

        private Coroutine _reloadCoroutine;

        /// <summary>
        /// Normalised reload progress: 0 = just started, 1 = complete.
        /// Zero when not reloading. Useful for driving a reload progress bar.
        /// </summary>
        public float ReloadProgress { get; private set; }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Begins reloading <paramref name="gun"/>.
        /// Does nothing if the gun is already reloading or the magazine is full.
        /// </summary>
        public void StartReload(GunInstance gun)
        {
            if (gun == null)
                return;

            if (gun.IsReloading)
                return;

            if (gun.CurrentAmmo == gun.Definition.magazineSize)
                return;

            if (gun.ReserveAmmo <= 0)
                return;

            gun.SetReloading(true);
            _reloadCoroutine = StartCoroutine(ReloadCoroutine(gun));
        }

        /// <summary>
        /// Interrupts an in-progress reload and clears the reloading flag.
        /// </summary>
        public void InterruptReload(GunInstance gun)
        {
            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _reloadCoroutine = null;
            }

            if (gun != null)
                gun.SetReloading(false);

        ReloadProgress = 0f;
        }

        /// <summary>Returns whether the given gun is currently reloading.</summary>
        public bool IsReloading(GunInstance gun)
        {
            return gun != null && gun.IsReloading;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private IEnumerator ReloadCoroutine(GunInstance gun)
        {
            float elapsed = 0f;
            float total   = gun.Definition.reloadTime;

            // Track progress frame-by-frame so AmmoUI can display a live bar.
            while (elapsed < total)
            {
                elapsed       += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01(elapsed / total);
                yield return null;
            }

            ReloadProgress = 0f;

            int needed    = gun.Definition.magazineSize - gun.CurrentAmmo;
            int ammoToAdd = Mathf.Min(needed, gun.ReserveAmmo);
            gun.CompleteReload(ammoToAdd);

            // Play reload-complete sound.
            if (audioSource != null && gun.Definition.reloadSound != null)
                audioSource.PlayOneShot(gun.Definition.reloadSound);

            _reloadCoroutine = null;
        }
    }
}
