using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Absorbs surplus power, releases during deficit.
    /// Capacity: 5000 Wh per tier. Degrades over charge cycles.
    /// </summary>
    public class BatteryBank : PowerNode
    {
        [Header("Battery Bank")]
        public float maxCapacity         = 5000f; // watt-seconds per bank
        public float degradationPerCycle = 0.001f; // capacity lost per full cycle
        public int   maxCycles           = 1000;

        [Header("State")]
        [SerializeField] private float _storedEnergy;
        [SerializeField] private float _cycleCount;
        [SerializeField] private float _partialCycle; // tracks fractional cycles

        [Header("FX")]
        public Renderer chargeIndicator;
        public Color     fullColor  = Color.green;
        public Color     emptyColor = Color.red;

        public float StoredEnergy     => _storedEnergy;
        public float ChargePercent    => maxCapacity > 0f ? _storedEnergy / maxCapacity : 0f;
        public float RemainingCycles  => maxCycles - _cycleCount;
        public bool  NeedsReplacement => _cycleCount >= maxCycles;

        private void Awake()
        {
            powerDraw   = 0f;
            powerOutput = 0f;
            priority    = 5;
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
            UpdateIndicator();
        }

        // ── Called by PowerNetwork ─────────────────────────────────────────

        /// <summary>Absorbs up to wattSeconds of energy. Returns amount absorbed.</summary>
        public float Charge(float wattSeconds)
        {
            if (NeedsReplacement) return 0f;
            float space = maxCapacity - _storedEnergy;
            float absorbed = Mathf.Min(space, wattSeconds);
            _storedEnergy += absorbed;

            TrackCycle(absorbed / maxCapacity);
            return absorbed;
        }

        /// <summary>Releases up to wattSeconds of energy. Returns amount released.</summary>
        public float Discharge(float wattSeconds)
        {
            float released = Mathf.Min(_storedEnergy, wattSeconds);
            _storedEnergy -= released;
            TrackCycle(released / maxCapacity);
            return released;
        }

        private void TrackCycle(float fraction)
        {
            _partialCycle += fraction;
            while (_partialCycle >= 1f)
            {
                _partialCycle -= 1f;
                _cycleCount++;
                // Degrade max capacity.
                maxCapacity = Mathf.Max(0f, maxCapacity - maxCapacity * degradationPerCycle);
            }
        }

        // PowerNode overrides — battery doesn't draw or produce in the normal sense;
        // the PowerNetwork handles charge/discharge directly.
        public override float GetCurrentDraw()   => 0f;
        public override float GetCurrentOutput() => 0f;

        // ── Visual ────────────────────────────────────────────────────────

        private void UpdateIndicator()
        {
            if (chargeIndicator == null) return;
            chargeIndicator.material.color = Color.Lerp(emptyColor, fullColor, ChargePercent);
        }
    }
}
