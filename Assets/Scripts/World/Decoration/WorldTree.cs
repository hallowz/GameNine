using UnityEngine;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// W2.2 — Attached to the root of each tree prefab.
    /// Tracks the anchor world position and host chunk. Responds to terrain deformation
    /// by raycasting downward; if the ground is gone, signals that it should be pooled.
    /// No per-frame Update loop.
    /// </summary>
    public class WorldTree : MonoBehaviour
    {
        // ── Persistent placement data ──────────────────────────────────────
        [HideInInspector] public Vector3 anchorWorldPos;
        [HideInInspector] public Vector3Int hostChunkPos;

        private LayerMask _terrainMask;

        // ── Pool housekeeping ──────────────────────────────────────────────
        /// <summary>Which variant pool this tree belongs to (0 = broadleaf, 1 = pine, 2 = scrub).</summary>
        [HideInInspector] public int variantIndex;

        // ── Setup ──────────────────────────────────────────────────────────

        /// <summary>
        /// Called by WorldDecorationManager after retrieval from pool.
        /// Sets position, orientation, and registers placement data.
        /// </summary>
        public void Place(Vector3 anchor, Vector3 normal, float scale, Vector3Int chunkPos, LayerMask terrainMask)
        {
            anchorWorldPos = anchor;
            hostChunkPos = chunkPos;
            _terrainMask = terrainMask;

            transform.position = anchor;

            // Mostly upright with a subtle lean toward the surface normal (10% blend)
            Vector3 blendedUp = Vector3.Lerp(Vector3.up, normal, 0.1f).normalized;
            Quaternion toNormal = Quaternion.FromToRotation(Vector3.up, blendedUp);
            float yaw = (Mathf.Abs(anchor.x * 13.7f + anchor.z * 7.3f) % 360f);
            transform.rotation = toNormal * Quaternion.Euler(0f, yaw, 0f);
            transform.localScale = Vector3.one * scale;
        }

        // ── Terrain deformation response ───────────────────────────────────

        /// <summary>
        /// Called by WorldDecorationManager when terrain in the host chunk was rebuilt.
        /// Returns <c>true</c> if the tree lost its footing and should be returned to pool.
        /// Returns <c>false</c> if the tree was successfully snapped to the new surface.
        /// </summary>
        public bool OnTerrainDeformed()
        {
            // Cast from half a unit above the anchor downward
            Vector3 rayOrigin = anchorWorldPos + Vector3.up * 0.5f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.5f, _terrainMask))
                return true; // No terrain beneath — pool me

            // Snap to new surface
            anchorWorldPos = hit.point;
            transform.position = hit.point;
            return false;
        }
    }
}
