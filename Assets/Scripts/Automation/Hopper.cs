using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Connects a container or machine to a belt or tube.
    ///
    /// Modes:
    ///   Output Hopper — pulls items from the connected container and pushes them
    ///                   onto the connected belt/tube. Ticked first so belts receive
    ///                   items before they advance.
    ///   Input Hopper  — pulls items from the connected belt/tube and pushes them
    ///                   into the connected container. Ticked after belts.
    ///
    /// Filter:
    ///   If an ItemDefinition is assigned to 'filter', only items of that type are
    ///   transferred. The FilterHopper variant simply pre-sets this field at creation.
    ///
    /// Usage:
    ///   1. Set mode to Output or Input.
    ///   2. Assign 'container' to a MonoBehaviour implementing IAutomationNode
    ///      (a chest, machine input port, etc.).
    ///   3. Assign 'beltOrTube' to a ConveyorBelt or PneumaticTube.
    ///   4. Optionally assign a filter item to restrict what flows through.
    /// </summary>
    public class Hopper : MonoBehaviour, ITickable
    {
        // ── Enums ──────────────────────────────────────────────────────────

        public enum HopperMode { Output, Input }

        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Hopper Configuration")]
        [SerializeField] private HopperMode mode = HopperMode.Output;

        [Tooltip("The container or machine this hopper is attached to (must implement IAutomationNode).")]
        [SerializeField] private MonoBehaviour containerMono;

        [Tooltip("The belt or tube this hopper connects to (must implement IAutomationNode).")]
        [SerializeField] private MonoBehaviour beltOrTubeMono;

        [Tooltip("Only transfer items of this type. Leave empty to transfer any item.")]
        [SerializeField] private ItemDefinition filter;

        // ── Runtime ────────────────────────────────────────────────────────

        private IAutomationNode _container;
        private IAutomationNode _beltOrTube;

        // ── ITickable ──────────────────────────────────────────────────────

        // Output hoppers (priority 10) run before belts (20).
        // Input hoppers (priority 30) run after belts.
        public int TickPriority => mode == HopperMode.Output ? 10 : 30;

        public void AutomationTick()
        {
            if (_container == null || _beltOrTube == null) return;

            if (mode == HopperMode.Output)
                TransferFromContainerToBelt();
            else
                TransferFromBeltToContainer();
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private AutomationConnector _beltConn;

        private void Awake()
        {
            _container  = containerMono  as IAutomationNode;
            _beltOrTube = beltOrTubeMono as IAutomationNode;

            foreach (var c in GetComponentsInChildren<AutomationConnector>())
            {
                c.OnConnectionChanged += RefreshConnections;
                if (c.portId == "belt_port") _beltConn = c;
            }
        }

        private void RefreshConnections()
        {
            if (_beltConn != null)
                _beltOrTube = _beltConn.GetConnectedNode() ?? (beltOrTubeMono as IAutomationNode);
        }

        private void OnEnable()
        {
            AutomationTickManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            AutomationTickManager.Instance?.Unregister(this);
        }

        // ── Transfer logic ─────────────────────────────────────────────────

        private void TransferFromContainerToBelt()
        {
            // Source: container. Destination: belt/tube.
            if (!_container.HasItem(filter)) return;

            ItemStack extracted = _container.TryExtract(filter);
            if (extracted.IsEmpty) return;

            if (!_beltOrTube.CanAccept(extracted.item))
            {
                // Return item to container — belt is full.
                _container.TryInsert(extracted);
                return;
            }

            _beltOrTube.TryInsert(extracted);
        }

        private void TransferFromBeltToContainer()
        {
            // Source: belt/tube. Destination: container.
            if (!_beltOrTube.HasItem(filter)) return;

            // Check the container can accept before extracting.
            // We peek by checking HasItem then extracting — small race risk is acceptable
            // since this runs on a single thread.
            ItemStack extracted = _beltOrTube.TryExtract(filter);
            if (extracted.IsEmpty) return;

            if (!_container.CanAccept(extracted.item))
            {
                // Return item to belt — container is full.
                _beltOrTube.TryInsert(extracted);
                return;
            }

            _container.TryInsert(extracted);
        }

        // ── Public helpers ─────────────────────────────────────────────────

        public HopperMode Mode => mode;

        /// <summary>Set filter at runtime (e.g., from UI).</summary>
        public void SetFilter(ItemDefinition newFilter) => filter = newFilter;

        /// <summary>Assign connections programmatically (for placement system).</summary>
        public void Connect(IAutomationNode container, IAutomationNode beltOrTube)
        {
            _container  = container;
            _beltOrTube = beltOrTube;
        }
    }
}
