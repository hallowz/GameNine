using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using Voidborne.Combat;

namespace Voidborne.Tests.PlayMode
{
    [TestFixture]
    public class WeaponSystemTests
    {
        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Creates a GunDefinition ScriptableObject with sensible test defaults.
        /// </summary>
        private static GunDefinition CreateTestGunDef(
            string gunName = "TestRifle",
            int magazineSize = 30,
            float fireRate = 600f,
            float damage = 20f,
            float reloadTime = 2f,
            FireMode fireMode = FireMode.Auto,
            Vector2[] recoilPattern = null)
        {
            var def = ScriptableObject.CreateInstance<GunDefinition>();
            def.gunName = gunName;
            def.magazineSize = magazineSize;
            def.fireRate = fireRate;
            def.damage = damage;
            def.reloadTime = reloadTime;
            def.fireMode = fireMode;
            def.range = 100f;
            def.penetration = 0f;
            def.spreadStanding = 1f;
            def.spreadMoving = 3f;
            def.spreadCrouching = 0.5f;
            def.spreadAirborne = 5f;
            def.adsZoomMultiplier = 0.5f;
            def.equipTime = 0.4f;

            if (recoilPattern != null)
            {
                def.recoilPattern = recoilPattern;
            }
            else
            {
                def.recoilPattern = new[]
                {
                    new Vector2(0.1f, 1.0f),
                    new Vector2(-0.1f, 0.8f),
                    new Vector2(0.2f, 1.2f),
                    new Vector2(-0.15f, 0.9f),
                };
            }

            return def;
        }

        // -------------------------------------------------------------------
        // 1. Gun starts with full magazine
        // -------------------------------------------------------------------

