using UnityEngine;

namespace Voidborne.Building
{
    /// <summary>
    /// V7.2 — Block form classification (cube / slab / panel / stairs / door).
    ///
    /// The V3.3 item visual pipeline emits one <see cref="ItemDefinition.placedPrefab"/>
    /// per build block with the form-specific mesh already baked in (slab is
    /// already half-height geometry, panel is already thin-wall geometry, etc).
    /// This resolver does NOT scale prefabs — it tells <see cref="BlockPlacer"/>
    /// how to snap the cell's world-space position so the placed visual ends up
    /// on the correct part of the cell (slab at the bottom half, panel on an
    /// edge, etc).
    ///
    /// For M2 scope, only Cube + Slab + Panel + Door are recognized — stairs
    /// and the rest of the 17×5 = 85 block matrix lands in M7 Expansion 1.
    /// </summary>
    public static class BlockFormResolver
    {
        public enum Form
        {
            Cube,
            Slab,
            Panel,
            Door,
            Stairs,
            Unknown
        }

        /// <summary>
        /// Resolve the form for a build-tagged item. Reads the id suffix
        /// (e.g. <c>_slab</c>, <c>_panel</c>, <c>_door</c>) since the V2.2
        /// schema does not yet carry a structured "form" field — the design
        /// note (master_prompt V7.2) says forms are read from the role string;
        /// for M2 the id suffix is the authoritative signal.
        /// </summary>
        public static Form Resolve(ItemDefinition def)
        {
            if (def == null) return Form.Unknown;
            return ResolveFromId(def.itemId);
        }

        /// <summary>Same as <see cref="Resolve(ItemDefinition)"/> but takes the raw id.</summary>
        public static Form ResolveFromId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Form.Unknown;
            // Order matters: check the most specific suffixes first.
            if (itemId.EndsWith("_door")) return Form.Door;
            if (itemId.EndsWith("_slab")) return Form.Slab;
            if (itemId.EndsWith("_panel")) return Form.Panel;
            if (itemId.EndsWith("_stairs")) return Form.Stairs;
            if (itemId.EndsWith("_cube")) return Form.Cube;
            return Form.Cube; // sensible default for unknown build-tagged items
        }

        /// <summary>
        /// Returns the world-space placement position for a block of the
        /// given form at the given cell. For:
        /// <list type="bullet">
        /// <item><description>Cube: cell centre.</description></item>
        /// <item><description>Slab: bottom-half cell centre (y offset = -0.25 of cellSize).</description></item>
        /// <item><description>Panel: cell centre (the prefab's geometry is already thin).</description></item>
        /// <item><description>Door: cell centre at floor level (y offset 0 from cell bottom).</description></item>
        /// </list>
        /// </summary>
        public static Vector3 GetPlacementPosition(BuildGrid grid, Vector3Int cell, Form form)
        {
            if (grid == null) return Vector3.zero;
            float cs = grid.CellSize;
            Vector3 centre = grid.CellToWorld(cell);
            switch (form)
            {
                case Form.Slab:
                    // Sit on the bottom half of the cell; the slab prefab is
                    // half-height with its origin at the BOTTOM, so we drop
                    // its origin to the cell floor.
                    return centre + new Vector3(0f, -cs * 0.5f, 0f);
                case Form.Door:
                    // Door pivot lives at the floor of the cell (hinge bottom).
                    return centre + new Vector3(0f, -cs * 0.5f, 0f);
                case Form.Panel:
                case Form.Cube:
                case Form.Stairs:
                case Form.Unknown:
                default:
                    return centre;
            }
        }
    }
}
