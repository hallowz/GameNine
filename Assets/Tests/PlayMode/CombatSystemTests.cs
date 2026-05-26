using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using Voidborne.Combat;
using Voidborne.Enemies;

namespace Voidborne.Tests.PlayMode
{
    [TestFixture]
    public class CombatSystemTests
    {
        private GameObject _managerGo;
        private EnemyManager _enemyManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Ensure no leftover singleton from a previous test.
            if (EnemyManager.Instance != null)
                Object.Destroy(EnemyManager.Instance.gameObject);
            yield return null;

            _managerGo = new GameObject("TestEnemyManager");
            _enemyManager = _managerGo.AddComponent<EnemyManager>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_managerGo != null) Object.Destroy(_managerGo);
            yield return null;
            yield return null;
        }

        // ----- helpers -------------------------------------------------------

        /// <summary>
        /// Creates a minimal EnemyDefinition ScriptableObject for testing.
        /// </summary>
        private static EnemyDefinition CreateTestDefinition(
            float maxHealth = 100f,
            float armor = 0f,
            EnemyCategory category = EnemyCategory.Optimized)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.enemyName = "TestEnemy";
            def.maxHealth = maxHealth;
            def.moveSpeed = 4f;
            def.attackDamage = 10f;
            def.attackRange = 2f;
            def.detectionRange = 15f;
            def.armor = armor;
            def.attackCooldown = 1.5f;
            def.attackWindup = 0.3f;
            def.category = category;
            return def;
        }

        /// <summary>
        /// Creates a test enemy GameObject with CharacterController and EnemyEntity.
        /// The EnemyEntity's definition is set via serialized field reflection.
        /// Returns the entity; the caller must wait a frame for Start() to register.
        /// </summary>
        private static (GameObject go, EnemyEntity entity) CreateTestEnemy(
            EnemyDefinition def, Vector3 position)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;

            // CharacterController is required by EnemyEntity.
            go.AddComponent<CharacterController>();

            var entity = go.AddComponent<EnemyEntity>();

            // Inject the definition via reflection (it is a [SerializeField]).
            var field = typeof(EnemyEntity).GetField("_definition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(entity, def);

            return (go, entity);
        }

        // -------------------------------------------------------------------
        // 1. DamageInfo construction with all damage types
        // -------------------------------------------------------------------

        [Test]
        public void DamageInfo_ConstructionWithAllDamageTypes()
        {
            var damageTypes = new[]
            {
                DamageType.Bullet, DamageType.Melee, DamageType.Projectile,
                DamageType.Explosive, DamageType.Fire, DamageType.Poison,
                DamageType.Energy, DamageType.Generic
            };

            foreach (var type in damageTypes)
            {
                var info = new DamageInfo
                {
                    Amount = 25f,
                    HitPoint = Vector3.one,
                    HitNormal = Vector3.up,
                    Type = type,
                    Attacker = null
                };

                Assert.AreEqual(25f, info.Amount, $"Amount mismatch for {type}");
                Assert.AreEqual(type, info.Type, $"Type mismatch for {type}");
                Assert.AreEqual(Vector3.one, info.HitPoint, $"HitPoint mismatch for {type}");
                Assert.AreEqual(Vector3.up, info.HitNormal, $"HitNormal mismatch for {type}");
            }
        }

        // -------------------------------------------------------------------
        // 2. Enemy takes damage and health decreases
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_TakesDamage_HealthDecreases()
        {
            var def = CreateTestDefinition(maxHealth: 100f, armor: 0f);
            var (go, entity) = CreateTestEnemy(def, Vector3.up * 2f);

            // Wait for Start() to register with EnemyManager.
            yield return null;
            yield return null;

            float healthBefore = entity.CurrentHealth;
            Assert.AreEqual(100f, healthBefore, 0.01f, "Enemy should start at max health");

            // Queue damage through EnemyManager (the standard path).
            _enemyManager.QueueDamage(new DamageEvent
            {
                enemyId = entity.GetInstanceID(),
                amount = 30f,
                hitPoint = default,
                hitNormal = default,
                type = DamageType.Bullet,
            });

            // Damage is processed in Update, so wait a frame.
            yield return null;

            float healthAfter = entity.CurrentHealth;
            Assert.Less(healthAfter, healthBefore, "Health should decrease after taking damage");
            Assert.AreEqual(70f, healthAfter, 0.5f, "Health should be 100 - 30 = 70");

            Object.Destroy(go);
            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 3. Enemy dies when health reaches zero
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_DiesWhenHealthReachesZero()
        {
            var def = CreateTestDefinition(maxHealth: 50f);
            var (go, entity) = CreateTestEnemy(def, Vector3.up * 2f);
            yield return null;
            yield return null;

            // Deal lethal damage.
            _enemyManager.QueueDamage(new DamageEvent
            {
                enemyId = entity.GetInstanceID(),
                amount = 999f,
                hitPoint = default,
                hitNormal = default,
                type = DamageType.Generic,
            });

            yield return null;

            Assert.IsTrue(entity.IsDead, "Enemy should be dead after lethal damage");

            // The entity schedules Destroy with a short delay; wait for it.
            yield return new WaitForSeconds(0.5f);
            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 4. Enemy stagger mechanic works
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Enemy_StaggerMechanic_Works()
        {
            var def = CreateTestDefinition(maxHealth: 200f);
            var (go, entity) = CreateTestEnemy(def, Vector3.up * 2f);
            yield return null;
            yield return null;

            Assert.IsFalse(entity.IsStaggered, "Enemy should not be staggered initially");

            // Apply a 2-second stagger.
            entity.Stagger(2f);
            yield return null;

            Assert.IsTrue(entity.IsStaggered, "Enemy should be staggered after Stagger()");

            Object.Destroy(go);
            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 5. Multiple enemies can be registered/unregistered
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MultipleEnemies_RegisterAndUnregister()
        {
            var def = CreateTestDefinition(maxHealth: 80f);

            var (go1, entity1) = CreateTestEnemy(def, new Vector3(0, 2, 0));
            var (go2, entity2) = CreateTestEnemy(def, new Vector3(5, 2, 0));
            var (go3, entity3) = CreateTestEnemy(def, new Vector3(10, 2, 0));

            // Wait for all Start() calls.
            yield return null;
            yield return null;

            Assert.AreEqual(3, _enemyManager.ActiveCount,
                "Three enemies should be registered");

            // Destroy one enemy.
            Object.Destroy(go2);
            yield return null;
            yield return null;

            Assert.AreEqual(2, _enemyManager.ActiveCount,
                "After destroying one enemy, count should be 2");

            Object.Destroy(go1);
            Object.Destroy(go3);
            Object.Destroy(def);
        }

        // -------------------------------------------------------------------
        // 6. Damage types are correctly applied (armor reduction)
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DamageTypes_ArmorReducesDamage()
        {
            // Enemy with 10 armor — should reduce each hit by 10.
            var def = CreateTestDefinition(maxHealth: 100f, armor: 10f);
            var (go, entity) = CreateTestEnemy(def, Vector3.up * 2f);
            yield return null;
            yield return null;

            float healthBefore = entity.CurrentHealth;
            Assert.AreEqual(100f, healthBefore, 0.01f);

            // Deal 25 bullet damage. After armor reduction: 25 - 10 = 15 actual.
            _enemyManager.QueueDamage(new DamageEvent
            {
                enemyId = entity.GetInstanceID(),
                amount = 25f,
                hitPoint = default,
                hitNormal = default,
                type = DamageType.Bullet,
            });

            yield return null;

            float healthAfter = entity.CurrentHealth;
            Assert.AreEqual(85f, healthAfter, 0.5f,
                "Health should be 100 - (25 - 10 armor) = 85");

            Object.Destroy(go);
            Object.Destroy(def);
        }
    }
}
