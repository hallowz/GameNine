using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using Voidborne.Player;

namespace Voidborne.Tests.PlayMode
{
    [TestFixture]
    public class PlayerMovementTests
    {
        private GameObject _playerGo;
        private FirstPersonController _fpc;
        private CharacterController _cc;
        private GameObject _groundPlane;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Create a ground plane so the CharacterController can land and IsGrounded works.
            _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _groundPlane.transform.position = Vector3.zero;
            _groundPlane.transform.localScale = new Vector3(10f, 1f, 10f);

            // Create the player above the ground so it falls and lands.
            _playerGo = new GameObject("TestPlayer");
            _cc = _playerGo.AddComponent<CharacterController>();
            _playerGo.transform.position = new Vector3(0f, 1f, 0f);

            // FirstPersonController requires CharacterController (already added).
            // Disable the component briefly so Awake doesn't run until we position it.
            _fpc = _playerGo.AddComponent<FirstPersonController>();

            // Allow physics and CharacterController to settle for a few frames.
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_groundPlane != null) Object.Destroy(_groundPlane);
            yield return null;
        }

        // -------------------------------------------------------------------
        // 1. Player spawns at correct position
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Player_SpawnsAtCorrectPosition()
        {
            // The player was placed at (0, 1, 0). After a couple of frames it
            // should still be near that X/Z location (gravity may shift Y).
            yield return null;

            Vector3 pos = _playerGo.transform.position;
            Assert.AreEqual(0f, pos.x, 0.5f, "Player X should be near spawn X");
            Assert.AreEqual(0f, pos.z, 0.5f, "Player Z should be near spawn Z");
        }

        // -------------------------------------------------------------------
        // 2. Player has CharacterController component
        // -------------------------------------------------------------------

        [Test]
        public void Player_HasCharacterController()
        {
            Assert.IsNotNull(_cc, "Player should have a CharacterController component");
            Assert.IsNotNull(_playerGo.GetComponent<CharacterController>(),
                "GetComponent<CharacterController> should return non-null");
        }

        // -------------------------------------------------------------------
        // 3. Player initial stamina equals max stamina
        // -------------------------------------------------------------------

        [Test]
        public void Player_InitialStamina_EqualsMaxStamina()
        {
            Assert.AreEqual(_fpc.MaxStamina, _fpc.CurrentStamina, 0.01f,
                "CurrentStamina should equal MaxStamina at start");
            Assert.Greater(_fpc.MaxStamina, 0f, "MaxStamina should be positive");
        }

        // -------------------------------------------------------------------
        // 4. ConsumeStamina reduces stamina correctly
        // -------------------------------------------------------------------

        [Test]
        public void ConsumeStamina_ReducesStamina()
        {
            float before = _fpc.CurrentStamina;
            float consumeAmount = 25f;

            _fpc.ConsumeStamina(consumeAmount);

            Assert.AreEqual(before - consumeAmount, _fpc.CurrentStamina, 0.01f,
                "Stamina should decrease by the consumed amount");
        }

        // -------------------------------------------------------------------
        // 5. Stamina doesn't go below zero
        // -------------------------------------------------------------------

        [Test]
        public void ConsumeStamina_DoesNotGoBelowZero()
        {
            // Consume more than max stamina.
            _fpc.ConsumeStamina(_fpc.MaxStamina + 50f);

            Assert.GreaterOrEqual(_fpc.CurrentStamina, 0f,
                "Stamina should never go below zero");
            Assert.AreEqual(0f, _fpc.CurrentStamina, 0.01f,
                "Stamina should be clamped to zero");
        }

        // -------------------------------------------------------------------
        // 6. ExternalSpeedMult defaults to 1.0
        // -------------------------------------------------------------------

        [Test]
        public void ExternalSpeedMult_DefaultsToOne()
        {
            Assert.AreEqual(1f, _fpc.ExternalSpeedMult, 0.001f,
                "ExternalSpeedMult should default to 1.0");
        }

        // -------------------------------------------------------------------
        // 7. Player initially not sprinting or crouching
        // -------------------------------------------------------------------

        [Test]
        public void Player_InitiallyNotSprintingOrCrouching()
        {
            Assert.IsFalse(_fpc.IsSprinting, "Player should not be sprinting at start");
            Assert.IsFalse(_fpc.IsCrouching, "Player should not be crouching at start");
        }
    }
}
