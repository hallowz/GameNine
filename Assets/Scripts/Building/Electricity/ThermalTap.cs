using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Passive geothermal generator. Must be placed below Y = -256.
    /// Outputs 500W permanently, no fuel. Silent. Expensive to craft.
    /// </summary>
    public class ThermalTap : PowerGenerator
    {
        [Header("Thermal Tap")]
        public float outputWatts    = 500f;
        public float minimumDepthY  = -256f;

        [Header("FX")]
        public ParticleSystem heatShimmer;
        public Light          glowLight;

        protected override void Awake()
        {
            base.Awake();
            powerOutput = outputWatts;
        }

        protected override void Start()
        {
            // Validate placement depth.
            if (transform.position.y > minimumDepthY)
            {
                Debug.LogWarning("[ThermalTap] Must be placed below Y = " + minimumDepthY +
                                 ". Current Y = " + transform.position.y + ". Not generating.");
                _isGenerating = false;
            }
            else
            {
                _isGenerating = true;
            }

            base.Start();
            UpdateFX();
        }

        private void UpdateFX()
        {
            if (heatShimmer != null)
            {
                if (_isGenerating && !heatShimmer.isPlaying) heatShimmer.Play();
                if (!_isGenerating && heatShimmer.isPlaying) heatShimmer.Stop();
            }
            if (glowLight != null)
                glowLight.enabled = _isGenerating;
        }

        public bool IsAtValidDepth => transform.position.y <= minimumDepthY;
    }
}
