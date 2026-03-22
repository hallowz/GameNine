using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Relay point for long cable runs. Reduces transmission loss on its segment.
    /// Displays real-time generation/consumption when inspected.
    /// </summary>
    public class JunctionBox : PowerNode
    {
        [Header("Junction Box")]
        [Tooltip("Segment loss reduction factor (0.5 = half the normal loss through this box).")]
        public float lossReductionFactor = 0.5f;

        [Header("Status Light")]
        public Renderer statusLight;
        public Color    surplusColor   = Color.green;
        public Color    deficitColor   = Color.red;
        public Color    balancedColor  = Color.yellow;

        private void Awake()
        {
            powerDraw   = 0f;
            powerOutput = 0f;
            priority    = 1; // Relays are critical infrastructure.
        }

        private void Start()
        {
            PowerNetworkManager.Instance?.RegisterNode(this);
        }

        private void OnDestroy()
        {
            PowerNetworkManager.Instance?.UnregisterNode(this);
        }

        private void Update()
        {
            UpdateStatusLight();
        }

        private void UpdateStatusLight()
        {
            if (statusLight == null || Network == null) return;
            float net = Network.TotalGeneration - Network.TotalConsumption;
            Color c;
            if (net > 5f)       c = surplusColor;
            else if (net < -5f) c = deficitColor;
            else                c = balancedColor;
            statusLight.material.color = c;
        }

        // PowerNode — passthrough; no draw or output.
        public override float GetCurrentDraw()   => 0f;
        public override float GetCurrentOutput() => 0f;

        // ── Inspection readout ─────────────────────────────────────────────

        public string GetReadout()
        {
            if (Network == null) return "No network";
            var s = Network.GetNetworkSummary();
            float net = s.totalGeneration - s.totalConsumption;
            string status = net >= 0f ? "<color=green>SURPLUS</color>" : "<color=red>OVERDRAW</color>";
            return $"Net ID: {s.networkId}\n" +
                   $"Gen: {s.totalGeneration:F0}W  Draw: {s.totalConsumption:F0}W\n" +
                   $"Battery: {s.batteryLevel:F0}/{s.batteryCapacity:F0} Ws\n" +
                   $"Status: {status}";
        }
    }
}
