using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Enclosed item transport segment. Items travel invisibly through opaque tube segments.
    ///
    /// Behaviour:
    ///   • Faster than conveyor belts (0.15 s tick — registered on the fast list).
    ///   • Items enter at the entry port and exit at the next connected node.
    ///   • The tube's Renderer emits a faint glow when active; it pulses brighter when
    ///     an item transits through this segment.
    ///   • Higher power cost than belts (10W per segment when active).
    ///   • Ideal for long-distance runs through walls, rock, and ceilings.
    ///
    /// Connection: daisy-chain PneumaticTube segments by setting each tube's nextNode to
    /// the next tube or machine/hopper in the run. The final tube in a run should point
    /// to an InputHopper or machine input port.
    /// </summary>
    public class PneumaticTube : MonoBehaviour, IAutomationNode, ITickable
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Connection")]
        [Tooltip("Next node in the tube run (another PneumaticTube, Hopper, or machine input).")]
        [SerializeField] private MonoBehaviour nextNodeMono;

        [Header("Capacity")]
        [Tooltip("Maximum items in transit within this tube segment.")]
        [SerializeField] private int maxTransitItems = 3;

        [Header("Visuals")]
        [SerializeField] private Renderer tubeRenderer;
        [Tooltip("Emission color when the tube is active (items inside).")]
        [SerializeField] private Color activeGlow   = new Color(0.2f, 0.8f, 1.0f);
        [Tooltip("Emission color when idle.")]
        [SerializeField] private Color idleGlow     = new Color(0.05f, 0.2f, 0.3f);
        [Tooltip("Emission color during item pulse.")]
        [SerializeField] private Color pulseColor   = Color.white;
        [SerializeField] private float pulseDuration = 0.1f;

        // ── Runtime ────────────────────────────────────────────────────────

        private IAutomationNode _nextNode;

        // Queue: index 0 = front (nearest exit), last = just entered.
        private readonly Queue<ItemStack> _transit = new Queue<ItemStack>();

        private float _pulseTimer;

        // Cache the emission property ID once.
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;

        // ── ITickable ──────────────────────────────────────────────────────

        public int TickPriority => 20;

        public void AutomationTick()
        {
            if (_transit.Count == 0) return;

            var front = _transit.Peek();
            if (_nextNode != null && _nextNode.CanAccept(front.item))
            {
                _nextNode.TryInsert(front);
                _transit.Dequeue();
                TriggerPulse();
            }
            // else: tube stalls — item waits.
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
            => item != null && _transit.Count < maxTransitItems;

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty || _transit.Count >= maxTransitItems) return false;
            _transit.Enqueue(stack);
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            if (_transit.Count == 0) return false;
            if (filter == null) return true;
            foreach (var s in _transit)
                if (s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            // Tubes don't support mid-queue extraction; only the front item can exit.
            if (_transit.Count == 0) return new ItemStack(null, 0);
            var front = _transit.Peek();
            if (filter == null || front.item == filter)
            {
                _transit.Dequeue();
                return front;
            }
            return new ItemStack(null, 0);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private AutomationConnector _outputConn;

        private void Awake()
        {
            _nextNode = nextNodeMono as IAutomationNode;
            _mpb      = new MaterialPropertyBlock();

            foreach (var c in GetComponentsInChildren<AutomationConnector>())
            {
                c.OnConnectionChanged += RefreshConnections;
                if (c.portId == "output") _outputConn = c;
            }
        }

        private void RefreshConnections()
        {
            if (_outputConn != null)
                _nextNode = _outputConn.GetConnectedNode() ?? (nextNodeMono as IAutomationNode);
        }

        private void OnEnable()
        {
            // Tubes use the fast tick (0.15 s).
            AutomationTickManager.Instance?.RegisterFast(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
        }

        private void Update()
        {
            UpdateGlow();
        }

        // ── Glow / pulse visuals ───────────────────────────────────────────

        private void TriggerPulse()
        {
            _pulseTimer = pulseDuration;
        }

        private Color _prevGlowColor;

        private void UpdateGlow()
        {
            if (tubeRenderer == null) return;

            Color target;
            if (_pulseTimer > 0f)
            {
                _pulseTimer -= Time.deltaTime;
                float t = _pulseTimer / pulseDuration;
                target = Color.Lerp(_transit.Count > 0 ? activeGlow : idleGlow, pulseColor, t);
            }
            else
            {
                target = _transit.Count > 0 ? activeGlow : idleGlow;
            }

            // Skip property block update if color hasn't changed
            if (target.r == _prevGlowColor.r && target.g == _prevGlowColor.g &&
                target.b == _prevGlowColor.b && target.a == _prevGlowColor.a)
                return;

            _prevGlowColor = target;
            tubeRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColor, target);
            tubeRenderer.SetPropertyBlock(_mpb);
        }

        // ── Public helpers ─────────────────────────────────────────────────

        public void ConnectNext(IAutomationNode node) => _nextNode = node;

        public int TransitCount => _transit.Count;

        /// <summary>
        /// Set the physical length of this tube segment (meters).
        /// Called by SegmentPlacementController when placing wire-style segments.
        /// </summary>
        public void SetSegmentLength(float length)
        {
            _segmentLength = Mathf.Max(0.1f, length);
        }

        // Segment length — 1 unit for legacy block placement, configurable for wire-style.
        private float _segmentLength = 1f;

        /// <summary>Physical length of this tube segment in meters.</summary>
        public float SegmentLength => _segmentLength;
    }
}
