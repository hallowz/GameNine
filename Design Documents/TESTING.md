# Voidborne Testing Guide

This document covers all testing infrastructure for the Voidborne project (GameNine), including NUnit unit tests, PlayMode integration tests, and the custom runtime automated test runner.

---

## 1. Test Architecture Overview

Voidborne uses three categories of tests:

**NUnit EditMode Tests** run inside the Unity Editor without entering Play mode. They are ideal for pure logic validation -- math functions, data structures, coordinate conversions, and anything that does not depend on MonoBehaviour lifecycle callbacks or the Unity runtime loop.

**NUnit PlayMode Tests** run inside a simulated Unity runtime. The test runner enters Play mode, instantiates GameObjects, and advances frames. These tests validate component interactions, physics-dependent behavior, and systems that rely on Start/Update/Coroutine execution.

**Runtime Automated Tests** use a dedicated scene (`AutomatedTestScene`) with a custom `AutomatedTestRunner` MonoBehaviour. These tests exercise full end-to-end behavior in a live game environment -- terrain generation, player movement with real physics, inventory manipulation through the actual PlayerInventory component, and more. They are not part of the NUnit framework and run independently.

---

## 2. Test File Locations

```
Assets/Tests/EditMode/
  ├── EditModeTests.asmdef
  ├── ChunkCoordTests.cs            (23 tests - coordinate conversion)
  ├── InventoryTests.cs              (45 tests - Inventory, ItemStack)
  └── TerrainGenerationTests.cs      (47 tests - DensityFunction, ChunkData)

Assets/Tests/PlayMode/
  ├── PlayModeTests.asmdef
  ├── PlayerMovementTests.cs         (7 tests  - FirstPersonController)
  ├── CombatSystemTests.cs           (6 tests  - DamageInfo, EnemyEntity)
  ├── WeaponSystemTests.cs           (7 tests  - GunInstance, WeaponSwitcher)
  ├── InventoryManagementTests.cs    (16 tests - PlayerInventory)
  └── QuestSystemTests.cs            (12 tests - QuestManager)
```

Both `.asmdef` files reference the main Voidborne assembly via GUID so that test code can access game scripts directly.

---

## 3. Running Tests

### Via Unity Test Runner UI

1. Open **Window > General > Test Runner**.
2. Select the **EditMode** or **PlayMode** tab.
3. Click **Run All** to execute every test in that category, or expand the tree and select individual tests or test classes.
4. Results appear inline: green for passed, red for failed, yellow for skipped/inconclusive.

### Via Claude Code MCP

The Unity MCP integration exposes two tools for running NUnit tests programmatically:

**`run_tests`** -- Starts a test run asynchronously and returns a `job_id`.

| Parameter | Description |
|-----------|-------------|
| `mode` | `"EditMode"` or `"PlayMode"` |
| `test_names` | Optional list of fully qualified test names to run |
| `assembly_names` | Optional list of assembly names to filter (e.g., `["EditModeTests"]`) |
| `category_names` | Optional list of NUnit category attributes to filter |
| `include_failed_tests` | Set to `true` to include failure details in the result |

Example -- run all EditMode tests:
```
run_tests  mode="EditMode"
```

Example -- run a specific test class:
```
run_tests  mode="PlayMode"  test_names=["CombatSystemTests.DamageReducedByArmor"]
```

**`get_test_job`** -- Polls the status of a running test job.

| Parameter | Description |
|-----------|-------------|
| `job_id` | The ID returned by `run_tests` |
| `wait_timeout` | Optional seconds to wait before returning (reduces polling; recommended: 30-60) |
| `include_failed_tests` | Set to `true` to see failure messages and stack traces |
| `include_details` | Set to `true` to see results for every individual test |

Typical workflow:
1. Call `run_tests` with the desired mode and filters.
2. Call `get_test_job` with the returned `job_id` and `wait_timeout: 60`.
3. If the job is still running, call `get_test_job` again.
4. Inspect the results. Use `include_failed_tests: true` to diagnose failures.

### Via Command Line

Run EditMode tests in batch mode (no GUI):
```
Unity.exe -batchmode -runTests -testPlatform EditMode -projectPath <path>
```

