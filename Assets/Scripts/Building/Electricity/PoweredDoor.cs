using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Draws 5W. When powered, opens/closes on interact.
    /// When unpowered, stays in current state (power loss doesn't force-close it).
    /// </summary>
    public class PoweredDoor : PowerNode
    {
        [Header("Powered Door")]
        public float drawWatts = 5f;

        [Header("Animation")]
        public Animator doorAnimator;
        public string   openParam  = "Open";

        private bool _isOpen;

        private void Awake()
        {
            powerDraw   = drawWatts;
            powerOutput = 0f;
            priority    = 3; // Doors are relatively important.
        }

        private void Start()
        {
            PowerNetworkManager.Instance?.RegisterNode(this);
            if (doorAnimator == null)
                doorAnimator = GetComponentInChildren<Animator>();
        }

        private void OnDestroy()
        {
            PowerNetworkManager.Instance?.UnregisterNode(this);
        }

        /// <summary>Called by PlayerInteraction when player interacts with the door.</summary>
        public void Interact()
        {
            if (!IsPowered) return; // Unpowered doors can't be toggled.
            _isOpen = !_isOpen;
            if (doorAnimator != null)
                doorAnimator.SetBool(openParam, _isOpen);
        }

        // When power returns, door stays in whatever state it was left in.
        protected override void OnPowerChanged(bool powered) { }

        public bool IsOpen => _isOpen;
    }
}
