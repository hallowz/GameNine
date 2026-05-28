using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.5 — Centralized block-breaking helpers.
    ///
    /// Wraps the "apply damage to a PlacedBlock; on hp<=0 drop the item +
    /// unregister + destroy the GO" sequence so <see cref="Player.PlayerMining"/>
    /// (the V3.3 mining path) can detect a PlacedBlock hit and route through
    /// here instead of through <see cref="World.Chunks.TerrainDeformer"/>.
    ///
    /// For M2: no tool-tier requirement, no drop-table variability — every
    /// block at hp 0 drops one of itself. Tool-tier scaling lands in M7.
    /// </summary>
    /// <remarks>
    /// Coop note: damage application is server-authoritative in V21. The
    /// public methods here are owner-authoritative for M2 and trivially
    /// portable to a ServerRpc when V21 lands.
    /// </remarks>
    public static class BlockBreaker
    {
        /// <summary>
        /// Apply <paramref name="damage"/> to <paramref name="block"/>. If the
        /// block reaches 0 hp, drops one of the block's item at the cell
        /// centre, unregisters from <see cref="BlockRegistry"/>, and destroys
        /// the GameObject. Returns true iff the block was broken.
        /// </summary>
        public static bool ApplyDamage(PlacedBlock block, int damage)
        {
            if (block == null) return false;
            int hp = block.TakeDamage(damage);
            if (hp > 0) return false;
            DestroyAndDrop(block);
            return true;
        }

        /// <summary>
        /// Drop the block's item at its cell, unregister, destroy the GO.
        /// Public so EditMode tests can drive the break path without having
        /// to land an exact integer damage value.
        /// </summary>
        public static void DestroyAndDrop(PlacedBlock block)
        {
            if (block == null) return;

            BlockRegistry registry = BlockRegistry.Instance;
            BuildGrid grid = BuildGrid.Instance;

            // Resolve item def + spawn world drop. ItemDatabase may be unloaded
            // in unit tests; the registry-side work still has to happen, so we
            // gate the drop on a non-null def.
            ItemDefinition def = null;
            if (!string.IsNullOrEmpty(block.ItemId))
            {
                ItemDatabase db = ItemDatabase.GetOrLoad();
                if (db != null) def = db.GetItem(block.ItemId);
            }

            Vector3 dropPos = grid != null
                ? grid.CellToWorld(block.Cell)
                : block.transform.position;

            block.NotifyBroken();

            if (registry != null)
            {
                registry.Unregister(block);
            }

            if (def != null)
            {
                WorldItemSpawner.SpawnItem(new ItemStack(def, 1), dropPos);
            }

            // Destroy the GameObject last so listeners on the block can read
            // its final state in OnBlockBroken.
            if (Application.isPlaying)
            {
                Object.Destroy(block.gameObject);
            }
            else
            {
                Object.DestroyImmediate(block.gameObject);
            }
        }
    }
}