Run PlayMode tests in batch mode:
```
Unity.exe -batchmode -runTests -testPlatform PlayMode -projectPath <path>
```

Add `-testResults <output.xml>` to write NUnit XML results to a file.

---

## 4. Runtime Automated Tests

### Overview

The runtime test system lives outside of NUnit. It uses a dedicated Unity scene and a custom test runner script that executes coroutine-based tests in a live game environment.

- **Scene:** `Assets/Scenes/AutomatedTestScene.unity`
- **Runner:** The scene contains a `TestRunner` GameObject with `TestRunnerUI` and `AutomatedTestRunner` components attached.

### How to Run

1. Open `AutomatedTestScene` in the Unity Editor.
2. Enter Play mode.
3. Tests begin executing automatically on scene start.
4. Press **F9** to toggle the test overlay UI.
5. Results are displayed in both the overlay panel and the Unity Console, prefixed with `[AUTOTEST]`.

### Runtime Test List

| Test | What It Validates |
|------|-------------------|
| TerrainGeneration | Chunks generate around the player position; resulting meshes contain vertices |
| PlayerMovement | CharacterController.Move displaces the player transform position |
| InventoryAddRemove | Items can be added to and removed from PlayerInventory with correct count tracking |
| EnemySpawnAndDamage | Enemy health value decreases after a TakeDamage call |
| WeaponEquip | WeaponSwitcher component is present and active slot index is within valid range |
| BuildingConstants | GridUnit equals 3, WallHeight equals 3, SnapDistance equals 0.15 |

Each runtime test has a 10-second timeout. If a test does not complete within that window it is marked as failed.

---

## 5. Using Screenshots for Verification

Claude Code can capture visual output from the Unity Editor via MCP camera tools. This is useful for verifying that tests produce the expected visual state.

**Take a single screenshot:**
```
manage_camera  action="screenshot"  include_image=true
```

**Take a 6-angle contact sheet:**
```
manage_camera  action="screenshot_multiview"
```
This captures front, back, left, right, top, and bottom views stitched into a single image.

**Positioned capture aimed at a target:**
```
manage_camera  action="screenshot"  include_image=true  look_at="Player"  view_position=[0, 20, 0]
```

### Example Verification Workflow

1. Run PlayMode or runtime tests.
2. After tests complete (while still in Play mode), take a screenshot.
3. Verify visual output: terrain meshes rendered, UI elements visible, enemies spawned at expected locations.
4. Use `screenshot_multiview` to inspect the scene from all angles if spatial correctness matters.

This is particularly useful for validating terrain generation, enemy spawn placement, and building system alignment after test runs.

---

## 6. Adding New Tests

### Adding NUnit EditMode Tests

1. Create a new `.cs` file in `Assets/Tests/EditMode/`.
2. Use the namespace `Voidborne.Tests.EditMode`.
3. Add `using NUnit.Framework;` at the top.
4. Mark test methods with the `[Test]` attribute.
5. The `EditModeTests.asmdef` already references the Voidborne assembly, so game scripts are accessible.

```csharp
namespace Voidborne.Tests.EditMode
{
    public class MyNewTests
    {
        [Test]
        public void ExampleCalculation_ReturnsExpectedValue()
        {
            var result = SomeSystem.Calculate(10);
            Assert.AreEqual(42, result);
        }
    }
}
```

### Adding NUnit PlayMode Tests

1. Create a new `.cs` file in `Assets/Tests/PlayMode/`.
2. Use the namespace `Voidborne.Tests.PlayMode`.
3. Add `using NUnit.Framework;` and `using UnityEngine.TestTools;`.
4. Use `[Test]` for synchronous tests or `[UnityTest]` for tests that need frame advancement (returns `IEnumerator`).
5. Use `[UnitySetUp]` and `[UnityTearDown]` for setup/teardown that requires yielding frames.

