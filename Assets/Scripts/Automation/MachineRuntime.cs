using UnityEngine;
using Voidborne.Power;
using Voidborne.UI;

namespace Voidborne.Automation
{
    /// <summary>
    /// V9.1 — Thin facade over <see cref="MachineCraftingStation"/> + the
    /// optional sibling <see cref="PowerConsumerNode"/>. Lives on the placed
    /// machine prefab and provides:
    /// <list type="bullet">
    /// <item><description>The <see cref="IInteractable"/> hook -- E to open the
    /// Machine UI through <see cref="UIManager.OpenMachineUI"/>.</description></item>
    /// <item><description>A single read-through accessor (<see cref="MachineDef"/>)
    /// that downstream HUD / tooltip consumers can grab without poking inside
    /// the station.</description></item>
    /// <item><description>A virtual <see cref="OnRuntimeInitialized"/> hook so
    /// per-machine subclasses (CampfireRuntime, SteamBoilerRuntime, etc) can
    /// layer behaviour on top of the base station without touching V6.3.</description></item>
    /// </list>
    ///
    /// V6.3 <see cref="MachineCraftingStation"/> already does the heavy lifting
    /// (slot grids, recipe gate via V6.1 engine, AdvanceTick PowerSatisfaction
    /// scaling). V9.1's job is to bundle that into a cohesive component the
    /// V7.1 BlockPlacer can attach with a single AddComponent call, and to give
    /// player interaction a single entry point.
    /// </summary>
    /// <remarks>
    /// Coop note: server-authoritative in V21 (the recipe consume / produce
    /// path lives on the host). The local MachineRuntime opens the Machine UI
    /// for the local player only; the IMachineInputProvider it forwards to the
    /// UIManager is the local mirror of the server's station state.
    /// </remarks>
    [RequireComponent(typeof(MachineCraftingStation))]
    public class MachineRuntime : MonoBehaviour, IInteractable
    {
        // ---------------------------------------------------------------
        //  Inspector / state
        // ---------------------------------------------------------------

        [Tooltip("Interact radius in meters. The Player's PlayerInteraction does its own overlap sphere; this is a safety gate.")]
        [SerializeField] private float interactRange = 4f;

        private MachineCraftingStation _station;
        private PowerConsumerNode _powerConsumer;

        // ---------------------------------------------------------------
        //  Public accessors
        // ---------------------------------------------------------------

        /// <summary>The machine SO this runtime is bound to. Read from the underlying station.</summary>
        public MachineDefinition MachineDef => _station != null ? _station.Machine : null;

        /// <summary>Underlying crafting station. Subclasses + tests may read this.</summary>
        public MachineCraftingStation Station => _station;

        /// <summary>Sibling power consumer node, or null when the machine doesn't draw power.</summary>
        public PowerConsumerNode PowerConsumer => _powerConsumer;

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected virtual void Awake()
        {
            ResolveSiblings();
        }

        protected virtual void OnEnable()
        {
            // Sibling components may have been added between Awake and Enable;
            // re-resolve so MachineDef / PowerConsumer always reflect the
            // current sibling set.
            ResolveSiblings();
            // Allow subclasses (CampfireRuntime, SteamBoilerRuntime, etc) to
            // hook one-shot setup once Init is in place.
            if (_station != null && _station.Machine != null)
            {
                OnRuntimeInitialized(_station.Machine);
            }
        }

        private void ResolveSiblings()
        {
            if (_station == null) _station = GetComponent<MachineCraftingStation>();
            if (_powerConsumer == null) _powerConsumer = GetComponent<PowerConsumerNode>();
        }

        // ---------------------------------------------------------------
        //  Subclass hook
        // ---------------------------------------------------------------

        /// <summary>
        /// Override in per-machine subclasses to layer behaviour on top of the
        /// base station. Called from <see cref="OnEnable"/> once the underlying
        /// <see cref="MachineCraftingStation"/> has a bound
        /// <see cref="MachineDefinition"/>. Safe to be called more than once
        /// (e.g. on a disable+enable cycle); subclasses should be idempotent.
        /// </summary>
        protected virtual void OnRuntimeInitialized(MachineDefinition def)
        {
        }

        // ---------------------------------------------------------------
        //  IInteractable
        // ---------------------------------------------------------------

        /// <summary>
        /// Open the generic Machine UI for this machine. Called by the
        /// Player's PlayerInteraction when E is pressed within range.
        /// Sub-classes that want a custom UI (e.g. StorageChestRuntime's
        /// container modal) should override <see cref="Interact"/>.
        /// </summary>
        public virtual void Interact(GameObject interactor)
        {
            if (_station == null || _station.Machine == null) return;
            if (UIManager.Instance == null) return;
            UIManager.Instance.OpenMachineUI(_station.Machine, _station);
        }

        public virtual bool CanInteract(Vector3 fromPosition)
        {
            if (_station == null || _station.Machine == null) return false;
            float r = Mathf.Max(0.1f, interactRange);
            return (fromPosition - transform.position).sqrMagnitude <= r * r;
        }

        public virtual string InteractPrompt =>
            _station != null && _station.Machine != null && !string.IsNullOrEmpty(_station.Machine.displayName)
                ? $"Open {_station.Machine.displayName}"
                : "Open Machine";

        // ---------------------------------------------------------------
        //  EditMode test seam
        // ---------------------------------------------------------------

        /// <summary>
        /// EditMode test seam -- re-resolve sibling components + replay the
        /// OnRuntimeInitialized hook. Unity does not fire Awake/Enable on
        /// AddComponent in EditMode, so tests call this directly after
        /// AddComponent + station.Init.
        /// </summary>
        public void ForceInitialize()
        {
            ResolveSiblings();
            if (_station != null && _station.Machine != null)
            {
                OnRuntimeInitialized(_station.Machine);
            }
        }
    }
}
