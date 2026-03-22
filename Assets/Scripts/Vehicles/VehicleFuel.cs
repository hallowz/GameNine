using System;
using UnityEngine;

namespace Voidborne.Vehicles
{
    public class VehicleFuel : MonoBehaviour
    {
        [Header("Fuel")]
        [SerializeField] private float maxFuel = 100f;
        [SerializeField] private float baseDrainRate = 2f;   // per second at full throttle
        [SerializeField] private float leakDrainRate = 0.5f; // per second when leaking

        private float _currentFuel;
        private bool _isLeaking;
        private VehicleDamageSystem _damageSystem;

        public float CurrentFuel => _currentFuel;
        public float MaxFuel => maxFuel;
        public bool IsEmpty => _currentFuel <= 0f;
        public float FuelRatio => maxFuel > 0f ? _currentFuel / maxFuel : 0f;

        public event Action OnFuelEmpty;

        private void Awake()
        {
            _currentFuel = maxFuel;
            _damageSystem = GetComponent<VehicleDamageSystem>();
        }

        private void OnEnable()
        {
            if (_damageSystem != null)
                _damageSystem.OnZoneChanged += HandleZoneChanged;
        }

        private void OnDisable()
        {
            if (_damageSystem != null)
                _damageSystem.OnZoneChanged -= HandleZoneChanged;
        }

        private void HandleZoneChanged(DamageZone zone, DamageStage stage)
        {
            if (zone == DamageZone.FuelSystem)
                _isLeaking = stage >= DamageStage.Critical;
        }

        /// <summary>Called by VehicleBase each physics frame to drain fuel by throttle.</summary>
        public void Drain(float throttle, float dt)
        {
            if (_currentFuel <= 0f) return;
            float drain = throttle * baseDrainRate * dt;
            if (_isLeaking) drain += leakDrainRate * dt;
            _currentFuel = Mathf.Max(0f, _currentFuel - drain);
            if (_currentFuel <= 0f) OnFuelEmpty?.Invoke();
        }

        /// <summary>Passive leak drain independent of throttle.</summary>
        public void DrainLeak(float dt)
        {
            if (!_isLeaking || _currentFuel <= 0f) return;
            _currentFuel = Mathf.Max(0f, _currentFuel - leakDrainRate * dt);
            if (_currentFuel <= 0f) OnFuelEmpty?.Invoke();
        }

        public void Refuel(float amount)
        {
            _currentFuel = Mathf.Min(maxFuel, _currentFuel + amount);
        }

        public void ExtendTank(float extra)
        {
            maxFuel += extra;
            _currentFuel += extra;
        }
    }
}
