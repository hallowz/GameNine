using UnityEngine;
using Voidborne;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// W2.3 — Attached to each rock prefab.
    /// Rocks start in Static state (kinematic, zero physics cost).
    /// They transition to Physics when terrain beneath them is deformed away.
    /// Player can pick them up via IInteractable (press E).
    /// At most MaxPhysicsRocks rocks may have live physics at once (world-wide).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(MeshCollider))]
    public class WorldRock : MonoBehaviour, IInteractable
    {
        public enum RockState { Static, Physics, Held }

        // ── Pool / placement data ──────────────────────────────────────────
        [HideInInspector] public Vector3 anchorWorldPos;
        [HideInInspector] public Vector3Int hostChunkPos;
        /// <summary>Which prefab variant pool this rock belongs to (0–5).</summary>
        [HideInInspector] public int variantIndex;

        // ── State ──────────────────────────────────────────────────────────
        private RockState _state = RockState.Static;
        private Rigidbody _rb;
        private LayerMask _terrainMask;

        // World-wide cap on simultaneous physics rocks (W4.2: ≤ 32)
        private static int s_ActivePhysicsCount;
        private const int MaxPhysicsRocks = 32;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ApplyState(RockState.Static);
        }

        private void OnDisable()
        {
            // Returning to pool — ensure physics slot is released
            if (_state == RockState.Physics)
            {
                s_ActivePhysicsCount--;
                _state = RockState.Static;
            }
        }

        private void OnDestroy()
        {
            if (_state == RockState.Physics)
                s_ActivePhysicsCount--;
        }


        // ── Setup ──────────────────────────────────────────────────────────

        /// <summary>Called by WorldDecorationManager after retrieval from pool.</summary>
        public void Place(Vector3 anchor, Quaternion rotation, float scale, Vector3Int chunkPos, LayerMask terrainMask)
        {
            anchorWorldPos = anchor;
            hostChunkPos = chunkPos;
            _terrainMask = terrainMask;

            transform.position = anchor;
            transform.rotation = rotation;
            transform.localScale = Vector3.one * scale;

            ApplyState(RockState.Static);
        }

        // ── State machine ──────────────────────────────────────────────────

        public void SetState(RockState newState)
        {
            if (newState == _state) return;

            // Release old physics slot
            if (_state == RockState.Physics)
                s_ActivePhysicsCount--;

            // Guard: never exceed physics cap
            if (newState == RockState.Physics && s_ActivePhysicsCount >= MaxPhysicsRocks)
                return;

            _state = newState;
            ApplyState(newState);
        }

        private void ApplyState(RockState s)
        {
            switch (s)
            {
                case RockState.Static:
                    _rb.isKinematic       = true;
                    _rb.detectCollisions  = false; // Zero physics cost
                    break;

                case RockState.Physics:
                    _rb.isKinematic      = false;
                    _rb.detectCollisions = true;
                    s_ActivePhysicsCount++;
                    break;

                case RockState.Held:
                    _rb.isKinematic      = true;
                    _rb.detectCollisions = false;
                    break;
            }
        }

        // ── Terrain deformation response ───────────────────────────────────

        /// <summary>
        /// Called by WorldDecorationManager when host chunk terrain was rebuilt.
        /// If the ground below is gone, the rock enters physics (falls).
        /// </summary>
        public void OnTerrainDeformed()
        {
            if (_state != RockState.Static) return;

            bool hasGround = Physics.Raycast(
                anchorWorldPos + Vector3.up * 0.5f,
                Vector3.down, 1.2f, _terrainMask);

            if (!hasGround)
                SetState(RockState.Physics);
        }

        // ── IInteractable ──────────────────────────────────────────────────

        public string InteractPrompt => "Pick up";

        public bool CanInteract(Vector3 fromPosition) =>
            _state == RockState.Static || _state == RockState.Physics;

        public void Interact(GameObject interactor)
        {
            if (_state == RockState.Held) return;

            // Release physics slot before switching to Held
            if (_state == RockState.Physics)
                s_ActivePhysicsCount--;
            _state = RockState.Held;
            ApplyState(RockState.Held);

            // Parent to interactor, position in front
            transform.SetParent(interactor.transform, worldPositionStays: false);
            transform.localPosition = new Vector3(0f, -0.1f, 1.1f);
            transform.localRotation = Quaternion.identity;
            // TODO V+: integrate with inventory system
        }
    }
}
