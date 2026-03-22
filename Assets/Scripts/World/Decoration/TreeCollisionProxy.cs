using UnityEngine;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// Pushes the player away from nearby tree trunks each frame.
    /// Attach to the Player GameObject (requires CharacterController).
    /// No physics colliders are created — this does direct position correction,
    /// keeping the physics layer clean and avoiding raycast interference.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TreeCollisionProxy : MonoBehaviour
    {
        [Tooltip("Radius around the player to query for trees.")]
        [SerializeField] private float queryRadius = 6f;

        [Tooltip("Frames between tree queries. 1 = every frame, 3 = every 3rd frame.")]
        [SerializeField] private int updateInterval = 2;

        private CharacterController _controller;
        private TreeRenderer.TreeInstanceData[] _nearbyBuffer;
        private int _frameCounter;

        private const int MaxNearby = 12;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _nearbyBuffer = new TreeRenderer.TreeInstanceData[MaxNearby];
        }

        private void LateUpdate()
        {
            if (++_frameCounter < updateInterval) return;
            _frameCounter = 0;

            if (TreeRenderer.Instance == null || !_controller.enabled) return;

            Vector3 playerPos = transform.position;
            int count = TreeRenderer.Instance.GetNearbyTrees(playerPos, queryRadius, _nearbyBuffer);

            float playerRadius = _controller.radius;

            for (int i = 0; i < count; i++)
            {
                var tree = _nearbyBuffer[i];
                float trunkRadius = 0.35f * tree.scale;
                float trunkHeight = 3f * tree.scale;
                float minDist = trunkRadius + playerRadius;

                // Check Y overlap (player feet to top of controller vs trunk)
                float playerBottom = playerPos.y;
                float playerTop = playerPos.y + _controller.height;
                float trunkTop = tree.position.y + trunkHeight;

                if (playerBottom > trunkTop || playerTop < tree.position.y)
                    continue;

                // XZ distance check
                float dx = playerPos.x - tree.position.x;
                float dz = playerPos.z - tree.position.z;
                float horizDist = Mathf.Sqrt(dx * dx + dz * dz);

                if (horizDist >= minDist || horizDist < 0.001f)
                    continue;

                // Push player out of trunk
                float pushDist = minDist - horizDist;
                Vector3 pushDir = new Vector3(dx / horizDist, 0f, dz / horizDist);
                _controller.Move(pushDir * pushDist);
            }
        }
    }
}
