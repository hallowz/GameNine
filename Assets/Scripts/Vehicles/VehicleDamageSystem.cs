using System;
using UnityEngine;

namespace Voidborne.Vehicles
{
    public enum DamageZone { EngineBay, LeftDrivetrain, RightDrivetrain, Chassis, FrontGlazing, FuelSystem }
    public enum DamageStage { Healthy, Damaged, Critical, Destroyed }

    [Serializable]
    public class ZoneHealth
    {
        public DamageZone zone;
        public DamageStage stage = DamageStage.Healthy;
        public float hitPoints = 100f;
        public float maxHitPoints = 100f;

        public bool IsDestroyed => stage == DamageStage.Destroyed;

        public void ApplyDamage(float amount)
        {
            if (stage == DamageStage.Destroyed) return;
            hitPoints -= amount;
            if (hitPoints <= 0f)
            {
                if (stage < DamageStage.Destroyed)
                    stage++;
                hitPoints = (stage == DamageStage.Destroyed) ? 0f : maxHitPoints * 0.6f;
            }
        }

        /// <summary>Improve by one stage (cannot repair Destroyed).</summary>
        public bool Repair()
        {
            if (stage == DamageStage.Healthy || stage == DamageStage.Destroyed) return false;
            stage--;
            hitPoints = maxHitPoints;
            return true;
        }

        /// <summary>Full workshop repair — restores all stages.</summary>
        public void FullRepair()
        {
            stage = DamageStage.Healthy;
            hitPoints = maxHitPoints;
        }
    }

    public class VehicleDamageSystem : MonoBehaviour
    {
        private readonly ZoneHealth[] _zones = new ZoneHealth[6];

        [Header("Visual References")]
        [SerializeField] private GameObject frontGlazingMesh;
        [SerializeField] private ParticleSystem fuelLeakParticles;

        private VehicleBody _body;

        public event Action<DamageZone, DamageStage> OnZoneChanged;

        private void Awake()
        {
            _body = GetComponent<VehicleBody>();
            for (int i = 0; i < 6; i++)
            {
                _zones[i] = new ZoneHealth
                {
                    zone = (DamageZone)i,
                    maxHitPoints = 100f,
                    hitPoints = 100f,
                    stage = DamageStage.Healthy
                };
            }
        }

        public ZoneHealth GetZone(DamageZone zone) => _zones[(int)zone];
        public ZoneHealth[] AllZones => _zones;

        /// <summary>Routes damage to the correct zone based on hit direction relative to forward.</summary>
        public void ReceiveDirectionalDamage(float amount, Vector3 hitNormal, bool isScrape = false)
        {
            DamageZone target;
            if (isScrape)
            {
                target = DamageZone.FuelSystem;
            }
            else
            {
                float frontDot = Vector3.Dot(transform.forward, -hitNormal);
                float rightDot = Vector3.Dot(transform.right, -hitNormal);

                if (frontDot > 0.5f)
                    target = DamageZone.EngineBay;
                else if (rightDot > 0.3f)
                    target = DamageZone.RightDrivetrain;
                else if (rightDot < -0.3f)
                    target = DamageZone.LeftDrivetrain;
                else
                    target = DamageZone.Chassis;
            }
            ApplyDamageToZone(target, amount);
        }

        public void ApplyDamageToZone(DamageZone zone, float amount)
        {
            var z = _zones[(int)zone];
            var prevStage = z.stage;
            z.ApplyDamage(amount);

            if (z.stage != prevStage)
            {
                OnZoneChanged?.Invoke(zone, z.stage);
                ApplyZoneEffect(zone, z.stage);
            }
        }

        /// <summary>Route damage to a specific body part slot by ID.</summary>
        public void ReceivePartDamage(string slotId, float amount)
        {
            if (_body != null)
                _body.ReceivePartDamage(slotId, amount);
        }

        private static BodySection ZoneToSection(DamageZone zone)
        {
            switch (zone)
            {
                case DamageZone.EngineBay:       return BodySection.Front;
                case DamageZone.Chassis:         return BodySection.Middle;
                case DamageZone.FuelSystem:       return BodySection.Back;
                case DamageZone.LeftDrivetrain:
                case DamageZone.RightDrivetrain:  return BodySection.Undercarriage;
                case DamageZone.FrontGlazing:     return BodySection.Front;
                default:                          return BodySection.Middle;
            }
        }

        private void ApplyZoneEffect(DamageZone zone, DamageStage stage)
        {
            if (zone == DamageZone.FrontGlazing && stage == DamageStage.Destroyed)
            {
                if (frontGlazingMesh != null) frontGlazingMesh.SetActive(false);
            }
            if (zone == DamageZone.FuelSystem && stage >= DamageStage.Critical)
            {
                if (fuelLeakParticles != null && !fuelLeakParticles.isPlaying)
                    fuelLeakParticles.Play();
            }

            // Propagate damage to body parts in the matching section
            if (_body != null)
            {
                var section = ZoneToSection(zone);
                float partDamage = 15f; // fixed amount per stage transition
                _body.ReceiveSectionDamage(section, partDamage);
            }
        }

        public bool RepairZone(DamageZone zone)
        {
            var z = _zones[(int)zone];
            bool repaired = z.Repair();
            if (repaired)
            {
                if (zone == DamageZone.FuelSystem && z.stage < DamageStage.Critical)
                    if (fuelLeakParticles != null) fuelLeakParticles.Stop();
                OnZoneChanged?.Invoke(zone, z.stage);
            }
            return repaired;
        }

        public void FullRepair()
        {
            for (int i = 0; i < 6; i++)
                _zones[i].FullRepair();

            if (frontGlazingMesh != null) frontGlazingMesh.SetActive(true);
            if (fuelLeakParticles != null) fuelLeakParticles.Stop();

            for (int i = 0; i < 6; i++)
                OnZoneChanged?.Invoke(_zones[i].zone, _zones[i].stage);
        }
    }
}
