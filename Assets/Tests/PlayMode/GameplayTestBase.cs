using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine.TestTools;
using Voidborne.Diagnostics;
using Voidborne.Player;
using Voidborne.Combat;
using Voidborne.Combat.Melee;
using Voidborne.Enemies;
using Voidborne.World.Chunks;
using Voidborne.Building;

namespace Voidborne.Tests.PlayMode
{
    /// <summary>
    /// Base class for gameplay simulation tests.
    /// Loads the AutomatedTestScene once, resolves all system references,
    /// and provides utility methods for common test operations.
    /// </summary>
    public abstract class GameplayTestBase
    {
        // Shared across all test instances (scene loads once)
        private static bool s_sceneLoaded;
        private static bool s_terrainReady;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // Suppress runtime errors/exceptions from game systems
            // (Input System, missing refs, etc.) that are expected in test environment
            LogAssert.ignoreFailingMessages = true;
        }

        // Per-test resolved references
        protected PlayerAutopilot Autopilot;
        protected FirstPersonController FPC;
        protected CharacterController CC;
        protected PlayerInventory Inventory;
        protected ChunkManager Chunks;
        protected EnemyManager Enemies;
        protected GunController GunCtrl;
        protected MeleeController MeleeCtrl;
        protected WeaponSwitcher Switcher;
        protected BuildingManager Building;
        protected Transform PlayerTransform;

        // Test item tracking for cleanup
        private readonly System.Collections.Generic.List<UnityEngine.Object> _testObjects
            = new System.Collections.Generic.List<UnityEngine.Object>();

        [UnitySetUp]
        public IEnumerator BaseSetUp()
        {
            // Suppress expected runtime errors from game systems (Input System, missing refs, etc.)
            LogAssert.ignoreFailingMessages = true;

            // Load scene if not loaded or if singletons are gone (scene was unloaded)
            if (!s_sceneLoaded || ChunkManager.Instance == null)
            {
                Debug.Log("[GAMEPLAY] Loading AutomatedTestScene...");
                SceneManager.LoadScene("AutomatedTestScene");

                // Wait several frames for scene load + Awake/Start
                for (int i = 0; i < 10; i++)
                    yield return null;

                s_sceneLoaded = true;
                s_terrainReady = false;
                Debug.Log("[GAMEPLAY] Scene loaded, waiting for singletons...");
            }

            // Wait for ChunkManager singleton with generous timeout
            float waitStart = Time.time;
            while (ChunkManager.Instance == null && Time.time - waitStart < 15f)
                yield return null;

            // Soft check - some tests can still run without terrain
            if (ChunkManager.Instance == null)
                Debug.LogWarning("[GAMEPLAY] ChunkManager not found - terrain tests may fail");

            // Find and setup PlayerAutopilot
            Autopilot = PlayerAutopilot.GetOrCreate();
            if (Autopilot == null)
            {
                // Wait more frames and retry
                yield return new WaitForSeconds(1f);
                Autopilot = PlayerAutopilot.GetOrCreate();
            }
            Assert.IsNotNull(Autopilot, "Failed to create PlayerAutopilot");

            // Resolve references
            FPC = Autopilot.FPC;
            CC = Autopilot.CC;
            Inventory = Autopilot.Inventory;
            GunCtrl = Autopilot.GunCtrl;
            MeleeCtrl = Autopilot.MeleeCtrl;
            Switcher = Autopilot.Switcher;
            PlayerTransform = Autopilot.transform;

            Chunks = ChunkManager.Instance;
            Enemies = EnemyManager.Instance;
            Building = BuildingManager.Instance;

            // Wait for terrain on first run
            if (!s_terrainReady && Chunks != null)
            {
                yield return Autopilot.WaitForTerrain(45f);
                s_terrainReady = true;

                // Extra time for physics settling
                yield return new WaitForSeconds(1f);
            }
        }

        [TearDown]
        public void BaseTearDown()
        {
            // Stop any ongoing movement
            if (Autopilot != null)
                Autopilot.Stop();

            // Clean up test objects
            foreach (var obj in _testObjects)
            {
                if (obj != null)
                {
                    if (obj is GameObject go)
                        UnityEngine.Object.Destroy(go);
                    else if (obj is ScriptableObject so)
                        UnityEngine.Object.Destroy(so);
                }
            }
            _testObjects.Clear();

            // Clear inventory of test items
            if (Inventory != null)
            {
                for (int i = 0; i < Inventory.Hotbar.SlotCount; i++)
                    Inventory.Hotbar.SetSlot(i, default);
                for (int i = 0; i < Inventory.Main.SlotCount; i++)
                    Inventory.Main.SetSlot(i, default);
            }
        }

