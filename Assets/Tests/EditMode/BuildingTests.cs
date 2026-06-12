#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voidborne.Automation;
using Voidborne.Building;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// V7.1 / V7.2 / V7.3 / V7.5 — Building system EditMode coverage.
    ///
    /// Validates:
    /// <list type="bullet">
    /// <item><description>BuildGrid origin-pin policy + cell round-trip math.</description></item>
    /// <item><description>BlockRegistry register / lookup / double-placement refusal.</description></item>
    /// <item><description>BlockPlacer programmatic placement + Machine-vs-Block discrimination.</description></item>
    /// <item><description>PlacedBlock damage + BlockBreaker drop-and-unregister sequence.</description></item>
    /// <item><description>TerrainLevelTool footprint math (cells modified).</description></item>
    /// </list>
    ///
    /// Tests drive the system programmatically (no Unity input, no PlayMode
    /// loop). The placement / break paths use ItemDatabase fixtures rather
    /// than the project's Resources/ItemDatabase.asset so each test owns its
    /// own data and runs deterministically.
    /// </summary>
    public class BuildingTests
    {
        // ---------------------------------------------------------------
        //  Fixture state
        // ---------------------------------------------------------------

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private GameObject _gridHostGo;
        private GameObject _registryHostGo;
        private GameObject _placerHostGo;
        private GameObject _toolHostGo;
        private ItemDatabase _itemDbFixture;
        private MachineRegistry _machineRegFixture;

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private static GameObject MakeStubPlacedPrefab(string id)
        {
            // Cannot use Resources / .prefab files in pure EditMode; instead,
            // synthesize a runtime GameObject with a BoxCollider so the placer
            // can Instantiate it. NewInstance is a Resource we own so we can
            // DestroyImmediate at TearDown.
            var go = new GameObject(id + "_placed_stub");
            go.AddComponent<BoxCollider>();
            return go;
        }

        private ItemDefinition MakeBuildItem(string id, GameObject placedPrefab)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.maxStackSize = 64;
            item.kind = ItemKind.Product;
            item.isBuildBlock = true;
            item.placedPrefab = placedPrefab;
            _created.Add(item);
            return item;
        }

        private ItemDefinition MakeMachineItem(string id, GameObject placedPrefab)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = id;
            item.maxStackSize = 1;
            item.kind = ItemKind.Machine;
            item.isBuildBlock = false;
            item.placedPrefab = placedPrefab;
            _created.Add(item);
            return item;
        }

        private MachineDefinition MakeMachineDef(string id)
        {
            var def = ScriptableObject.CreateInstance<MachineDefinition>();
            def.itemId = id;
            def.displayName = id;
            def.gridWidth = 3;
            def.gridHeight = 3;
            def.processType = MachineProcessType.Forgiving_Thermal_DryBurn;
            _created.Add(def);
            return def;
        }

        private void InstallItemDb(IEnumerable<ItemDefinition> items)
        {
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition>(items);
            _itemDbFixture.Reindex();
            _created.Add(_itemDbFixture);
        }

        private void InstallMachineRegistry(IEnumerable<MachineDefinition> defs)
        {
            _machineRegFixture = ScriptableObject.CreateInstance<MachineRegistry>();
            _machineRegFixture.allMachines = new List<MachineDefinition>(defs);
            _machineRegFixture.Reindex();
            _created.Add(_machineRegFixture);
        }

        private BuildGrid CreateGrid()
        {
            BuildGrid.ResetInstance();
            _gridHostGo = new GameObject("BuildGrid_TestHost");
            return _gridHostGo.AddComponent<BuildGrid>();
        }

        private BlockRegistry CreateRegistry()
        {
            BlockRegistry.ResetInstance();
            _registryHostGo = new GameObject("BlockRegistry_TestHost");
            return _registryHostGo.AddComponent<BlockRegistry>();
        }

        private BlockPlacer CreatePlacer()
        {
            _placerHostGo = new GameObject("BlockPlacer_TestHost");
            return _placerHostGo.AddComponent<BlockPlacer>();
        }

        private TerrainLevelTool CreateLevelTool()
        {
            _toolHostGo = new GameObject("TerrainLevelTool_TestHost");
            return _toolHostGo.AddComponent<TerrainLevelTool>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gridHostGo != null) { UnityEngine.Object.DestroyImmediate(_gridHostGo); _gridHostGo = null; }
            if (_registryHostGo != null) { UnityEngine.Object.DestroyImmediate(_registryHostGo); _registryHostGo = null; }
            if (_placerHostGo != null) { UnityEngine.Object.DestroyImmediate(_placerHostGo); _placerHostGo = null; }
            if (_toolHostGo != null) { UnityEngine.Object.DestroyImmediate(_toolHostGo); _toolHostGo = null; }
            BuildGrid.ResetInstance();
            BlockRegistry.ResetInstance();

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ===============================================================
        //  V7.1 — BuildGrid
        // ===============================================================

        [Test]
        public void BuildGrid_FirstBlockSetsOrigin()
        {
            var grid = CreateGrid();
            Assert.IsFalse(grid.IsOriginSet, "Fresh grid should have no origin pinned.");

            Vector3 firstBlockPos = new Vector3(10.3f, 5.7f, 8.1f);
            grid.SetOriginIfUnset(firstBlockPos);

            Assert.IsTrue(grid.IsOriginSet, "Origin should be pinned after first call.");
            // Origin must be cell-aligned (1m cells -> floor to integer world coords).
            Assert.AreEqual(10f, grid.Origin.x, 1e-4f, "Origin x must snap to floor.");
            Assert.AreEqual(5f, grid.Origin.y, 1e-4f, "Origin y must snap to floor.");
            Assert.AreEqual(8f, grid.Origin.z, 1e-4f, "Origin z must snap to floor.");

            // Idempotent: a second call must not move the origin.
            grid.SetOriginIfUnset(new Vector3(100f, 100f, 100f));
            Assert.AreEqual(10f, grid.Origin.x, 1e-4f, "Origin must not move on second pin.");
        }

        [Test]
        public void BuildGrid_WorldToCellAndBack()
        {
            var grid = CreateGrid();
            grid.SetOriginIfUnset(new Vector3(10f, 5f, 8f));

            // A point inside cell (3, 2, 4) relative to the origin
            // -> world = (10 + 3 + 0.3, 5 + 2 + 0.6, 8 + 4 + 0.9) = (13.3, 7.6, 12.9)
            Vector3 worldPos = new Vector3(13.3f, 7.6f, 12.9f);
            Vector3Int cell = grid.WorldToCell(worldPos);
            Assert.AreEqual(new Vector3Int(3, 2, 4), cell, "World->cell must compute relative-floor cell.");

            // CellToWorld returns the cell's centre — re-mapping that centre
            // back via WorldToCell must land on the same cell.
            Vector3 centre = grid.CellToWorld(cell);
            Vector3Int roundTrip = grid.WorldToCell(centre);
            Assert.AreEqual(cell, roundTrip, "WorldToCell(CellToWorld(c)) must equal c.");
        }

        // ===============================================================
        //  V7.1 — BlockRegistry
        // ===============================================================

        [Test]
        public void BlockRegistry_RegisterAndLookup()
        {
            var registry = CreateRegistry();
            var blockGo = new GameObject("TestBlock");
            var block = blockGo.AddComponent<PlacedBlock>();
            block.OnPlaced("wood_cube", new Vector3Int(5, 2, 3), Quaternion.identity);

            bool ok = registry.Register(block);
            Assert.IsTrue(ok, "Register must succeed for an empty cell.");
            Assert.AreEqual(1, registry.Count, "Registry should contain exactly one block.");
            Assert.IsTrue(registry.IsCellOccupied(new Vector3Int(5, 2, 3)), "Cell must be reported occupied.");
            Assert.AreSame(block, registry.GetAt(new Vector3Int(5, 2, 3)), "GetAt must return the registered block.");

            UnityEngine.Object.DestroyImmediate(blockGo);
        }

        [Test]
        public void BlockRegistry_DoesNotAllowDoublePlacement()
        {
            var registry = CreateRegistry();
            var go1 = new GameObject("Block1");
            var b1 = go1.AddComponent<PlacedBlock>();
            b1.OnPlaced("wood_cube", new Vector3Int(1, 0, 0), Quaternion.identity);
            registry.Register(b1);

            var go2 = new GameObject("Block2");
            var b2 = go2.AddComponent<PlacedBlock>();
            b2.OnPlaced("stone_cube", new Vector3Int(1, 0, 0), Quaternion.identity);
            // Suppress the expected warning from BlockRegistry.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*already occupied.*"));
            bool ok = registry.Register(b2);

            Assert.IsFalse(ok, "Double-placement at an occupied cell must be refused.");
            Assert.AreSame(b1, registry.GetAt(new Vector3Int(1, 0, 0)), "First block must remain at the cell.");
            Assert.AreEqual(1, registry.Count, "Registry must contain exactly one block after a refused dup.");

            UnityEngine.Object.DestroyImmediate(go1);
            UnityEngine.Object.DestroyImmediate(go2);
        }

        // ===============================================================
        //  V7.1 / V7.2 — BlockPlacer
        // ===============================================================

        [Test]
        public void BlockPlacer_PlacesBlockOnLeftClick()
        {
            // Programmatic placement path: BlockPlacer.TryPlace is the test
            // seam for "what left-click does" without simulating Mouse input.
            var prefab = MakeStubPlacedPrefab("wood_cube");
            _created.Add(prefab);
            var def = MakeBuildItem("wood_cube", prefab);
            InstallItemDb(new[] { def });

            var grid = CreateGrid();
            var registry = CreateRegistry();
            var placer = CreatePlacer();

            // No inventory wired in this fixture; BlockPlacer.TryPlace will
            // still place (it short-circuits the consume step when inventory
            // is null), so we cover the spawn-and-register surface here.
            Vector3Int targetCell = new Vector3Int(0, 0, 0);
            Vector3 placePos = grid.CellToWorld(targetCell);
            PlacedBlock placed = placer.TryPlace(def, targetCell, placePos);

            Assert.IsNotNull(placed, "TryPlace must return a PlacedBlock.");
            Assert.AreEqual("wood_cube", placed.ItemId, "PlacedBlock must carry the item id.");
            Assert.IsTrue(registry.IsCellOccupied(targetCell), "Registry must reflect the new placement.");
            Assert.AreEqual(1, registry.Count);

            UnityEngine.Object.DestroyImmediate(placed.gameObject);
        }

        [Test]
        public void BlockPlacer_AttachesMachineCraftingStationForMachineItems()
        {
            // V6.4 review heads-up: Machine items must get a
            // MachineCraftingStation attached on placement; build blocks
            // must NOT.
            var prefab = MakeStubPlacedPrefab("workbench");
            _created.Add(prefab);
            var def = MakeMachineItem("workbench", prefab);
            var mdef = MakeMachineDef("workbench");
            InstallItemDb(new[] { def });
            InstallMachineRegistry(new[] { mdef });

            CreateGrid();
            CreateRegistry();
            var placer = CreatePlacer();

            PlacedBlock placed = placer.TryPlace(def, new Vector3Int(0, 0, 0), Vector3.zero);
            Assert.IsNotNull(placed, "Machine placement must succeed.");

            MachineCraftingStation station = placed.GetComponent<MachineCraftingStation>();
            Assert.IsNotNull(station, "Machine item must spawn with MachineCraftingStation attached.");
            Assert.AreSame(mdef, station.Machine,
                "Station must be Init'd with the matching MachineDefinition.");

            UnityEngine.Object.DestroyImmediate(placed.gameObject);
        }

        [Test]
        public void BlockPlacer_BuildBlockDoesNotGetMachineStation()
        {
            // The discriminator goes the other way too: a build block must
            // NOT have a MachineCraftingStation attached. Otherwise inert
            // wood cubes would surface as crafting UIs.
            var prefab = MakeStubPlacedPrefab("wood_cube");
            _created.Add(prefab);
            var def = MakeBuildItem("wood_cube", prefab);
            InstallItemDb(new[] { def });
            // Install an empty machine registry so the lookup is well-defined.
            InstallMachineRegistry(System.Array.Empty<MachineDefinition>());

            CreateGrid();
            CreateRegistry();
            var placer = CreatePlacer();

            PlacedBlock placed = placer.TryPlace(def, new Vector3Int(0, 0, 0), Vector3.zero);
            Assert.IsNotNull(placed);
            Assert.IsNull(placed.GetComponent<MachineCraftingStation>(),
                "Build blocks must not have MachineCraftingStation attached.");

            UnityEngine.Object.DestroyImmediate(placed.gameObject);
        }

        [Test]
        public void BlockPlacer_DoesNotPlaceInsideExistingBlock()
        {
            var prefab = MakeStubPlacedPrefab("wood_cube");
            _created.Add(prefab);
            var def = MakeBuildItem("wood_cube", prefab);
            InstallItemDb(new[] { def });

            CreateGrid();
            var registry = CreateRegistry();
            var placer = CreatePlacer();

            PlacedBlock first = placer.TryPlace(def, new Vector3Int(0, 0, 0), Vector3.zero);
            Assert.IsNotNull(first);

            PlacedBlock dup = placer.TryPlace(def, new Vector3Int(0, 0, 0), Vector3.zero);
            Assert.IsNull(dup, "Second placement at an occupied cell must return null.");
            Assert.AreEqual(1, registry.Count, "Registry must still contain only the original block.");

            UnityEngine.Object.DestroyImmediate(first.gameObject);
        }

        // ===============================================================
        //  V7.5 — Block damage / break + drop
        // ===============================================================

        [Test]
        public void Block_DamageReducesHealth()
        {
            var go = new GameObject("DamageBlock");
            var block = go.AddComponent<PlacedBlock>();
            block.OnPlaced("wood_cube", Vector3Int.zero, Quaternion.identity);

            Assert.AreEqual(PlacedBlock.DefaultHealth, block.HealthRemaining,
                "Default HP should be DefaultHealth on placement.");
            int hp = block.TakeDamage(50);
            Assert.AreEqual(PlacedBlock.DefaultHealth - 50, hp,
                "TakeDamage must return the new HP and reduce HealthRemaining by 'amount'.");
            Assert.AreEqual(PlacedBlock.DefaultHealth - 50, block.HealthRemaining);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Block_AtZeroHealthDropsItemAndUnregisters()
        {
            // Wire fixtures: item db w/ wood_cube + a registered block at (0,0,0).
            var prefab = MakeStubPlacedPrefab("wood_cube");
            _created.Add(prefab);
            var def = MakeBuildItem("wood_cube", prefab);
            InstallItemDb(new[] { def });

            CreateGrid();
            var registry = CreateRegistry();

            // Instantiate the block manually (matches what BlockPlacer would do).
            var instance = UnityEngine.Object.Instantiate(prefab);
            var block = instance.AddComponent<PlacedBlock>();
            block.OnPlaced("wood_cube", new Vector3Int(0, 0, 0), Quaternion.identity);
            registry.Register(block);

            int countBefore = CountWorldItemsByItemId("wood_cube");

            // Apply damage that exceeds max HP.
            bool broken = BlockBreaker.ApplyDamage(block, PlacedBlock.DefaultHealth + 50);
            Assert.IsTrue(broken, "Damage exceeding HP must return broken=true.");

            Assert.IsFalse(registry.IsCellOccupied(new Vector3Int(0, 0, 0)),
                "Cell must be unregistered after the break.");

            int countAfter = CountWorldItemsByItemId("wood_cube");
            Assert.AreEqual(countBefore + 1, countAfter,
                "Breaking a wood_cube must drop exactly one wood_cube WorldItem.");

            CleanupSpawnedWorldItems();
        }

        // ===============================================================
        //  V7.3 — TerrainLevelTool
        // ===============================================================

        [Test]
        public void TerrainLevelTool_ModifiesVoxelsInRadius()
        {
            var tool = CreateLevelTool();
            // ApplyAt(radius=3) must visit every cell in a 7x7 footprint
            // whose square distance to (0,0) is <= 9. That count is the
            // number of integer (dx,dz) pairs in [-3..3]^2 with dx^2+dz^2<=9.
            int cells = tool.ApplyAt(new Vector3(0f, 0f, 0f), 3);

            // Expected count: hand-counted disk for radius 3.
            int expected = 0;
            for (int dz = -3; dz <= 3; dz++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    if (dx * dx + dz * dz <= 9) expected++;
                }
            }
            Assert.AreEqual(expected, cells,
                "TerrainLevelTool must visit every cell in the configured radius (disk-shaped footprint).");
            Assert.Greater(cells, 0, "Cell count must be > 0 for any non-degenerate radius.");
        }

        // ---------------------------------------------------------------
        //  Helpers — WorldItem assertions
        // ---------------------------------------------------------------

        private static int CountWorldItemsByItemId(string id)
        {
            int n = 0;
            foreach (var wi in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            {
                if (wi == null) continue;
                if (wi.itemStack.IsEmpty) continue;
                if (wi.itemStack.item == null) continue;
                if (wi.itemStack.item.itemId == id) n++;
            }
            return n;
        }

        private static void CleanupSpawnedWorldItems()
        {
            foreach (var wi in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            {
                if (wi != null) UnityEngine.Object.DestroyImmediate(wi.gameObject);
            }
        }
    }
}
#endif
