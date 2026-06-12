using UnityEngine;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.1 — a single placed cable connecting two <see cref="PowerNode"/>
    /// endpoints. Self-registers with <see cref="PowerNetwork"/> on Enable
    /// and de-registers on Disable. Owns an optional <see cref="LineRenderer"/>
    /// for the in-world visual (a straight line between the two endpoints --
    /// M2 minimum-viable; M7 polish can add routed splines and pylons).
    /// </summary>
    /// <remarks>
    /// CableSegment placement is a SEPARATE path from <see cref="Voidborne.Building.BlockPlacer"/>
    /// per the V7 review heads-up: cables are edges (two-point) not cells
    /// (one-point). See <see cref="CablePlacer"/> for the input controller.
    ///
    /// Coop note: cable state is server-authoritative in V21. The Init() +
    /// burnout events are the only mutations; they map cleanly to ServerRpc.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CableSegment : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------

        [SerializeField] private PowerNode endpointA;
        [SerializeField] private PowerNode endpointB;

        [Tooltip("The cable tier. Determines the burnout ceiling (T1=200W, T2=1000W, T3=5000W).")]
        [SerializeField] private PowerCableTier tier = PowerCableTier.T1;

        /// <summary>The first endpoint of this cable.</summary>
        public PowerNode EndpointA => endpointA;

        /// <summary>The second endpoint of this cable.</summary>
        public PowerNode EndpointB => endpointB;

        /// <summary>The cable tier.</summary>
        public PowerCableTier Tier => tier;

        /// <summary>Watts currently flowing through this cable, written by <see cref="PowerNetwork.TickOnce"/> each tick.</summary>
        public int CurrentWatts { get; internal set; }

        /// <summary>Visible line for the placed cable (optional).</summary>
        private LineRenderer _line;

        // ---------------------------------------------------------------
        //  Init
        // ---------------------------------------------------------------

        /// <summary>
        /// Bind this cable to two endpoints + a tier. Called by
        /// <see cref="CablePlacer"/> immediately after Instantiate; tests
        /// drive it directly. Idempotent for re-init.
        /// </summary>
        public void Init(PowerNode a, PowerNode b, PowerCableTier cableTier)
        {
            endpointA = a;
            endpointB = b;
            tier = cableTier;
            // Register with the network now that endpoints are wired.
            // ConnectCable is idempotent so re-init / re-enable paths are
            // safe to call repeatedly.
            PowerNetwork.Instance?.ConnectCable(this);
            RefreshLine();
        }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            // Register from Awake so EditMode tests that synthesize cables
            // via AddComponent + Init see them land in the graph
            // immediately. Init() also re-registers idempotently, so the
            // public Init() call from CablePlacer still works.
            PowerNetwork.Instance?.ConnectCable(this);
        }

        private void OnEnable()
        {
            PowerNetwork.Instance?.ConnectCable(this);
            RefreshLine();
        }

        private void OnDisable()
        {
            PowerNetwork.Instance?.DisconnectCable(this);
        }

        private void LateUpdate()
        {
            // Keep the visual snapped to its endpoints in case either moved.
            // Cheap -- one matrix read per endpoint, no allocation.
            if (_line == null) return;
            if (endpointA != null) _line.SetPosition(0, endpointA.transform.position);
            if (endpointB != null) _line.SetPosition(1, endpointB.transform.position);
        }

        // ---------------------------------------------------------------
        //  Internal hooks
        // ---------------------------------------------------------------

        /// <summary>Used by <see cref="PowerNetwork"/> to write per-tick flow.</summary>
        internal void SetCurrentWatts(int watts) => CurrentWatts = watts;

        /// <summary>
        /// Called by <see cref="PowerNetwork"/> when the cable exceeds its
        /// tier ceiling and is being removed. M2: just destroy the GO; M7
        /// will swap a charred prefab + smoke VFX.
        /// </summary>
        internal void NotifyBurnedOut()
        {
            if (gameObject == null) return;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        // ---------------------------------------------------------------
        //  Visual
        // ---------------------------------------------------------------

        private void RefreshLine()
        {
            if (endpointA == null || endpointB == null) return;
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
                _line.positionCount = 2;
                _line.widthMultiplier = 0.05f;
                _line.useWorldSpace = true;
                // LineRenderer has no default material under URP — without one
                // the cable renders magenta/invisible. Sprites/Default is
                // unlit and respects vertex colors.
                if (_line.sharedMaterial == null)
                {
                    _line.material = new Material(
                        Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                    var copper = new Color(0.72f, 0.45f, 0.2f);
                    _line.startColor = copper;
                    _line.endColor   = copper;
                }
            }
            _line.SetPosition(0, endpointA.transform.position);
            _line.SetPosition(1, endpointB.transform.position);
        }
    }
}