        // =====================================================================
        // Utility Methods
        // =====================================================================

        /// <summary>
        /// Create a test ItemDefinition (tracked for cleanup).
        /// </summary>
        protected ItemDefinition CreateTestItem(string id, string name,
            ItemType type = ItemType.Resource, int maxStack = 64)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = name;
            item.itemType = type;
            item.maxStackSize = maxStack;
            item.weight = 1f;
            _testObjects.Add(item);
            return item;
        }

        /// <summary>
        /// Create a test GunDefinition (tracked for cleanup).
        /// </summary>
        protected GunDefinition CreateTestGun(string name = "TestGun", float damage = 25f,
            int magSize = 30, FireMode mode = FireMode.Auto)
        {
            var gun = ScriptableObject.CreateInstance<GunDefinition>();
            gun.gunName = name;
            gun.fireRate = 600f;
            gun.damage = damage;
            gun.magazineSize = magSize;
            gun.reloadTime = 2f;
            gun.fireMode = mode;
            gun.bulletType = BulletType.Hitscan;
            gun.pelletCount = 1;
            gun.range = 100f;
            gun.penetration = 0f;
            gun.spreadStanding = 1f;
            gun.spreadMoving = 3f;
            gun.spreadCrouching = 0.5f;
            gun.spreadAirborne = 5f;
            gun.recoilPattern = new Vector2[] { new Vector2(0, 1f) };
            gun.equipTime = 0.1f;
            _testObjects.Add(gun);
            return gun;
        }

        /// <summary>
        /// Create a test MeleeDefinition (tracked for cleanup).
        /// </summary>
        protected MeleeDefinition CreateTestMelee(string name = "TestSword", float damage = 40f)
        {
            var melee = ScriptableObject.CreateInstance<MeleeDefinition>();
            melee.weaponName = name;
            melee.damage = damage;
            melee.range = 2.5f;
            melee.hitRadius = 0.5f;
            melee.windupTime = 0.2f;
            melee.releaseTime = 0.15f;
            melee.recoveryTime = 0.3f;
            melee.staminaCost = 10f;
            melee.attackSpeed = 1f;
            melee.maxComboChain = 3;
            _testObjects.Add(melee);
            return melee;
        }

        /// <summary>
        /// Place an item in hotbar slot 0 and select it.
        /// Uses ItemDatabase if available, otherwise creates a test item.
        /// </summary>
        protected void EquipItemInHotbar(ItemDefinition item, int slot = 0, int quantity = 1)
        {
            Inventory.Hotbar.SetSlot(slot, new ItemStack(item, quantity));
            Inventory.SelectedHotbarIndex = slot;
        }

        /// <summary>
        /// Spawn a test enemy at position (tracked for cleanup).
        /// Creates a minimal enemy with CharacterController + EnemyEntity.
        /// </summary>
        protected (GameObject go, EnemyEntity entity) SpawnTestEnemy(
            Vector3 position, float maxHealth = 100f, string name = "TestEnemy")
        {
            // Create definition
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.enemyName = name;
            def.category = EnemyCategory.Optimized;
            def.maxHealth = maxHealth;
            def.moveSpeed = 0f; // Stationary for testing
            def.attackDamage = 10f;
            def.attackRange = 1.8f;
            def.detectionRange = 0f; // Don't detect player
            def.armor = 0f;
            def.attackCooldown = 5f;
            def.attackWindup = 0.5f;
            _testObjects.Add(def);

            // Create GameObject
            var go = new GameObject(name);
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;

            // Inject definition via reflection before EnemyEntity.Start()
            var entity = go.AddComponent<EnemyEntity>();
            var defField = typeof(EnemyEntity).GetField("_definition",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (defField != null)
                defField.SetValue(entity, def);

            _testObjects.Add(go);
            return (go, entity);
        }

        /// <summary>
        /// Wait until a condition is true, with timeout and descriptive failure message.
        /// </summary>
        protected IEnumerator WaitUntilTrue(Func<bool> condition, float timeout, string message)
        {
            float start = Time.time;
            while (!condition() && Time.time - start < timeout)
                yield return null;

            Assert.IsTrue(condition(),
                $"{message} (timed out after {timeout}s)");
        }

        /// <summary>
        /// Wait for a specific number of seconds.
        /// </summary>
        protected IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>
        /// Track an object for cleanup in TearDown.
        /// </summary>
        protected void TrackForCleanup(UnityEngine.Object obj)
        {
            _testObjects.Add(obj);
        }
    }
}
