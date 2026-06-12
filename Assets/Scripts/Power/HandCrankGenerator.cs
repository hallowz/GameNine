using UnityEngine;
using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.2 — Hand Crank Generator. Picky_Specialty machine in the Core 60
    /// (<c>hand_crank_generator</c>). Outputs
    /// <see cref="GameConstants.Power.HandCrankGeneratorOutputWatts"/> watts
    /// (30W) for as long as the player is holding E within
    /// <see cref="CrankRadius"/> metres.
    ///
    /// <para>This is the BOOTSTRAP generator -- it's how a new world gets
    /// its first 30W to power a single low-draw machine without coal /
    /// without a built boiler. M2 ships this as the only no-fuel,
    /// no-pre-requisite power source.</para>
    /// </summary>
    /// <remarks>
    /// Auto-attached on placement by <see cref="Voidborne.Building.BlockPlacer"/>
    /// when a <c>hand_crank_generator</c> item lands (V8.3 wiring).
    ///
    /// Test seam <see cref="IsBeingCranked"/> is settable so tests can
    /// drive the "player is cranking" state without needing a real
    /// player + input loop.
    /// </remarks>
    public class HandCrankGenerator : PowerGenerator
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Tooltip("Distance (metres) within which the player can crank this generator.")]
        [SerializeField] private float crankRadius = 2f;

        [Tooltip("Optional explicit reference to the player transform (FindObjectOfType-fallback used when null).")]
        [SerializeField] private Transform playerTransform;

        /// <summary>The crank-engagement radius.</summary>
        public float CrankRadius => crankRadius;

        /// <summary>
        /// True iff the player is currently engaging this generator. Set
        /// by <see cref="Update"/> when the player is in range + holding
        /// the engage key, OR settable as a test seam.
        /// </summary>
        public bool IsBeingCranked { get; set; }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected override void InitializeRole()
        {
            maxOutputWatts = GameConstants.Power.HandCrankGeneratorOutputWatts;
            base.InitializeRole();
        }

        // ---------------------------------------------------------------
        //  Input-driven engagement
        // ---------------------------------------------------------------

        private void Update()
        {
            // Resolve player transform on demand.
            if (playerTransform == null)
            {
                var inv = FindFirstObjectByType<PlayerInventory>();
                if (inv != null) playerTransform = inv.transform;
            }

            // M2: any time the player is within crankRadius AND holding the
            // E key (interact action), the generator is cranking. The
            // dedicated InputSystem_Actions binding for an "engage" prompt
            // lands in M7; for now we poll the Use/Interact action via
            // Keyboard.current directly so we don't depend on the action map.
            bool playerInRange = false;
            if (playerTransform != null)
            {
                float sqr = (playerTransform.position - transform.position).sqrMagnitude;
                playerInRange = sqr <= crankRadius * crankRadius;
            }

            bool eHeld = false;
            try
            {
                eHeld = UnityEngine.InputSystem.Keyboard.current != null
                     && UnityEngine.InputSystem.Keyboard.current.eKey != null
                     && UnityEngine.InputSystem.Keyboard.current.eKey.isPressed;
            }
            catch (System.Exception)
            {
                // EditMode fixtures lack a Keyboard.current device; the
                // test-seam IsBeingCranked carries the state in those cases.
                eHeld = false;
            }

            // Either the input loop or the test seam can flip the flag on.
            // Don't override a test-seam true with a derived false in the
            // same frame -- tests set IsBeingCranked once and expect it to
            // stick across TickOnce() calls.
            if (Application.isPlaying)
            {
                IsBeingCranked = playerInRange && eHeld;
            }
        }

        // ---------------------------------------------------------------
        //  Gen tick
        // ---------------------------------------------------------------

        /// <summary>Returns 30W while <see cref="IsBeingCranked"/> is true; 0W otherwise.</summary>
        public override int GetTickOutputWatts()
        {
            return IsBeingCranked ? maxOutputWatts : 0;
        }
    }
}
