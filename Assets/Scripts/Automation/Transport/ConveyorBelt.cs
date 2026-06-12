using UnityEngine;
using Voidborne.Building;
using Voidborne.Power;

namespace Voidborne.Automation.Transport
{
    /// <summary>
    /// V9.5 — Single-cell conveyor belt segment.
    ///
    /// <para>Each placed conveyor occupies one BuildGrid cell and holds at
    /// most one <see cref="ItemStack"/>. On <see cref="AdvanceTick"/> it
    /// attempts to push its held item into the adjacent forward cell -- which
    /// may itself be a <see cref="ConveyorBelt"/> (chained) or any other
    /// component that implements <see cref="IBeltSink"/>.</para>
    ///
    /// <para>Movement gating:
    /// <list type="bullet">
    /// <item><description>Movement speed scales with
    /// <see cref="PowerSatisfaction"/> via the V8.3 contract (full power =
    /// 1 cell per <see cref="cellsPerSecond"/> reciprocal; half power = half
    /// speed).</description></item>
    /// <item><description>An item must spend at least
    /// 1 / <see cref="cellsPerSecond"/> seconds on the belt before it can
    /// hop to the next cell. This makes long belts visibly slow.</description></item>
    /// <item><description>If the forward target is full or unreachable, the
    /// belt stalls -- the item stays put.</description></item>
    /// </list></para>
    ///
    /// <para>Power: each segment owns its own <see cref="PowerNode"/>
    /// (consumer role), drawing <see cref="powerDrawWatts"/> watts. Long
    /// belts therefore aggregate draw, matching the V8/V9.5 heads-up that
    /// "per-segment PowerConsumerNode" is the right shape.</para>
    /// </summary>
    /// <remarks>
    /// Coop note: belt state is server-authoritative in V21. The local
    /// AdvanceTick is deterministic given the same cell adjacency + power
    /// satisfaction, so client+server agree on item motion without explicit
    /// replication. Item carried + forward direction are the only fields
    /// that need server replication.
    /// </remarks>
    public class ConveyorBelt : MonoBehaviour, IBeltSink
    {
        // ---------------------------------------------------------------
        //  Configuration
        // ---------------------------------------------------------------

        [Tooltip("Forward direction in world cells. The belt pushes items toward (cell + forward). Default +X.")]
        [SerializeField] private Vector3Int forward = new Vector3Int(1, 0, 0);

        [Tooltip("Cells moved per second at full power. Default 0.5 (= 1 cell every 2 seconds).")]
        [SerializeField] private float cellsPerSecond = 0.5f;

        [Tooltip("Watts drawn per segment. Default 10W.")]
        [SerializeField] private int powerDrawWatts = 10;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private ItemStack _carried;
        private float _timeOnBelt;
        private PowerNode _powerConsumer;
        private PlacedBlock _placedBlock;

        // EditMode test override: when not null, AdvanceTick uses this instead
        // of reading the sibling PowerNode. Lets tests drive movement without
        // a full PowerNetwork.
        private float? _powerSatisfactionOverride;

        // ---------------------------------------------------------------
        //  Public state
        // ---------------------------------------------------------------

        /// <summary>The currently carried item stack (empty when belt is idle).</summary>
        public ItemStack Carried => _carried;

        /// <summary>True if this belt currently has an item on it.</summary>
        public bool HasItem => !_carried.IsEmpty;

        /// <summary>True if this belt is at capacity (max 1 item per single-cell segment).</summary>
        public bool IsFull => HasItem;

        /// <summary>Forward direction in cell-space.</summary>
        public Vector3Int Forward
        {
            get => forward;
            set => forward = value;
        }

        /// <summary>Cells moved per second at full power.</summary>
        public float CellsPerSecond
        {
            get => cellsPerSecond;
            set => cellsPerSecond = Mathf.Max(0.01f, value);
        }

        /// <summary>Watts drawn per segment.</summary>
        public int PowerDrawWatts => powerDrawWatts;

        /// <summary>
        /// Current power satisfaction (0..1). Reads from the sibling
        /// <see cref="PowerNode"/> when present, or from the EditMode test
        /// override. Returns 1 when no power node is attached.
        /// </summary>
        public float PowerSatisfaction
        {
            get
            {
                if (_powerSatisfactionOverride.HasValue) return Mathf.Clamp01(_powerSatisfactionOverride.Value);
                if (_powerConsumer == null) ResolvePowerConsumer();
                if (_powerConsumer == null || _powerConsumer.RequiredWatts <= 0) return 1f;
                int have = Mathf.Max(0, _powerConsumer.CurrentWatts);
                return Mathf.Clamp01((float)have / _powerConsumer.RequiredWatts);
            }
        }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            ResolvePowerConsumer();
        }

