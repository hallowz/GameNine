using UnityEngine;

namespace Voidborne.Combat
{
    public class GunInstance
    {
        public GunDefinition Definition { get; private set; }
        public int CurrentAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public int RecoilIndex { get; private set; }
        public float RecoilRecoveryTimer { get; private set; }
        public bool IsReloading { get; private set; }

        public GunInstance(GunDefinition definition)
        {
            Definition = definition;
            CurrentAmmo = definition.magazineSize;
            ReserveAmmo = definition.magazineSize * 3;
            RecoilIndex = 0;
            RecoilRecoveryTimer = 0f;
            IsReloading = false;
        }

        public void ResetRecoil()
        {
            RecoilIndex = 0;
            RecoilRecoveryTimer = 0f;
        }

        public Vector2 GetNextRecoil()
        {
            if (Definition.recoilPattern == null || Definition.recoilPattern.Length == 0)
                return Vector2.zero;

            Vector2 recoil = Definition.recoilPattern[RecoilIndex % Definition.recoilPattern.Length];
            RecoilIndex++;
            return recoil;
        }

        public bool CanFire()
        {
            return !IsReloading && CurrentAmmo > 0;
        }

        public void ConsumeAmmo()
        {
            CurrentAmmo--;
        }

        /// <summary>Sets the reloading flag directly.</summary>
        public void SetReloading(bool value)
        {
            IsReloading = value;
        }

        /// <summary>
        /// Adds ammo from reserve into the magazine and clears the reloading flag.
        /// Clamps CurrentAmmo to [0, magazineSize].
        /// </summary>
        public void CompleteReload(int ammoToAdd)
        {
            CurrentAmmo = Mathf.Clamp(CurrentAmmo + ammoToAdd, 0, Definition.magazineSize);
            ReserveAmmo = Mathf.Max(0, ReserveAmmo - ammoToAdd);
            IsReloading = false;
        }

        /// <summary>Directly sets CurrentAmmo (e.g. for editor / cheat tools).</summary>
        public void SetCurrentAmmo(int value)
        {
            CurrentAmmo = Mathf.Clamp(value, 0, Definition.magazineSize);
        }
    }
}