        [Test]
        public void GunInstance_StartsWithFullMagazine()
        {
            var def = CreateTestGunDef(magazineSize: 30);
            var gun = new GunInstance(def);

            Assert.AreEqual(30, gun.CurrentAmmo,
                "Gun should start with a full magazine");
            Assert.AreEqual(def.magazineSize, gun.CurrentAmmo,
                "CurrentAmmo should equal magazineSize from definition");

            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 2. Firing consumes ammo correctly
        // -------------------------------------------------------------------

        [Test]
        public void GunInstance_FiringConsumesAmmo()
        {
            var def = CreateTestGunDef(magazineSize: 10);
            var gun = new GunInstance(def);

            Assert.AreEqual(10, gun.CurrentAmmo);

            gun.ConsumeAmmo();
            Assert.AreEqual(9, gun.CurrentAmmo, "One shot should consume one round");

            gun.ConsumeAmmo();
            gun.ConsumeAmmo();
            Assert.AreEqual(7, gun.CurrentAmmo, "Three shots total should leave 7 rounds");

            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 3. Empty gun cannot fire
        // -------------------------------------------------------------------

        [Test]
        public void GunInstance_EmptyGunCannotFire()
        {
            var def = CreateTestGunDef(magazineSize: 2);
            var gun = new GunInstance(def);

            Assert.IsTrue(gun.CanFire(), "Gun with ammo should be able to fire");

            gun.ConsumeAmmo();
            gun.ConsumeAmmo();
            Assert.AreEqual(0, gun.CurrentAmmo, "Magazine should be empty");
            Assert.IsFalse(gun.CanFire(), "Empty gun should not be able to fire");

            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 4. Reload restores ammo
        // -------------------------------------------------------------------

        [Test]
        public void GunInstance_ReloadRestoresAmmo()
        {
            var def = CreateTestGunDef(magazineSize: 30);
            var gun = new GunInstance(def);

            // Spend some rounds.
            for (int i = 0; i < 20; i++)
                gun.ConsumeAmmo();

            Assert.AreEqual(10, gun.CurrentAmmo, "Should have 10 rounds left");

            int reserveBefore = gun.ReserveAmmo;

            // Simulate a reload: add enough to fill the magazine.
            int ammoNeeded = def.magazineSize - gun.CurrentAmmo;
            gun.SetReloading(true);
            Assert.IsTrue(gun.IsReloading, "Gun should be in reloading state");
            Assert.IsFalse(gun.CanFire(), "Cannot fire while reloading");

            gun.CompleteReload(ammoNeeded);

            Assert.IsFalse(gun.IsReloading, "Reloading flag should be cleared");
            Assert.AreEqual(30, gun.CurrentAmmo, "Magazine should be full after reload");
            Assert.AreEqual(reserveBefore - ammoNeeded, gun.ReserveAmmo,
                "Reserve ammo should decrease by the reloaded amount");
            Assert.IsTrue(gun.CanFire(), "Gun should be able to fire after reload");

            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 5. Recoil pattern cycles correctly
        // -------------------------------------------------------------------

        [Test]
        public void GunInstance_RecoilPatternCycles()
        {
            var pattern = new[]
            {
                new Vector2(0.1f, 1.0f),
                new Vector2(-0.2f, 0.5f),
                new Vector2(0.3f, 1.5f),
            };
            var def = CreateTestGunDef(recoilPattern: pattern);
            var gun = new GunInstance(def);

            Assert.AreEqual(0, gun.RecoilIndex, "Recoil index should start at 0");

            // First cycle through the pattern.
            Vector2 r0 = gun.GetNextRecoil();
            Assert.AreEqual(pattern[0], r0, "First recoil should be pattern[0]");

            Vector2 r1 = gun.GetNextRecoil();
            Assert.AreEqual(pattern[1], r1, "Second recoil should be pattern[1]");

            Vector2 r2 = gun.GetNextRecoil();
            Assert.AreEqual(pattern[2], r2, "Third recoil should be pattern[2]");

            // Pattern should wrap around (cycle).
            Vector2 r3 = gun.GetNextRecoil();
            Assert.AreEqual(pattern[0], r3,
                "Fourth recoil should wrap to pattern[0]");

            Assert.AreEqual(4, gun.RecoilIndex, "RecoilIndex should be 4 after 4 shots");

            // Test reset.
            gun.ResetRecoil();
            Assert.AreEqual(0, gun.RecoilIndex, "RecoilIndex should be 0 after reset");

            Vector2 afterReset = gun.GetNextRecoil();
            Assert.AreEqual(pattern[0], afterReset,
                "After reset, recoil should start from pattern[0]");

            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 6. Different fire modes (Auto, Semi, Burst)
        // -------------------------------------------------------------------

        [Test]
        public void GunDefinition_FireModes_AreDistinct()
        {
            var defAuto = CreateTestGunDef(fireMode: FireMode.Auto);
            var defSemi = CreateTestGunDef(fireMode: FireMode.Semi);
            var defBurst = CreateTestGunDef(fireMode: FireMode.Burst);

            Assert.AreEqual(FireMode.Auto, defAuto.fireMode);
            Assert.AreEqual(FireMode.Semi, defSemi.fireMode);
            Assert.AreEqual(FireMode.Burst, defBurst.fireMode);

            Assert.AreNotEqual(defAuto.fireMode, defSemi.fireMode,
                "Auto and Semi should be different fire modes");
            Assert.AreNotEqual(defSemi.fireMode, defBurst.fireMode,
                "Semi and Burst should be different fire modes");

            // Verify each fire mode creates a functional GunInstance.
            var gunAuto = new GunInstance(defAuto);
            var gunSemi = new GunInstance(defSemi);
            var gunBurst = new GunInstance(defBurst);

            Assert.IsTrue(gunAuto.CanFire(), "Auto gun should be ready to fire");
            Assert.IsTrue(gunSemi.CanFire(), "Semi gun should be ready to fire");
            Assert.IsTrue(gunBurst.CanFire(), "Burst gun should be ready to fire");

            Assert.AreEqual(defAuto.fireMode, gunAuto.Definition.fireMode);
            Assert.AreEqual(defSemi.fireMode, gunSemi.Definition.fireMode);
            Assert.AreEqual(defBurst.fireMode, gunBurst.Definition.fireMode);

            Object.Destroy(defAuto);
            Object.Destroy(defSemi);
            Object.Destroy(defBurst);
        }

        // -------------------------------------------------------------------
        // Bonus: WeaponSwitcher component can be added to a GameObject
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WeaponSwitcher_CanBeCreated()
        {
            var go = new GameObject("TestWeaponSwitcher");
            var switcher = go.AddComponent<WeaponSwitcher>();

            yield return null;

            Assert.IsNotNull(switcher, "WeaponSwitcher should be created successfully");
            Assert.AreEqual(0, switcher.CurrentSlot, "Default slot should be 0");
            Assert.IsFalse(switcher.HasActiveWeapon,
                "No weapon should be active without definitions assigned");

            Object.Destroy(go);
        }
    }
}
