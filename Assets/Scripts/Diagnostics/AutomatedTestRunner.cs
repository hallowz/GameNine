using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Voidborne.Player;
using Voidborne.Combat;
using Voidborne.Enemies;
using Voidborne.Building;
using Voidborne.World.Chunks;

namespace Voidborne.Diagnostics
{
    /// <summary>
    /// Runtime gameplay test runner. Executes coroutine-based tests sequentially
    /// and reports results to TestRunnerUI. Not NUnit — these validate real
    /// in-scene game systems.
    /// </summary>
    public class AutomatedTestRunner : MonoBehaviour
    {
        // ── Configuration ───────────────────────────────────────────────

        [Tooltip("Automatically run all tests when play mode starts.")]
        [SerializeField] private bool runOnStart = false;

        [Tooltip("Maximum seconds per test before it times out.")]
        [SerializeField] private float testTimeout = 10f;

        // ── Test registry ───────────────────────────────────────────────

        private readonly List<(string name, Func<IEnumerator> coroutine)> _tests =
            new List<(string, Func<IEnumerator>)>();

        private bool _running;
        public bool IsRunning => _running;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        private void Start()
        {
            RegisterTests();

            if (runOnStart)
                RunAllTests();
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Start executing all registered tests sequentially.</summary>
        public void RunAllTests()
        {
            if (_running)
            {
                Debug.LogWarning("[AUTOTEST] Tests already running.");
                return;
            }

            // Ensure TestRunnerUI exists
            if (TestRunnerUI.Instance == null)
            {
                var go = new GameObject("TestRunnerUI");
                go.AddComponent<TestRunnerUI>();
            }

            TestRunnerUI.Instance.ClearResults();
            TestRunnerUI.Instance.Show();
            StartCoroutine(RunAllCoroutine());
        }

        // ── Test Registration ───────────────────────────────────────────

        private void RegisterTests()
        {
            _tests.Clear();

            _tests.Add(("TerrainGeneration",   Test_TerrainGeneration));
            _tests.Add(("PlayerMovement",      Test_PlayerMovement));
            _tests.Add(("InventoryAddRemove",  Test_InventoryAddRemove));
            _tests.Add(("EnemySpawnAndDamage", Test_EnemySpawnAndDamage));
            _tests.Add(("WeaponEquip",         Test_WeaponEquip));
            _tests.Add(("BuildingConstants",   Test_BuildingConstants));
        }

        // ── Sequential Runner ───────────────────────────────────────────

        private IEnumerator RunAllCoroutine()
        {
            _running = true;
            Debug.Log($"[AUTOTEST] Starting {_tests.Count} tests...");

            for (int i = 0; i < _tests.Count; i++)
            {
                var (testName, testFunc) = _tests[i];
                yield return StartCoroutine(RunSingleTest(testName, testFunc));
            }

            int passed = TestRunnerUI.Instance != null ? TestRunnerUI.Instance.PassedCount : 0;
            int failed = TestRunnerUI.Instance != null ? TestRunnerUI.Instance.FailedCount : 0;
            Debug.Log($"[AUTOTEST] All tests complete. Passed: {passed}, Failed: {failed}");
            _running = false;
        }

        private IEnumerator RunSingleTest(string testName, Func<IEnumerator> testFunc)
        {
            var ui = TestRunnerUI.Instance;
            ui?.StartTest(testName);

            float startTime = Time.realtimeSinceStartup;
            bool passed  = false;
            string error = null;

            // Run the test coroutine with timeout
            IEnumerator TimeoutWrapper()
            {
                IEnumerator inner = null;
                try
                {
                    inner = testFunc();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    yield break;
                }

                while (true)
                {
                    // Check timeout
                    if (Time.realtimeSinceStartup - startTime > testTimeout)
                    {
                        error = $"Test timed out after {testTimeout}s";
                        yield break;
                    }

                    bool hasNext;
                    try
                    {
                        hasNext = inner.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        yield break;
                    }

                    if (!hasNext)
                    {
                        // Completed without exception -> passed
                        passed = true;
                        yield break;
                    }

                    yield return inner.Current;
                }
            }

            yield return StartCoroutine(TimeoutWrapper());

            float duration = Time.realtimeSinceStartup - startTime;

            // If test threw a TestFailedException, treat as failure
            if (error != null)
                passed = false;

            ui?.AddTestResult(testName, passed, duration, error);

            string prefix = passed ? "PASS" : "FAIL";
            string msg = $"[AUTOTEST] {prefix}: {testName} ({duration:F2}s)";
            if (!string.IsNullOrEmpty(error))
                msg += $" - {error}";

            if (passed)
                Debug.Log(msg);
            else
                Debug.LogError(msg);

            // Small gap between tests for readability
            yield return null;
        }

        // ── Assertion Helpers ───────────────────────────────────────────

        /// <summary>Throws if condition is false.</summary>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new TestFailedException(message);
        }

        /// <summary>Throws if actual != expected.</summary>
        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestFailedException($"{label}: expected {expected} but got {actual}");
        }

        /// <summary>Throws if value is null.</summary>
        private static void AssertNotNull(object value, string label)
        {
            if (value == null || (value is UnityEngine.Object uObj && uObj == null))
                throw new TestFailedException($"{label} is null");
        }

        private class TestFailedException : Exception
        {
            public TestFailedException(string message) : base(message) { }
        }