```csharp
namespace Voidborne.Tests.PlayMode
{
    public class MyPlayModeTests
    {
        private GameObject _testObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _testObject = new GameObject("TestObject");
            _testObject.AddComponent<MyComponent>();
            yield return null; // Advance one frame for Awake/Start
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_testObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MyComponent_InitializesCorrectly()
        {
            var comp = _testObject.GetComponent<MyComponent>();
            Assert.IsNotNull(comp);
            Assert.IsTrue(comp.IsReady);
            yield return null;
        }
    }
}
```

### Adding Runtime Automated Tests

1. Open `AutomatedTestRunner.cs`.
2. Write a new test as a coroutine method that returns `IEnumerator`.
3. Register the test in the `RegisterTests()` method by adding a line:
   ```csharp
   _tests.Add(("MyNewTest", MyNewTestCoroutine()));
   ```
4. Use the built-in assertion helpers for validation:
   - `Assert(bool condition, string message)` -- fails if condition is false.
   - `AssertEqual(object expected, object actual, string message)` -- fails if values differ.
   - `AssertNotNull(object obj, string message)` -- fails if the object is null.

```csharp
private IEnumerator MyNewTestCoroutine()
{
    var obj = GameObject.Find("SomeObject");
    AssertNotNull(obj, "SomeObject should exist in the scene");
    yield return new WaitForSeconds(1f);
    var comp = obj.GetComponent<SomeComponent>();
    Assert(comp.IsActive, "SomeComponent should be active after 1 second");
}
```

---

## 7. Test Coverage Summary

| System | EditMode | PlayMode | Runtime | Notes |
|--------|----------|----------|---------|-------|
| Inventory (core) | Yes (45) | Yes (16) | Yes | Full CRUD coverage |
| Terrain / Chunks | Yes (47) | -- | Yes | Density functions, coord conversion, generation |
| Combat / Damage | -- | Yes (6) | -- | DamageInfo, enemy health reduction |
| Weapons | Yes (26) | Yes (7) | Yes | GunInstance, WeaponSwitcher |
| Player Movement | -- | Yes (7) | Yes | Stamina, speed, grounded state |
| Quest System | -- | Yes (12) | -- | Accept, complete, prerequisites |
| Building | -- | -- | Yes | Constants validation only |
| Automation | -- | -- | -- | Not yet covered |
| Vehicles | -- | -- | -- | Not yet covered |
| Crafting | -- | -- | -- | Not yet covered |
| UI | -- | -- | -- | Not yet covered |
| Electricity | -- | -- | -- | Not yet covered |

Systems marked with dashes have no test coverage in that category. Priority candidates for new tests are Automation (belts, sorters, drives), Crafting, and UI.

---

## 8. Best Practices

**Test data creation.** Use `ScriptableObject.CreateInstance<T>()` to create test instances of ScriptableObjects (e.g., ItemDefinition, QuestData). Do not load assets from disk in EditMode tests.

**GameObject cleanup.** Always destroy any GameObjects you create during a test. Use `[TearDown]` (EditMode) or `[UnityTearDown]` (PlayMode) to ensure cleanup runs even if the test fails.

**Test independence.** Tests must not depend on execution order or shared mutable state. Each test should set up its own preconditions and clean up after itself.

**Frame advancement.** In PlayMode tests, yield at least one frame (`yield return null`) after creating GameObjects so that `Awake()` and `Start()` execute before assertions.

**Timeouts.** Runtime automated tests enforce a 10-second timeout per test. Keep runtime tests focused on a single behavior to stay within this limit.

**Console log prefixes.** NUnit tests should use `[TEST]` as a log prefix when writing to the console. Runtime automated tests use `[AUTOTEST]`. This makes filtering and searching logs straightforward.

**Assertions over Debug.Log.** Prefer NUnit assertions (`Assert.AreEqual`, `Assert.IsTrue`, etc.) over manual Debug.Log checks. Assertions produce clear pass/fail results in the test runner.

**Avoid testing Unity internals.** Do not write tests that validate Unity engine behavior (e.g., verifying that `Transform.position` works). Test your game logic, not the engine.

**PlayMode setup cost.** PlayMode tests are slower because they enter Play mode. Batch related assertions into a single `[UnityTest]` when the setup is expensive, but keep logical test cases separate when setup is cheap.
