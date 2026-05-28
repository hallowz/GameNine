using UnityEngine;
using Voidborne.Automation.Machines;
using Voidborne.Building;
using Voidborne.Power;

namespace Voidborne.Automation.Transport
{
    /// <summary>
    /// V9.5 — Inserter Arm. A single-cell powered machine that picks one
    /// item per cooldown from a source <see cref="IBeltSink"/> and drops it
    /// into a target <see cref="IBeltSink"/>.
    ///
    /// <para>M2 mode: <see cref="InserterMode.PullFromChest"/>. The source
    /// is queried via <see cref="sourceDirection"/> (default -X) and the
    /// target via <see cref="targetDirection"/> (default +X). The inserter
    /// is itself a machine, so the V8.3 placement contract attaches its
    /// PowerConsumerNode the same way it does for the crusher.</para>
    ///
    /// <para>Power: 20W draw. Movement rate scales with PowerSatisfaction:
    /// at 0 satisfaction the inserter never advances; at 0.5 it takes twice
    /// as long per item.</para>
    /// </summary>
    /// <remarks>
    /// Coop note: server-authoritative tick. The local AdvanceTick is
    /// deterministic, so a client that observes the same chest contents +
    /// cooldown state agrees on which item moves where on each tick.
    /// </remarks>
    public class Inserter : MachineRuntime, IBeltSink
    {
        public enum InserterMode { PullFromChest, PushToChest, Bidirectional }

        // ---------------------------------------------------------------
        //  Configuration
        // ---------------------------------------------------------------

        [Tooltip("Inserter mode. M2 ships PullFromChest; the other modes are stubs for M7 polish.")]
        [SerializeField] private InserterMode mode = InserterMode.PullFromChest;

        [Tooltip("Direction (in cell-space) of the source. Default -X.")]
        [SerializeField] private Vector3Int sourceDirection = new Vector3Int(-1, 0, 0);

        [Tooltip("Direction (in cell-space) of the target. Default +X.")]
        [SerializeField] private Vector3Int targetDirection = new Vector3Int(1, 0, 0);

        [Tooltip("Cooldown between item pickups in seconds at full power.")]
        [SerializeField] private float cooldownSeconds = 1.0f;

        [Tooltip("Watts drawn by this inserter.")]
        [SerializeField] private int powerDrawWatts = 20;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        private float _cooldownRemaining;
        private float? _powerSatisfactionOverride;

        // ---------------------------------------------------------------
        //  Public accessors
        // ---------------------------------------------------------------

        /// <summary>Current mode (PullFromChest by default in M2).</summary>
        public InserterMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public Vector3Int SourceDirection
        {
            get => sourceDirection;
            set => sourceDirection = value;
        }

        public Vector3Int TargetDirection
        {
            get => targetDirection;
            set => targetDirection = value;
        }

        public float CooldownSeconds => cooldownSeconds;
        public float CooldownRemaining => _cooldownRemaining;
        public int PowerDrawWatts => powerDrawWatts;

        /// <summary>
        /// Power satisfaction. Reads from the sibling PowerConsumerNode (V8.3
        /// auto-attached via MachineRuntime + station Init when the machine
        /// def's needsPower=true) when present. Falls back to the test
        /// override.
        /// </summary>
        public float PowerSatisfaction
        {
            get
            {
                if (_powerSatisfactionOverride.HasValue) return Mathf.Clamp01(_powerSatisfactionOverride.Value);
                var consumer = PowerConsumer;
                if (consumer == null || consumer.RequiredWatts <= 0) return 1f;
                return consumer.PowerSatisfaction;
            }
        }

        /// <summary>EditMode test seam -- force satisfaction to a fixed value.</summary>
        public void SetPowerSatisfactionOverride(float? satisfaction)
        {
            _powerSatisfactionOverride = satisfaction;
        }

        // ---------------------------------------------------------------
        //  Source / target binding (test seams)
        // ---------------------------------------------------------------

        // Optional direct refs for test fixtures that don't pin BuildGrid.
        private MonoBehaviour _sourceOverride;
        private MonoBehaviour _targetOverride;

        public void SetSource(MonoBehaviour source) { _sourceOverride = source; }
        public void SetTarget(MonoBehaviour target) { _targetOverride = target; }

        // ---------------------------------------------------------------
        //  IBeltSink
        // ---------------------------------------------------------------

        // An inserter can receive items into itself via the PushToChest mode
        // in the future, but for M2's PullFromChest we don't buffer. Always
        // refuse direct belt insertion; items flow through, not into.
        public bool CanInsert => false;
        public bool TryInsert(ItemStack stack) => false;

        // ---------------------------------------------------------------
        //  Tick
        // ---------------------------------------------------------------

        public void AdvanceTick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            float satisfaction = PowerSatisfaction;
            if (satisfaction <= 0f) return; // stalled

