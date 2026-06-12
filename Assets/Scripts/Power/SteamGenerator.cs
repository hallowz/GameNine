using UnityEngine;
using Voidborne.Automation;
using Voidborne.Building;
using Voidborne.Core;

namespace Voidborne.Power
{
    /// <summary>
    /// V8.2 — Steam Generator. Picky_Specialty machine in the Core 60
    /// (<c>steam_generator</c>). Outputs
    /// <see cref="GameConstants.Power.SteamGeneratorOutputWatts"/> watts
    /// (100W) when an adjacent <c>steam_boiler</c> placed block is found
    /// AND that boiler is actively running a recipe (its
    /// <see cref="MachineCraftingStation.IsRunning"/> is true). Outputs 0W
    /// otherwise.
    ///
    /// <para>Adjacency is resolved via <see cref="BlockRegistry"/>: this
    /// generator's transform is rounded to a cell, and the six axis-aligned
    /// neighbours are probed for a <c>steam_boiler</c>. This is good enough
    /// for M2 minimum-viable; M7 polish can extend to facing-aware steam
    /// piping.</para>
    /// </summary>
    /// <remarks>
    /// Auto-attached by <see cref="Voidborne.Building.BlockPlacer"/> when
    /// it places a <c>steam_generator</c> item (the placedPrefab grows a
    /// SteamGenerator component on first Init; see <see cref="BlockPlacer.AttachPowerNodeIfPowerItem"/>
    /// in V7.1 / V8.3).
    ///
    /// Test seam <see cref="BoilerOverride"/> lets EditMode tests bypass
    /// the BlockRegistry scan and drive the active/inactive state directly.
    /// </remarks>
    public class SteamGenerator : PowerGenerator
    {
        // ---------------------------------------------------------------
        //  Inspector
        // ---------------------------------------------------------------

        [Tooltip("Search radius (in BuildGrid cells) for an adjacent steam_boiler. Default 1 (six-neighbour).")]
        [SerializeField] private int adjacencyRadius = 1;

        /// <summary>
        /// EditMode test seam: when set, this is used instead of the
        /// BlockRegistry adjacency scan. <c>true</c> means "boiler is hot
        /// and producing steam"; <c>false</c> means "no boiler".
        /// </summary>
        public bool? BoilerOverride { get; set; }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        protected override void InitializeRole()
        {
            maxOutputWatts = GameConstants.Power.SteamGeneratorOutputWatts;
            base.InitializeRole();
        }

        // ---------------------------------------------------------------
        //  Gen tick
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns 100W when an adjacent steam_boiler is actively running;
        /// 0W otherwise. EditMode tests can shortcut via <see cref="BoilerOverride"/>.
        /// </summary>
        public override int GetTickOutputWatts()
        {
            if (IsBoilerActive()) return maxOutputWatts;
            return 0;
        }

        // ---------------------------------------------------------------
        //  Adjacency probe
        // ---------------------------------------------------------------

        /// <summary>True if an adjacent steam_boiler block is registered AND actively crafting.</summary>
        public bool IsBoilerActive()
        {
            if (BoilerOverride.HasValue) return BoilerOverride.Value;

            BlockRegistry reg = BlockRegistry.Instance;
            if (reg == null) return false;

            // Cell-rounded position of the generator. We don't require a
            // BuildGrid pin here because absolute world-cell rounding is
            // sufficient for adjacency: as long as both blocks are placed
            // through the same grid, their cells share an origin offset.
            Vector3 p = transform.position;
            int cx = Mathf.RoundToInt(p.x);
            int cy = Mathf.RoundToInt(p.y);
            int cz = Mathf.RoundToInt(p.z);

            // Six axis-aligned neighbours (radius 1; configurable via
            // adjacencyRadius for M7 extension).
            int r = Mathf.Max(1, adjacencyRadius);
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            for (int dz = -r; dz <= r; dz++)
            {
                // Skip the centre cell (this generator's own cell).
                if (dx == 0 && dy == 0 && dz == 0) continue;
                // Manhattan distance <= r keeps us to axis-aligned neighbours
                // when r == 1.
                if (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz) > r) continue;

                Vector3Int probe = new Vector3Int(cx + dx, cy + dy, cz + dz);
                var block = reg.GetAt(probe);
                if (block != null && block.ItemId == "steam_boiler")
                {
                    // Boiler exists; is it active?
                    var station = block.GetComponent<MachineCraftingStation>();
                    if (station == null) return true; // No station -- treat boiler as "always producing" for M2 fallback.
                    if (station.IsRunning) return true;
                }
            }

            return false;
        }
    }
}
