using System.Collections;
using UnityEngine;
using Voidborne.Combat;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Consumes wood/coal/fuel to produce 150W. Loud — generates a noise radius.
    /// In enclosed underground spaces generates pollution buildup.
    /// </summary>
    public class BurnGenerator : PowerGenerator
    {
        [Header("Burn Generator")]
        public float outputWatts          = 150f;
        public float noiseRadius          = 15f;
        public float fuelBurnTimeSeconds  = 30f; // seconds per fuel item

        [Header("Pollution")]
        public float pollutionBuildupRate  = 0.1f;  // pollution/s when enclosed
        public float pollutionDamageRate   = 5f;    // hp/s to player when maxed
        public float pollutionClearRadius  = 3f;    // ventilation check distance

        [Header("Fuel")]
        public ItemDefinition[] acceptedFuels; // Assign Wood, Coal, etc. in inspector
        public int fuelSlots = 4;

        [Header("Noise")]
        [Tooltip("Optional: enemy noise events via this radius.")]
        public bool emitNoise = true;

        // Runtime state
        private float _fuelTimeRemaining;
        private float _pollutionLevel; // 0–1
        private bool  _isEnclosed;

        [Header("FX")]
        public ParticleSystem smokeParticles;
        public ParticleSystem pollutionParticles;
        public AudioSource    engineSound;

        protected override void Awake()
        {
            base.Awake();
            powerOutput = outputWatts;
        }

        protected override void Start()
        {
            base.Start();
            StartCoroutine(FuelLoop());
            StartCoroutine(PollutionLoop());
        }

        private IEnumerator FuelLoop()
        {
            while (true)
            {
                if (_fuelTimeRemaining > 0f)
                {
                    _isGenerating = true;
                    _fuelTimeRemaining -= Time.deltaTime;
                    if (_fuelTimeRemaining < 0f) _fuelTimeRemaining = 0f;
                }
                else
                {
                    _isGenerating = false;
                }

                UpdateFX();
                yield return null;
            }
        }

        private IEnumerator PollutionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (!_isGenerating) { _pollutionLevel = Mathf.Max(0f, _pollutionLevel - 0.05f); continue; }

                _isEnclosed = CheckEnclosed();

                if (_isEnclosed)
                {
                    _pollutionLevel = Mathf.Clamp01(_pollutionLevel + pollutionBuildupRate);
                    if (pollutionParticles != null && _pollutionLevel > 0.3f && !pollutionParticles.isPlaying)
                        pollutionParticles.Play();
                }
                else
                {
                    _pollutionLevel = Mathf.Max(0f, _pollutionLevel - 0.05f);
                    if (pollutionParticles != null && _pollutionLevel <= 0.1f && pollutionParticles.isPlaying)
                        pollutionParticles.Stop();
                }

                // Damage nearby players when pollution is high.
                if (_pollutionLevel > 0.8f)
                    ApplyPollutionDamage();
            }
        }

        private bool CheckEnclosed()
        {
            // Cast rays in 6 directions; if all hit within pollutionClearRadius, enclosed.
            Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int hits = 0;
            foreach (var d in dirs)
                if (Physics.Raycast(transform.position, d, pollutionClearRadius))
                    hits++;
            return hits >= 5; // at least 5/6 directions blocked
        }

        private void ApplyPollutionDamage()
        {
            // Find IDamageable components (player, enemies) in radius.
            var cols = Physics.OverlapSphere(transform.position, pollutionClearRadius * 2f);
            foreach (var c in cols)
            {
                var damageable = c.GetComponent<Combat.IDamageable>();
                if (damageable != null)
                {
                    var info = new Combat.DamageInfo
                    {
                        Amount = pollutionDamageRate,
                        Type   = Combat.DamageType.Fire
                    };
                    damageable.TakeDamage(info);
                }
            }
        }

        private void UpdateFX()
        {
            if (smokeParticles != null)
            {
                if (_isGenerating && !smokeParticles.isPlaying) smokeParticles.Play();
                if (!_isGenerating && smokeParticles.isPlaying)  smokeParticles.Stop();
            }
            if (engineSound != null)
            {
                if (_isGenerating && !engineSound.isPlaying) engineSound.Play();
                if (!_isGenerating && engineSound.isPlaying) engineSound.Stop();
            }
        }

        /// <summary>Add fuel time. Called from UI/interaction when player inserts a fuel item.</summary>
        public void AddFuel(ItemDefinition fuel, int count = 1)
        {
            _fuelTimeRemaining += fuelBurnTimeSeconds * count;
        }

        // Inspector display.
        public float FuelTimeRemaining  => _fuelTimeRemaining;
        public float PollutionLevel     => _pollutionLevel;
        public bool  IsEnclosed         => _isEnclosed;
    }
}