        private void OnEnable()
        {
            ResolvePowerConsumer();
        }

        private void ResolvePowerConsumer()
        {
            if (_powerConsumer == null) _powerConsumer = GetComponent<PowerNode>();
            if (_placedBlock == null) _placedBlock = GetComponent<PlacedBlock>();
        }

        /// <summary>
        /// EditMode test seam -- attach a generic <see cref="PowerNode"/>
        /// configured as a consumer drawing <see cref="powerDrawWatts"/>.
        /// </summary>
        public void EnsurePowerConsumerNode()
        {
            if (_powerConsumer != null) return;
            _powerConsumer = GetComponent<PowerNode>();
            if (_powerConsumer == null) _powerConsumer = gameObject.AddComponent<PowerNode>();
            _powerConsumer.ConfigureNode(generator: false, consumer: true, storage: false,
                required: powerDrawWatts, priority: 1);
        }

        /// <summary>
        /// EditMode test seam -- bypass the live PowerNetwork and drive
        /// satisfaction directly. Pass null to clear the override.
        /// </summary>
        public void SetPowerSatisfactionOverride(float? satisfaction)
        {
            _powerSatisfactionOverride = satisfaction;
        }

        // ---------------------------------------------------------------
        //  IBeltSink
        // ---------------------------------------------------------------

        /// <summary>
        /// Try to deposit <paramref name="stack"/> onto this belt. Returns
        /// false if the belt is already carrying something.
        /// </summary>
        public bool TryInsert(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            if (HasItem) return false;
            _carried = stack;
            _timeOnBelt = 0f;
            return true;
        }

        /// <summary>True if the belt can accept an item right now.</summary>
        public bool CanInsert => !HasItem;

        /// <summary>
        /// Extract the currently-carried item if any. Returns the carried
        /// stack (empty when belt was idle) and clears the carried state.
        /// Used by V9.5 Inserter when pulling from a belt source.
        /// </summary>
        public ItemStack TryExtract()
        {
            if (!HasItem) return default;
            var result = _carried;
            _carried = default;
            _timeOnBelt = 0f;
            return result;
        }

        // ---------------------------------------------------------------
        //  Tick
        // ---------------------------------------------------------------

        /// <summary>
        /// Drive the belt forward by <paramref name="deltaSeconds"/> of wall
        /// time. EditMode tests call this directly; PlayMode auto-tick lands
        /// in M7 polish (existing AutomationTickManager registration is the
        /// likely host).
        /// </summary>
        public void AdvanceTick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            if (!HasItem) return;

            float satisfaction = PowerSatisfaction;
            if (satisfaction <= 0f) return; // stalled by no power
            _timeOnBelt += deltaSeconds * satisfaction;

            // Item must spend a full 1/cellsPerSecond seconds on the belt
            // before it can hop forward.
            float requiredDwell = 1f / Mathf.Max(0.01f, cellsPerSecond);
            if (_timeOnBelt < requiredDwell) return;

            TryHandoff();
        }

        private void TryHandoff()
        {
            var nextSink = ResolveForwardSink();
            if (nextSink == null) return;
            if (!nextSink.CanInsert) return;

            if (nextSink.TryInsert(_carried))
            {
                _carried = default;
                _timeOnBelt = 0f;
            }
        }

        private IBeltSink ResolveForwardSink()
        {
            // Prefer BlockRegistry lookup if we have a PlacedBlock + registry.
            if (_placedBlock == null) _placedBlock = GetComponent<PlacedBlock>();
            BlockRegistry reg = BlockRegistry.Instance;
            if (reg != null && _placedBlock != null)
            {
                Vector3Int probe = _placedBlock.Cell + forward;
                PlacedBlock at = reg.GetAt(probe);
                if (at != null)
                {
                    var sink = at.GetComponent<IBeltSink>();
                    if (sink != null) return sink;
                }
            }

            // Fallback: scan for any IBeltSink within 1 unit forward in world
            // space (test fixtures that don't pin a BuildGrid origin still
            // work as long as belts are positioned adjacently).
            return ResolveSinkByWorldProbe();
        }

        private IBeltSink ResolveSinkByWorldProbe()
        {
            Vector3 worldForward = new Vector3(forward.x, forward.y, forward.z);
            Vector3 probePos = transform.position + worldForward;
            // Linear scan -- belt counts will be small. M7 polish can wire a
            // dedicated spatial index here.
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            float bestDist = 0.5f * 0.5f; // half-cell tolerance
            IBeltSink best = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == this) continue;
                if (!(all[i] is IBeltSink sink)) continue;
                float d2 = (all[i].transform.position - probePos).sqrMagnitude;
                if (d2 <= bestDist)
                {
                    bestDist = d2;
                    best = sink;
                }
            }
            return best;
        }
    }
}
