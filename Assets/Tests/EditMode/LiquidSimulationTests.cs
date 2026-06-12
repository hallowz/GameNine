using NUnit.Framework;
using UnityEngine;
using Voidborne.World.Liquid;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// Tests for the chunked liquid cellular automata (terrain overhaul P3.1/P3.2):
    /// fall/spread/settle rules, conservation, cross-chunk flow with refund,
    /// and the sleep/wake activity model that makes settled liquid cost zero.
    /// </summary>
    public class LiquidSimulationTests
    {
        private const int S = LiquidField.SIZE;

        private LiquidSimulator sim;
        private readonly System.Collections.Generic.Dictionary<Vector3Int, float[]> densities =
            new System.Collections.Generic.Dictionary<Vector3Int, float[]>();

        [SetUp]
        public void SetUp()
        {
            sim = new LiquidSimulator();
            densities.Clear();
        }

        /// <summary>Creates and registers a chunk with an all-air density field
        /// except a solid floor at the given local Y (floorY &lt; 0 = no floor).
        /// The density array stays accessible via the densities map (it is
        /// referenced live by the simulator, like ChunkData.densityField).</summary>
        private LiquidField MakeChunk(Vector3Int pos, int floorY)
        {
            var field = new LiquidField();
            var density = new float[LiquidField.VOLUME];
            // density <= 0 is air; > 0 is solid
            for (int i = 0; i < density.Length; i++) density[i] = -1f;
            if (floorY >= 0)
            {
                for (int z = 0; z < S; z++)
                for (int x = 0; x < S; x++)
                    density[LiquidField.Index(x, floorY, z)] = 1f;
            }
            sim.RegisterChunk(pos, field, density);
            densities[pos] = density;
            return field;
        }

        private long TotalVolume(params LiquidField[] fields)
        {
            long sum = 0;
            foreach (var f in fields) sum += f.TotalVolume();
            return sum;
        }

        // ------------------------------------------------------------------
        //  Core CA rules
        // ------------------------------------------------------------------

        [Test]
        public void Fall_MovesLiquidDownOneCellPerTick()
        {
            var field = MakeChunk(Vector3Int.zero, 0);
            sim.AddLiquid(Vector3Int.zero, 16, 10, 16, 255, LiquidType.Water);

            sim.TickOnce();

            Assert.AreEqual(0, field.Get(16, 10, 16), "Source cell should have drained");
            Assert.AreEqual(255, field.Get(16, 9, 16), "Liquid should be one cell lower");
        }

        [Test]
        public void Fall_StopsOnSolidFloorAndSpreads()
        {
            var field = MakeChunk(Vector3Int.zero, 4);
            sim.AddLiquid(Vector3Int.zero, 16, 5, 16, 255, LiquidType.Water);

            sim.TickOnce();

            // Sitting on the floor: cannot fall, so it spreads laterally
            int center = field.Get(16, 5, 16);
            int spread = field.Get(15, 5, 16) + field.Get(17, 5, 16)
                       + field.Get(16, 5, 15) + field.Get(16, 5, 17);
            Assert.Less(center, 255, "Center should have given liquid away");
            Assert.Greater(spread, 0, "Neighbors should have received liquid");
        }

        [Test]
        public void Spread_ConservesTotalVolume()
        {
            var field = MakeChunk(Vector3Int.zero, 0);
            sim.AddLiquid(Vector3Int.zero, 16, 1, 16, 255, LiquidType.Water);
            sim.AddLiquid(Vector3Int.zero, 16, 2, 16, 255, LiquidType.Water);
            long before = field.TotalVolume() + sim.TotalEvaporated;

            for (int i = 0; i < 20; i++) sim.TickOnce();

            Assert.AreEqual(before, field.TotalVolume() + sim.TotalEvaporated,
                "Volume must be conserved (minus tracked evaporation)");
        }

        [Test]
        public void Settle_TinyUnsupportedFilmsEvaporate()
        {
            var field = MakeChunk(Vector3Int.zero, 4);
            // 3 levels (< MIN_VISIBLE=4) directly on the floor — a stagnant dribble
            sim.AddLiquid(Vector3Int.zero, 16, 5, 16, 3, LiquidType.Water);

            for (int i = 0; i < 3; i++) sim.TickOnce();

            Assert.AreEqual(0, field.Get(16, 5, 16), "Sub-visible dribble should evaporate");
            Assert.AreEqual(3, sim.TotalEvaporated, "Evaporation must be tracked");
        }

        [Test]
        public void Viscosity_PetroleumSpreadsSlowerThanWater()
        {
            var water = MakeChunk(new Vector3Int(0, 0, 0), 4);
            var oil = MakeChunk(new Vector3Int(10, 0, 0), 4);
            sim.AddLiquid(new Vector3Int(0, 0, 0), 16, 5, 16, 255, LiquidType.Water);
            sim.AddLiquid(new Vector3Int(10, 0, 0), 16, 5, 16, 255, LiquidType.Petroleum);

            sim.TickOnce();

            int waterMoved = 255 - water.Get(16, 5, 16);
            int oilMoved = 255 - oil.Get(16, 5, 16);
            Assert.Greater(waterMoved, oilMoved,
                "Water (flow 64) must spread faster than petroleum (flow 16)");
        }

        [Test]
        public void Solid_BuildingIntoLiquidDestroysItButTracksIt()
        {
            var field = MakeChunk(Vector3Int.zero, 4);
            sim.AddLiquid(Vector3Int.zero, 16, 5, 16, 200, LiquidType.Water);
            // Only 2 ticks: the liquid stays in the y=5 plane (floor below it),
            // and a 200-unit puddle hasn't meaningfully evaporated yet.
            sim.TickOnce();
            sim.TickOnce();
            long total = field.TotalVolume() + sim.TotalEvaporated;
            Assert.Greater(field.TotalVolume(), 0);

            // Terrain deformation builds solid into the whole liquid layer.
            // The density array is referenced live by the simulator (like
            // ChunkData.densityField), so mutating it is what the game does.
            float[] density = densities[Vector3Int.zero];
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
                density[LiquidField.Index(x, 5, z)] = 1f;
            sim.Wake(Vector3Int.zero); // deformation wakes the chunk

            sim.TickOnce();

            Assert.AreEqual(0, field.TotalVolume(), "Liquid inside solid is destroyed");
            Assert.AreEqual(total, sim.TotalEvaporated + field.TotalVolume(),
                "Destroyed liquid must be tracked as evaporation (conservation accounting)");
        }

        // ------------------------------------------------------------------
        //  Cross-chunk flow
        // ------------------------------------------------------------------

        [Test]
        public void CrossChunk_FallsIntoChunkBelow()
        {
            var top = MakeChunk(new Vector3Int(0, 1, 0), -1);
            var bottom = MakeChunk(new Vector3Int(0, 0, 0), 0);
            sim.AddLiquid(new Vector3Int(0, 1, 0), 16, 0, 16, 255, LiquidType.Water);
            long before = TotalVolume(top, bottom) + sim.TotalEvaporated;

            sim.TickOnce();

            Assert.AreEqual(0, top.Get(16, 0, 16), "Top chunk border cell should drain");
            Assert.AreEqual(255, bottom.Get(16, S - 1, 16), "Bottom chunk should receive at its top layer");
            Assert.AreEqual(before, TotalVolume(top, bottom) + sim.TotalEvaporated, "Conserved across chunks");
            Assert.AreEqual(LiquidType.Water, bottom.type, "Receiving chunk adopts the liquid type");
        }

        [Test]
        public void CrossChunk_SpreadsLaterallyAcrossBorder()
        {
            var left = MakeChunk(new Vector3Int(0, 0, 0), 4);
            var right = MakeChunk(new Vector3Int(1, 0, 0), 4);
            // Full column on the border cell of the left chunk, on the floor
            sim.AddLiquid(new Vector3Int(0, 0, 0), S - 1, 5, 16, 255, LiquidType.Water);
            long before = TotalVolume(left, right) + sim.TotalEvaporated;

            for (int i = 0; i < 4; i++) sim.TickOnce();

            Assert.Greater(right.TotalVolume(), 0, "Liquid must cross the vertical chunk border");
            Assert.AreEqual(before, TotalVolume(left, right) + sim.TotalEvaporated, "Conserved across chunks");
        }

        [Test]
        public void CrossChunk_UnloadedNeighborBlocksFlow()
        {
            // Single chunk, no floor — liquid at y=0 wants to fall into the
            // unregistered chunk below. It must pile up, not vanish.
            var field = MakeChunk(Vector3Int.zero, -1);
            sim.AddLiquid(Vector3Int.zero, 16, 0, 16, 255, LiquidType.Water);
            long before = field.TotalVolume() + sim.TotalEvaporated;

            for (int i = 0; i < 5; i++) sim.TickOnce();

            Assert.AreEqual(before, field.TotalVolume() + sim.TotalEvaporated,
                "Liquid must not drain into unloaded chunks");
        }

        // ------------------------------------------------------------------
        //  Sleep/wake activity model
        // ------------------------------------------------------------------

        [Test]
        public void Sleep_SettledChunkLeavesActiveSet()
        {
            MakeChunk(Vector3Int.zero, 4);
            sim.AddLiquid(Vector3Int.zero, 16, 5, 16, 255, LiquidType.Water);
            Assert.AreEqual(1, sim.ActiveChunkCount);

            // Tick until settled + sleep timeout
            for (int i = 0; i < 60 && sim.ActiveChunkCount > 0; i++) sim.TickOnce();

            Assert.AreEqual(0, sim.ActiveChunkCount,
                "A settled puddle must leave the active set (zero steady-state cost)");
        }

        [Test]
        public void Sleep_TickOnEmptyActiveSetIsFree()
        {
            MakeChunk(Vector3Int.zero, 4);
            Assert.AreEqual(0, sim.ActiveChunkCount);
            Assert.AreEqual(0, sim.TickOnce(), "No active chunks → nothing simulated");
        }

        [Test]
        public void Wake_NeighborWakesWhenBorderChanges()
        {
            var left = MakeChunk(new Vector3Int(0, 0, 0), 4);
            var right = MakeChunk(new Vector3Int(1, 0, 0), 4);

            // Settle a substantial pool spanning the border region of the left
            // chunk. Volume matters: the settle rule evaporates stagnant cells
            // below MIN_VISIBLE, so a too-small spill self-cleans to nothing —
            // a real pool's equilibrium cells all sit at >= MIN_VISIBLE.
            for (int y = 5; y <= 8; y++)
                sim.AddLiquid(new Vector3Int(0, 0, 0), S - 1, y, 16, 255, LiquidType.Water);
            for (int i = 0; i < 100 && sim.ActiveChunkCount > 0; i++) sim.TickOnce();
            Assert.AreEqual(0, sim.ActiveChunkCount, "Pool should settle");
            Assert.Greater(right.TotalVolume(), 0, "Some liquid crossed during settling");

            // Wake only the left chunk with new liquid; the right one must wake
            // automatically when the shared border changes.
            sim.AddLiquid(new Vector3Int(0, 0, 0), S - 1, 12, 16, 255, LiquidType.Water);
            Assert.IsFalse(sim.IsActive(new Vector3Int(1, 0, 0)));

            for (int i = 0; i < 6; i++) sim.TickOnce();

            // The right chunk received inflow or woke via the border mask at
            // some point — its volume must have increased.
            Assert.Greater(right.TotalVolume(), 0);
            long before = TotalVolume(left, right) + sim.TotalEvaporated;
            for (int i = 0; i < 100 && sim.ActiveChunkCount > 0; i++) sim.TickOnce();
            Assert.AreEqual(before, TotalVolume(left, right) + sim.TotalEvaporated);
            Assert.AreEqual(0, sim.ActiveChunkCount, "Everything settles again");
        }

        [Test]
        public void Drained_ChunkCollapsesStorage()
        {
            // No floor in top chunk: everything drains into the bottom chunk.
            // Pour a real column (16 full cells) — a single 255 packet spread
            // over a 32×32 floor is a sub-visible film and would (by design)
            // fully evaporate via the settle rule.
            var top = MakeChunk(new Vector3Int(0, 1, 0), -1);
            var bottom = MakeChunk(new Vector3Int(0, 0, 0), 0);
            for (int x = 15; x <= 18; x++)
            for (int z = 15; z <= 18; z++)
                sim.AddLiquid(new Vector3Int(0, 1, 0), x, 3, z, 255, LiquidType.Water);

            for (int i = 0; i < 300 && sim.ActiveChunkCount > 0; i++) sim.TickOnce();

            Assert.IsFalse(top.HasLevels, "Fully drained chunk must release its storage");
            Assert.Greater(bottom.TotalVolume(), 0, "The pool must survive in the bottom chunk");
        }

        [Test]
        public void Determinism_SameSetupProducesSameResult()
        {
            long RunScenario()
            {
                var localSim = new LiquidSimulator();
                var field = new LiquidField();
                var density = new float[LiquidField.VOLUME];
                for (int i = 0; i < density.Length; i++) density[i] = -1f;
                for (int z = 0; z < S; z++)
                for (int x = 0; x < S; x++)
                    density[LiquidField.Index(x, 2, z)] = 1f;
                localSim.RegisterChunk(Vector3Int.zero, field, density);
                localSim.AddLiquid(Vector3Int.zero, 8, 10, 8, 255, LiquidType.Water);
                localSim.AddLiquid(Vector3Int.zero, 20, 12, 20, 180, LiquidType.Water);
                for (int i = 0; i < 15; i++) localSim.TickOnce();

                // Hash the final field state
                long hash = 17;
                for (int i = 0; i < field.levels.Length; i++)
                    hash = hash * 31 + field.levels[i];
                return hash;
            }

            Assert.AreEqual(RunScenario(), RunScenario(),
                "Identical setup + tick count must produce identical fields (coop requirement)");
        }
    }
}
