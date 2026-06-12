using System.Collections;
using UnityEngine;

namespace Voidborne.Player
{
    /// <summary>
    /// M2 Playtest — keeps the player from spawning inside terrain.
    ///
    /// On Start, raycasts straight down from a high altitude over the
    /// player's current XZ. If a hit is found, the player is lifted
    /// (or dropped) to 2m above the surface. If chunks haven't loaded
    /// yet the script retries for up to <see cref="maxRetryFrames"/>
    /// frames (default 60) before giving up and logging a warning.
    ///
    /// The CharacterController is briefly disabled around the teleport
    /// because CharacterController.Move resists positional jumps.
    ///
    /// Coop note: owner-authoritative — each peer corrects their own
    /// spawn locally.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaytestSpawnSafety : MonoBehaviour
    {
        [Tooltip("Y value the safety raycast starts from. Should be above any terrain peak.")]
        [SerializeField] private float raycastFromY = 200f;

        [Tooltip("Distance the safety raycast travels downward. 400 covers the surface zone.")]
        [SerializeField] private float raycastDistance = 400f;

        [Tooltip("Height above the surface to place the player after the raycast hits.")]
        [SerializeField] private float clearance = 2f;

        [Tooltip("How many frames to wait for chunks before giving up.")]
        [SerializeField] private int maxRetryFrames = 60;

        [Tooltip("Layers considered solid ground for the safety raycast. Default: everything.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            StartCoroutine(SnapToSurfaceWhenReady());
        }

        /// <summary>
        /// Public seam — teleport the player straight to the surface
        /// below their current XZ position. Used by PlaytestDevConsole
        /// when F4 is pressed.
        /// </summary>
        public void SnapNow()
        {
            StartCoroutine(SnapToSurfaceWhenReady());
        }

        private IEnumerator SnapToSurfaceWhenReady()
        {
            for (int frame = 0; frame < maxRetryFrames; frame++)
            {
                Vector3 origin = new Vector3(transform.position.x, raycastFromY, transform.position.z);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    // Skip hits on the player's own collider (CharacterController, ghost, etc.)
                    if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                    {
                        yield return null;
                        continue;
                    }

                    Vector3 target = hit.point + Vector3.up * clearance;
                    bool wasEnabled = _controller != null && _controller.enabled;
                    if (_controller != null) _controller.enabled = false;
                    transform.position = target;
                    if (_controller != null && wasEnabled) _controller.enabled = true;

                    Debug.Log($"[PlaytestSpawnSafety] Surface found at y={hit.point.y:F1}; placed player at {target}.");
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning($"[PlaytestSpawnSafety] No terrain raycast hit after {maxRetryFrames} frames. " +
                             "Player may be inside / above world. F4 to retry.");
        }
    }
}
