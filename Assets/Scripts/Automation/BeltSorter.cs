using System.Collections.Generic;
using UnityEngine;
using Voidborne.Building.Electricity;

namespace Voidborne.Automation
{
    /// <summary>
    /// A special belt junction with 2–3 outputs. Configure which item types route to
    /// which output. Unfiltered items take the default output.
    ///
    /// Visual: a small diverter arm (the 'diverterArm' Transform) rotates toward the
    /// active output lane when an item passes.
    ///
    /// Setup:
    ///   1. Add up to 3 Route entries in the Inspector (item type → output node).
    ///   2. Assign a defaultOutputMono for items that don't match any rule.
    ///   3. Assign diverterArm for visual feedback.
    /// </summary>
    public class BeltSorter : MonoBehaviour, IAutomationNode, ITickable
    {
        // ── Data ───────────────────────────────────────────────────────────

        [System.Serializable]
        public struct SortRule
        {
            [Tooltip("Item type that matches this rule.")]
            public ItemDefinition item;

            [Tooltip("Output node for matching items (ConveyorBelt, Hopper, or machine input).")]
            public MonoBehaviour outputMono;

            [System.NonSerialized] public IAutomationNode output;
        }

        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Routing Rules")]
        [SerializeField] private List<SortRule> rules = new List<SortRule>();

        [Tooltip("Receives items that don't match any rule.")]
        [SerializeField] private MonoBehaviour defaultOutputMono;

        [Header("Visuals")]
        [Tooltip("Rotates to indicate the active output lane.")]
        [SerializeField] private Transform diverterArm;

        [Tooltip("Local Y-axis rotation angles for each output lane (left, center, right).")]
        [SerializeField] private float armAngleLeft   = -45f;
        [SerializeField] private float armAngleCenter =   0f;
        [SerializeField] private float armAngleRight  =  45f;
        [SerializeField] private float armRotateSpeed = 180f; // degrees per second

        [Header("Power")]
        [Tooltip("Requires 5W to operate. Stops sorting when unpowered.")]
        [SerializeField] private PowerConsumer powerConsumer;

        // ── Runtime ────────────────────────────────────────────────────────

        private IAutomationNode _defaultOutput;

        // Items waiting to be sorted — at most one per tick is processed.
        private readonly Queue<ItemStack> _pending = new Queue<ItemStack>();

        private float _targetArmAngle;

        // ── ITickable ──────────────────────────────────────────────────────

        public int TickPriority => 25;

        public void AutomationTick()
        {
            // Stop sorting when unpowered.
            if (powerConsumer != null && !powerConsumer.IsPowered) return;
            if (_pending.Count == 0) return;

            var stack = _pending.Peek();
            IAutomationNode destination = FindDestination(stack.item);

            if (destination != null && destination.CanAccept(stack.item))
            {
                destination.TryInsert(stack);
                _pending.Dequeue();
                SetDiverterTarget(destination);
            }
            // else: sorter stalls until the destination has space.
        }

        // ── IAutomationNode ────────────────────────────────────────────────

        public bool CanAccept(ItemDefinition item)
            => item != null && _pending.Count < 5; // small internal buffer

        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty || _pending.Count >= 5) return false;
            _pending.Enqueue(stack);
            return true;
        }

        public bool HasItem(ItemDefinition filter)
        {
            if (_pending.Count == 0) return false;
            if (filter == null) return true;
            foreach (var s in _pending)
                if (s.item == filter) return true;
            return false;
        }

        public ItemStack TryExtract(ItemDefinition filter)
        {
            // BeltSorters don't support reverse extraction.
            return new ItemStack(null, 0);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private AutomationConnector _defaultOutConn;

        private void Awake()
        {
            if (powerConsumer == null)
                powerConsumer = GetComponent<PowerConsumer>();

            _defaultOutput = defaultOutputMono as IAutomationNode;
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                r.output = r.outputMono as IAutomationNode;
                rules[i] = r;
            }

            _targetArmAngle = armAngleCenter;

            foreach (var c in GetComponentsInChildren<AutomationConnector>())
            {
                c.OnConnectionChanged += RefreshConnections;
                if (c.portId == "default_output") _defaultOutConn = c;
            }
        }

        private void RefreshConnections()
        {
            if (_defaultOutConn != null)
                _defaultOutput = _defaultOutConn.GetConnectedNode()
                                 ?? (defaultOutputMono as IAutomationNode);
        }

        private void OnEnable()
        {
            AutomationTickManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
        }

        private void Update()
        {
            AnimateDiverterArm();
        }

        // ── Internal ───────────────────────────────────────────────────────

        private IAutomationNode FindDestination(ItemDefinition item)
        {
            foreach (var rule in rules)
            {
                if (rule.item == item && rule.output != null)
                    return rule.output;
            }
            return _defaultOutput;
        }

        private void SetDiverterTarget(IAutomationNode destination)
        {
            if (diverterArm == null) return;
            if (rules.Count == 0) { _targetArmAngle = armAngleCenter; return; }

            // Simple heuristic: first rule = left, second rule = right, default = center.
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].output == destination)
                {
                    _targetArmAngle = i == 0 ? armAngleLeft : armAngleRight;
                    return;
                }
            }
            _targetArmAngle = armAngleCenter; // default output
        }

        private void AnimateDiverterArm()
        {
            if (diverterArm == null) return;
            var current = diverterArm.localEulerAngles;
            float currentY = current.y > 180f ? current.y - 360f : current.y;
            float newY = Mathf.MoveTowardsAngle(currentY, _targetArmAngle, armRotateSpeed * Time.deltaTime);
            diverterArm.localEulerAngles = new Vector3(current.x, newY, current.z);
        }

        // ── Public helpers ─────────────────────────────────────────────────

        /// <summary>Add a routing rule at runtime (e.g., from a UI).</summary>
        public void AddRule(ItemDefinition item, IAutomationNode output)
        {
            rules.Add(new SortRule { item = item, output = output });
        }

        public void SetDefaultOutput(IAutomationNode output) => _defaultOutput = output;
    }
}
