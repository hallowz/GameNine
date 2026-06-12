using System;
using UnityEngine;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.1 — base power-network node. Auto-registers on Enable, deregisters
    /// on Disable; this keeps V7's <see cref="Voidborne.Building.BlockPlacer"/>
    /// free of any power-specific plumbing (per the V7 review heads-up:
    /// <i>"PowerNode auto-registers in OnEnable on the same placed prefab --
    /// no plumbing needed"</i>).
    ///
    /// One node can be a generator AND a storage AND a consumer at the same
    /// time -- the three role flags are NOT mutually exclusive. Concrete
    /// subclasses set those flags + the wattage fields:
    /// <list type="bullet">
    /// <item><description>Generators (<see cref="IsGenerator"/>) set
    /// <see cref="MaxOutputWatts"/>.</description></item>
    /// <item><description>Consumers (<see cref="IsConsumer"/>) set
    /// <see cref="RequiredWatts"/>.</description></item>
    /// <item><description>Storage (<see cref="IsStorage"/>) is handled by
    /// <see cref="Battery"/>.</description></item>
    /// </list>
    /// <see cref="CurrentWatts"/> is written by <see cref="PowerNetwork"/>
    /// each tick -- this is the only field consumers should poll when they
    /// want to know "do I have power right now?".
    /// </summary>
    /// <remarks>
    /// Coop note: nodes are local objects (MonoBehaviour, not NetworkBehaviour).
    /// In V21 the network tick runs server-authoritative; the server pushes
    /// the resolved <see cref="CurrentWatts"/> per-node to clients each tick.
    /// The deterministic tick algorithm in <see cref="PowerNetwork"/> means a
    /// client running the same inputs reaches the same outputs, so the wire
    /// payload can be a sparse delta.
    /// </remarks>
    public class PowerNode : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Role flags (subclasses set these in their fields/Awake)
        // ---------------------------------------------------------------

        [Header("Power Node Role")]
        [Tooltip("True if this node produces power. NOT exclusive with the other role flags.")]
        [SerializeField] protected bool isGenerator;

        [Tooltip("True if this node consumes power. NOT exclusive with the other role flags.")]
        [SerializeField] protected bool isConsumer;

        [Tooltip("True if this node stores power (charges on surplus, discharges on deficit). NOT exclusive.")]
        [SerializeField] protected bool isStorage;

        /// <summary>True if this node produces power.</summary>
        public bool IsGenerator => isGenerator;

        /// <summary>True if this node consumes power.</summary>
        public bool IsConsumer => isConsumer;

        /// <summary>True if this node stores power.</summary>
        public bool IsStorage => isStorage;

        // ---------------------------------------------------------------
        //  Wattage fields
        // ---------------------------------------------------------------

        [Header("Power Node Watts")]
        [Tooltip("Watts this generator outputs at peak. Subclasses can scale CurrentWatts <= MaxOutputWatts.")]
        [SerializeField] protected int maxOutputWatts;

        [Tooltip("Watts this consumer needs to run at full speed. Set by subclass / by MachineDefinition.")]
        [SerializeField] protected int requiredWatts;

        /// <summary>Peak watts this generator can produce. Read-only to consumers.</summary>
        public int MaxOutputWatts => maxOutputWatts;

        /// <summary>Watts this consumer needs at full satisfaction. Read-only to consumers.</summary>
        public int RequiredWatts => requiredWatts;

        /// <summary>
        /// Watts currently flowing through this node, written by
        /// <see cref="PowerNetwork"/> each tick.
        /// <list type="bullet">
        /// <item><description>For generators: actual production this tick
        /// (might be 0 if the generator's gating condition is off,
        /// e.g. SteamGenerator with no boiler).</description></item>
        /// <item><description>For consumers: watts received this tick.
        /// Less than <see cref="RequiredWatts"/> means underpowered.</description></item>
        /// <item><description>For batteries: positive = charging, negative =
        /// discharging.</description></item>
        /// </list>
        /// </summary>
        public int CurrentWatts { get; internal set; }

        /// <summary>
        /// Priority order for power distribution. 0 = highest (machines),
        /// 1 = gadgets, 2 = sinks. Lower numbers get power first. The
        /// <see cref="PowerNetwork"/> sorts consumers by this each tick.
        /// </summary>
        public int Priority { get; protected set; } = 0;

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>
        /// Raised when <see cref="CurrentWatts"/> changes between ticks. UI /
        /// downstream subscribers wire here instead of polling each frame.
        /// </summary>
        public event Action OnPowerChanged;

        /// <summary>
        /// Hook used by <see cref="PowerNetwork"/> to atomically update
        /// <see cref="CurrentWatts"/> + raise the change event. Also the
        /// public EditMode test seam: tests use this to inject a synthetic
        /// "as if the network just ticked" wattage on a consumer.
        /// </summary>
        public void SetCurrentWatts(int watts)
        {
            if (CurrentWatts == watts) return;
            CurrentWatts = watts;
            OnPowerChanged?.Invoke();
        }

        // ---------------------------------------------------------------
        //  Lifecycle (auto-register / deregister)
        //
        //  Registration runs from Awake (NOT OnEnable) so EditMode tests
        //  that AddComponent<PowerNode>() see the node land in the network
        //  immediately -- Unity does not fire OnEnable on AddComponent in
        //  EditMode unless the host scene is in play mode.
        // ---------------------------------------------------------------

        protected virtual void Awake()
        {
            InitializeRole();
            RegisterSelf();
        }

        protected virtual void OnEnable()
        {
            // Idempotent: RegisterNode is a no-op for already-registered
            // nodes. This keeps a disabled-then-enabled node behaving
            // correctly in PlayMode.
            InitializeRole();
            RegisterSelf();
        }

        /// <summary>
        /// Subclass hook: set the role flags + default wattage. Called
        /// from <see cref="Awake"/> and from <see cref="ForceRegister"/>
        /// so EditMode tests that don't get Awake auto-fired still see a
        /// fully-initialised node.
        /// </summary>
        protected virtual void InitializeRole()
        {
        }

        protected virtual void OnDisable()
        {
            PowerNetwork.Instance?.UnregisterNode(this);
            // Clear watts so a re-enable starts cleanly.
            if (CurrentWatts != 0)
            {
                CurrentWatts = 0;
                OnPowerChanged?.Invoke();
            }
        }

        private void RegisterSelf()
        {
            PowerNetwork.Instance?.RegisterNode(this);
        }

        // ---------------------------------------------------------------
        //  Subclass mutators
        // ---------------------------------------------------------------

        /// <summary>
        /// Configure this node's wattage. Used by subclasses + by
        /// <see cref="PowerConsumerNode.Configure"/>. Public so EditMode
        /// tests can spin synthetic nodes without subclassing.
        /// Also re-registers the node with <see cref="PowerNetwork"/> --
        /// EditMode tests benefit because Unity does NOT fire Awake on
        /// AddComponent in EditMode unless the GO is in play mode.
        /// </summary>
        public void ConfigureNode(bool generator, bool consumer, bool storage,
                                  int maxOutput = 0, int required = 0, int priority = 0)
        {
            isGenerator = generator;
            isConsumer = consumer;
            isStorage = storage;
            maxOutputWatts = maxOutput;
            requiredWatts = required;
            Priority = priority;
            RegisterSelf();
        }

        /// <summary>
        /// EditMode test seam -- force this node to register with the
        /// current <see cref="PowerNetwork"/>. Awake-driven registration
        /// is suppressed in EditMode (Unity doesn't fire Awake on
        /// AddComponent there); subclasses + fixtures call this directly.
        /// Idempotent; safe to call repeatedly.
        /// </summary>
        public void ForceRegister()
        {
            InitializeRole();
            RegisterSelf();
        }
    }
}