            // Cooldown counts down at satisfaction-scaled rate.
            _cooldownRemaining -= deltaSeconds * satisfaction;
            if (_cooldownRemaining > 0f) return;

            if (TryMoveOneItem())
            {
                _cooldownRemaining = cooldownSeconds;
            }
        }

        private bool TryMoveOneItem()
        {
            // For M2 we only handle PullFromChest. Other modes are stubs.
            if (mode != InserterMode.PullFromChest && mode != InserterMode.Bidirectional)
            {
                return false;
            }

            // Resolve source (chest) + target (machine input or belt).
            var source = ResolveSource();
            var target = ResolveTarget();
            if (source == null || target == null) return false;

            // Pull one item from the source.
            ItemStack pulled = PullOne(source);
            if (pulled.IsEmpty) return false;

            // Try to deliver to the target.
            if (TryDeliver(target, pulled)) return true;

            // Delivery refused -- put it back so the chest doesn't lose
            // items.
            ReturnToSource(source, pulled);
            return false;
        }

        // ---------------------------------------------------------------
        //  Source / target resolution
        // ---------------------------------------------------------------

        private MonoBehaviour ResolveSource()
        {
            if (_sourceOverride != null) return _sourceOverride;
            return ResolveAdjacent(sourceDirection);
        }

        private MonoBehaviour ResolveTarget()
        {
            if (_targetOverride != null) return _targetOverride;
            return ResolveAdjacent(targetDirection);
        }

        private MonoBehaviour ResolveAdjacent(Vector3Int dir)
        {
            // Prefer BlockRegistry lookup with a PlacedBlock.
            var placed = GetComponent<PlacedBlock>();
            BlockRegistry reg = BlockRegistry.Instance;
            if (reg != null && placed != null)
            {
                Vector3Int probe = placed.Cell + dir;
                PlacedBlock at = reg.GetAt(probe);
                if (at != null) return at;
            }
            return null;
        }

        // ---------------------------------------------------------------
        //  Pull / deliver
        // ---------------------------------------------------------------

        private ItemStack PullOne(MonoBehaviour source)
        {
            // 1) StorageChestRuntime -- pull one of whatever's there.
            var chest = source.GetComponent<StorageChestRuntime>();
            if (chest != null)
            {
                for (int i = 0; i < chest.Contents.SlotCount; i++)
                {
                    var slot = chest.Contents.GetSlot(i);
                    if (!slot.IsEmpty && slot.item != null)
                    {
                        return chest.TryWithdraw(slot.item.itemId, 1);
                    }
                }
                return default;
            }

            // 2) ConveyorBelt -- pull the carried item via TryExtract.
            var belt = source.GetComponent<ConveyorBelt>();
            if (belt != null && belt.HasItem)
            {
                return belt.TryExtract();
            }

            return default;
        }

        private static void ReturnToSource(MonoBehaviour source, ItemStack stack)
        {
            var chest = source.GetComponent<StorageChestRuntime>();
            if (chest != null) { chest.TryDeposit(stack); return; }
            var belt = source.GetComponent<ConveyorBelt>();
            if (belt != null) { belt.TryInsert(stack); }
        }

        private static bool TryDeliver(MonoBehaviour target, ItemStack stack)
        {
            // 1) IBeltSink (chest, belt, etc).
            var sink = target.GetComponent<IBeltSink>();
            if (sink != null && sink.CanInsert)
            {
                return sink.TryInsert(stack);
            }

            // 2) MachineCraftingStation -- push into the first matching /
            //    empty input slot.
            var station = target.GetComponent<MachineCraftingStation>();
            if (station != null)
            {
                return TryPushToStation(station, stack);
            }

            return false;
        }

        private static bool TryPushToStation(MachineCraftingStation station, ItemStack stack)
        {
            var inputs = station.Inputs;
            if (inputs == null || inputs.Count == 0) return false;

            // Merge into existing same-item slot first.
            for (int i = 0; i < inputs.Count; i++)
            {
                var slot = inputs[i];
                if (slot.IsEmpty || slot.item == null) continue;
                if (slot.item != stack.item) continue;
                int space = slot.item.maxStackSize - slot.quantity;
                if (space <= 0) continue;
                int add = Mathf.Min(space, stack.quantity);
                station.SetInput(i, new ItemStack(slot.item, slot.quantity + add));
                if (add >= stack.quantity) return true;
                // Leftover -- look for another slot.
                stack = new ItemStack(stack.item, stack.quantity - add);
                break;
            }

            // Drop into the first empty slot.
            for (int i = 0; i < inputs.Count; i++)
            {
                if (!inputs[i].IsEmpty) continue;
                station.SetInput(i, stack);
                return true;
            }

            return false;
        }
    }
}
