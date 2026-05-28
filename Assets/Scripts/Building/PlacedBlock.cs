using System;
using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.1 — Component attached to every placed block instance.
    ///
    /// Carries the per-instance state that the legacy <see cref="ItemDefinition"/>
    /// cannot hold (which cell the block occupies, current rotation, hit
    /// points remaining). The cell-based <see cref="BlockRegistry"/> indexes
    /// PlacedBlocks for O(1) "what's at this cell" queries; this component is
    /// the runtime mirror of one entry in that registry.
    /// </summary>
    /// <remarks>
    /// Coop note: PlacedBlock state is server-authoritative in V21. For V7.5
    /// it runs locally; <see cref="TakeDamage"/> returns the new HP so callers
    /// can route damage through a ServerRpc later without changing the API.
    /// </remarks>
    public class PlacedBlock : MonoBehaviour
    {
        /// <summary>Default starting HP for every M2 block. Tool-tier scaling lands in M7.</summary>
        public const int DefaultHealth = 100;

        // ---------------------------------------------------------------
        //  State (serialized so prefab instances survive save/load)
        // ---------------------------------------------------------------

        [Tooltip("The item id this block represents (matches ItemDefinition.itemId).")]
        [SerializeField] private string itemId;

        [Tooltip("Cell coordinates relative to BuildGrid.origin.")]
        [SerializeField] private Vector3Int cell;

        [Tooltip("Block's rotation at placement time (snapped to 90 degree increments around Y for M2 cube+slab forms).")]
        [SerializeField] private Quaternion rotation = Quaternion.identity;

        [Tooltip("Remaining hit points. Reaches 0 -> dropped + unregistered.")]
        [SerializeField] private int healthRemaining = DefaultHealth;

        [Tooltip("Maximum hit points (the value health resets to on a healed/replaced block).")]
        [SerializeField] private int healthMax = DefaultHealth;

        /// <summary>The item id this block represents.</summary>
        public string ItemId => itemId;

        /// <summary>The cell this block occupies (relative to <see cref="BuildGrid"/>.origin).</summary>
        public Vector3Int Cell => cell;

        /// <summary>The block's rotation as placed.</summary>
        public Quaternion Rotation => rotation;

        /// <summary>Remaining HP. Reaches 0 -> the V7.5 break path destroys this GO + drops the item.</summary>
        public int HealthRemaining => healthRemaining;

        /// <summary>Maximum HP for this block instance.</summary>
        public int HealthMax => healthMax;

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>Raised when health changes (mining tick, fire damage, etc).</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Raised when health hits 0 — just before the GO is destroyed.</summary>
        public event Action<PlacedBlock> OnBlockBroken;

        // ---------------------------------------------------------------
        //  Initialization
        // ---------------------------------------------------------------

        /// <summary>
        /// Initialize a freshly instantiated placed-prefab. Called by
        /// <see cref="BlockPlacer"/> right after Instantiate(). Sets the item
        /// id, cell, rotation, and resets HP to <see cref="DefaultHealth"/>.
        /// </summary>
        public void OnPlaced(string id, Vector3Int c, Quaternion r)
        {
            itemId = id;
            cell = c;
            rotation = r;
            healthRemaining = DefaultHealth;
            healthMax = DefaultHealth;
        }

        /// <summary>
        /// Initialize with a custom max HP (testing seam; some forms may
        /// scale HP by material tier in M7).
        /// </summary>
        public void OnPlaced(string id, Vector3Int c, Quaternion r, int maxHp)
        {
            itemId = id;
            cell = c;
            rotation = r;
            int clamped = maxHp > 0 ? maxHp : DefaultHealth;
            healthRemaining = clamped;
            healthMax = clamped;
        }

        // ---------------------------------------------------------------
        //  Damage / Repair
        // ---------------------------------------------------------------

        /// <summary>
        /// Apply <paramref name="amount"/> points of damage. Returns the new
        /// HP. Does NOT auto-destroy the GO on hp==0; the caller (V7.5
        /// <see cref="BlockBreaker"/>) is responsible for invoking
        /// <see cref="DestroyAndDrop"/> so item-drop policy is centralized.
        /// </summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return healthRemaining;
            healthRemaining -= amount;
            if (healthRemaining < 0) healthRemaining = 0;
            OnHealthChanged?.Invoke(healthRemaining, healthMax);
            return healthRemaining;
        }

        /// <summary>
        /// Heal up to <see cref="HealthMax"/>. Used by the (future) repair
        /// tool. No-op when already at full HP.
        /// </summary>
        public int Repair(int amount)
        {
            if (amount <= 0) return healthRemaining;
            healthRemaining += amount;
            if (healthRemaining > healthMax) healthRemaining = healthMax;
            OnHealthChanged?.Invoke(healthRemaining, healthMax);
            return healthRemaining;
        }

        /// <summary>
        /// Fire the broken event (so listeners can VFX / SFX) and return.
        /// Actual GO destruction + drop is owned by <see cref="BlockBreaker"/>
        /// so item-drop logic lives in one place.
        /// </summary>
        public void NotifyBroken()
        {
            OnBlockBroken?.Invoke(this);
        }
    }
}
