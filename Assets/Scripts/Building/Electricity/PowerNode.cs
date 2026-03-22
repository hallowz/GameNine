using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Base class for all objects that participate in a power network.
    /// Generators set powerOutput > 0 and powerDraw = 0.
    /// Consumers set powerDraw > 0 and powerOutput = 0.
    /// Batteries can do both (charge = draw, discharge = output).
    /// </summary>
    public abstract class PowerNode : MonoBehaviour
    {
        [Header("Power Node")]
        [Tooltip("Watts this node consumes when active.")]
        public float powerDraw;

        [Tooltip("Watts this node produces when active.")]
        public float powerOutput;

        [Tooltip("Priority: lower number = powered first when supply is limited. 0=life-support, 10=lights.")]
        public int priority = 5;

        // Set by PowerNetwork when the network ticks.
        public bool IsPowered { get; private set; }

        public PowerNetwork Network { get; private set; }

        // ── Network registration ───────────────────────────────────────────

        public void JoinNetwork(PowerNetwork network)
        {
            Network = network;
        }

        public void LeaveNetwork()
        {
            Network = null;
            SetPowered(false);
        }

        public void SetPowered(bool powered)
        {
            if (IsPowered == powered) return;
            IsPowered = powered;
            OnPowerChanged(powered);
        }

        // ── Override in subclasses ─────────────────────────────────────────

        /// <summary>Called when the powered state changes.</summary>
        protected virtual void OnPowerChanged(bool powered) { }

        /// <summary>
        /// Called each network tick. Generators should return their current output watts.
        /// Consumers return 0 (they don't produce).
        /// </summary>
        public virtual float GetCurrentOutput() => IsPowered ? powerOutput : 0f;

        /// <summary>Called each network tick.</summary>
        public virtual float GetCurrentDraw() => powerDraw;
    }
}
