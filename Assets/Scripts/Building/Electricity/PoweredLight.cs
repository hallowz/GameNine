using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Draws 10W. When powered, enables the attached point light.
    /// </summary>
    public class PoweredLight : PowerNode
    {
        [Header("Powered Light")]
        public float drawWatts = 10f;

        [Header("Light")]
        public Light pointLight;
        public float lightIntensity = 120f;

        private void Awake()
        {
            powerDraw   = drawWatts;
            powerOutput = 0f;
            priority    = 10; // Lights are lowest priority.
        }

        private void Start()
        {
            PowerNetworkManager.Instance?.RegisterNode(this);
            if (pointLight == null)
                pointLight = GetComponentInChildren<Light>();
            SetLightState(false);
        }

        private void OnDestroy()
        {
            PowerNetworkManager.Instance?.UnregisterNode(this);
        }

        protected override void OnPowerChanged(bool powered)
        {
            SetLightState(powered);
        }

        private void SetLightState(bool on)
        {
            if (pointLight == null) return;
            pointLight.enabled   = on;
            pointLight.intensity = on ? lightIntensity : 0f;
        }
    }
}
