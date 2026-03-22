using UnityEngine;
using Voidborne.Player;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Makes the CaveStalker cling to a ceiling above its spawn point and drop onto
    /// the player when they move beneath it.
    ///
    /// Behavior flow:
    ///   Spawn → scan upward for ceiling → if found, teleport up and enter Ambush state.
    ///   While Ambushing, watch player horizontal distance.
    ///   When player is close enough below, Drop: fall to ground and enter normal chase.
    ///   If no ceiling is found, behaves normally (EnemyManager drives everything).
    /// </summary>
    [RequireComponent(typeof(EnemyEntity))]
    [AddComponentMenu("Voidborne/Enemies/Cave Stalker Behavior")]
    public class CaveStalkerBehavior : MonoBehaviour
    {
        [Header("Ceiling Ambush")]
        [SerializeField] private float _ceilingCheckDistance = 10f;
        [SerializeField] private float _ambushDropRadius = 3.5f;
        [SerializeField] private LayerMask _ceilingMask = ~0;
        [SerializeField] private float _ambushTimeout = 30f;

        private EnemyEntity       _entity;
        private CharacterController _cc;
        private Transform       _player;

        private bool  _isAmbushing;
        private float _ambushTimer;

        private void Awake()
        {
            _entity = GetComponent<EnemyEntity>();
            _cc     = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (PlayerManager.Instance != null)
                _player = PlayerManager.Instance.PlayerTransform;

            TrySetupCeilingAmbush();
        }

        private void Update()
        {
            if (!_isAmbushing) return;
            if (_entity.IsDead) { _isAmbushing = false; return; }

            _ambushTimer -= Time.deltaTime;
            bool timedOut = _ambushTimer <= 0f;

            bool playerBelow = false;
            if (_player != null)
            {
                float horizDist = new Vector2(
                    transform.position.x - _player.position.x,
                    transform.position.z - _player.position.z).magnitude;

                playerBelow = horizDist <= _ambushDropRadius
                              && _player.position.y < transform.position.y;
            }

            if (playerBelow || timedOut)
                Drop();
        }

        private void TrySetupCeilingAmbush()
        {
            if (!Physics.Raycast(transform.position, Vector3.up,
                    out RaycastHit hit, _ceilingCheckDistance, _ceilingMask))
                return;

            Vector3 hangPos = hit.point + Vector3.down * 0.6f;
            transform.position = hangPos;

            _isAmbushing = true;
            _ambushTimer = _ambushTimeout;

            if (_cc != null) _cc.enabled = false;
            transform.rotation = Quaternion.Euler(180f, transform.eulerAngles.y, 0f);

            // Set ambush state in manager data
            if (_entity.ManagerIndex >= 0 && EnemyManager.Instance != null)
            {
                ref var data = ref EnemyManager.Instance.GetData(_entity.ManagerIndex);
                data.isAmbushing = true;
                data.state = EnemyState.Ambush;
            }
        }

        private void Drop()
        {
            _isAmbushing = false;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_cc != null) _cc.enabled = true;

            // Clear ambush state — manager will pick up from Advance
            if (_entity.ManagerIndex >= 0 && EnemyManager.Instance != null)
            {
                ref var data = ref EnemyManager.Instance.GetData(_entity.ManagerIndex);
                data.isAmbushing = false;
                data.playerDetected = true;
                data.state = EnemyState.Advance;
                data.stateTimer = 0f;
            }
        }
    }
}