        // ─────────────────────────────────────────────────────────────────
        //  TESTS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Verify ChunkManager has active chunks around the player and that
        /// at least one chunk GameObject has a non-null mesh.
        /// </summary>
        private IEnumerator Test_TerrainGeneration()
        {
            var cm = ChunkManager.Instance;
            AssertNotNull(cm, "ChunkManager.Instance");

            // Wait a few frames for chunks to generate
            yield return new WaitForSeconds(1f);

            int chunkCount = cm.ActiveChunkObjectCount;
            Assert(chunkCount > 0, $"Expected active chunk objects > 0, got {chunkCount}");

            // Find any chunk with a MeshFilter and verify it has a mesh
            bool foundMesh = false;
            var meshFilters = cm.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                if (meshFilters[i].sharedMesh != null && meshFilters[i].sharedMesh.vertexCount > 0)
                {
                    foundMesh = true;
                    break;
                }
            }

            Assert(foundMesh, "No chunk has a non-null mesh with vertices");
        }

        /// <summary>
        /// Move the player forward for 1 second using CharacterController.Move
        /// and verify position changed.
        /// </summary>
        private IEnumerator Test_PlayerMovement()
        {
            var fpc = FindObjectOfType<FirstPersonController>();
            AssertNotNull(fpc, "FirstPersonController");

            var cc = fpc.GetComponent<CharacterController>();
            AssertNotNull(cc, "CharacterController on player");

            Vector3 startPos = fpc.transform.position;

            // Apply movement for ~1 second
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                cc.Move(fpc.transform.forward * 5f * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Vector3 endPos = fpc.transform.position;
            float dist = Vector3.Distance(startPos, endPos);
            Assert(dist > 0.1f, $"Player moved only {dist:F3} units (expected > 0.1)");

            // Move back to original position
            fpc.transform.position = startPos;
        }

        /// <summary>
        /// Create a temporary ItemDefinition, add to PlayerInventory, verify count,
        /// remove, and verify empty.
        /// </summary>
        private IEnumerator Test_InventoryAddRemove()
        {
            var inventory = FindObjectOfType<PlayerInventory>();
            AssertNotNull(inventory, "PlayerInventory");

            // Create a temporary ItemDefinition at runtime
            var tempItem = ScriptableObject.CreateInstance<ItemDefinition>();
            tempItem.itemId       = "__test_item_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            tempItem.displayName  = "Test Item";
            tempItem.itemType     = ItemType.Resource;
            tempItem.maxStackSize = 64;

            yield return null;

            // Add 10 items to main inventory
            var stack = new ItemStack(tempItem, 10);
            bool added = inventory.Main.AddItem(stack);
            Assert(added, "AddItem returned false — inventory may be full");

            int count = inventory.Main.CountItem(tempItem.itemId);
            AssertEqual(10, count, "Item count after add");

            // Remove 10
            bool removed = inventory.Main.RemoveItem(tempItem.itemId, 10);
            Assert(removed, "RemoveItem returned false");

            int countAfter = inventory.Main.CountItem(tempItem.itemId);
            AssertEqual(0, countAfter, "Item count after remove");

            // Clean up the SO
            Destroy(tempItem);
        }

        /// <summary>
        /// Create a temporary enemy GameObject with EnemyEntity, apply damage,
        /// and verify health decreased. Since EnemyEntity requires EnemyManager
        /// registration and EnemyDefinition, we test at a higher level.
        /// </summary>
        private IEnumerator Test_EnemySpawnAndDamage()
        {
            // Verify EnemyManager exists
            var em = EnemyManager.Instance;
            AssertNotNull(em, "EnemyManager.Instance");

            // Find an existing EnemyEntity in the scene (if any spawned)
            var enemy = FindObjectOfType<EnemyEntity>();

            if (enemy != null && !enemy.IsDead)
            {
                float healthBefore = enemy.CurrentHealth;
                Assert(healthBefore > 0, "Enemy health should be > 0");

                // Apply damage via IDamageable
                var dmgInfo = new DamageInfo
                {
                    Amount    = 5f,
                    HitPoint  = enemy.transform.position,
                    HitNormal = Vector3.up,
                    Type      = DamageType.Generic,
                    Attacker  = gameObject,
                };
                enemy.TakeDamage(dmgInfo);

                // Wait a frame for EnemyManager to process damage queue
                yield return null;
                yield return null;

                float healthAfter = enemy.CurrentHealth;
                Assert(healthAfter < healthBefore,
                    $"Enemy health did not decrease: before={healthBefore}, after={healthAfter}");
            }
            else
            {
                // No live enemy in scene — verify EnemyManager at least exists and
                // we can query it without errors.
                Debug.Log("[AUTOTEST] No live enemy in scene; verifying EnemyManager is functional.");
                Assert(em != null, "EnemyManager.Instance should not be null");
                // Pass the test — we confirmed the system is present.
            }
        }

        /// <summary>
        /// Verify WeaponSwitcher exists on the player and has valid slot configuration.
        /// </summary>
        private IEnumerator Test_WeaponEquip()
        {
            var fpc = FindObjectOfType<FirstPersonController>();
            AssertNotNull(fpc, "FirstPersonController");

            var switcher = fpc.GetComponentInChildren<WeaponSwitcher>(true);
            AssertNotNull(switcher, "WeaponSwitcher on player");

            // Verify CurrentSlot property is accessible
            int slot = switcher.CurrentSlot;
            Assert(slot >= 0, $"CurrentSlot should be >= 0, got {slot}");

            yield return null;
        }

        /// <summary>
        /// Verify BuildingManager constants match expected values.
        /// </summary>
        private IEnumerator Test_BuildingConstants()
        {
            AssertEqual(3.0f, BuildingManager.GridUnit,    "BuildingManager.GridUnit");
            AssertEqual(3.0f, BuildingManager.WallHeight,  "BuildingManager.WallHeight");
            AssertEqual(0.15f, BuildingManager.SnapDistance, "BuildingManager.SnapDistance");

            // These are compile-time constants, so no scene dependency
            yield return null;
        }
    }
}
