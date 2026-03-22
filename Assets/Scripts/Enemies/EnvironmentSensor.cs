using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Voidborne.Enemies
{
    /// <summary>
    /// Hemisphere lidar sensor built on Unity's RaycastCommand job system.
    ///
    /// Raycasts are batched and dispatched to worker threads via RaycastCommand.ScheduleBatch.
    /// Results are consumed on the following frame — never blocks the main thread.
    ///
    /// Scan rate is staggered per-enemy: each sensor updates every <see cref="ScanIntervalFrames"/>
    /// frames, offset by a hash of the GameObject's InstanceID, so N enemies spread their scans
    /// evenly across N frames rather than all firing at once.
    ///
    /// Usage:
    ///   Call <see cref="Tick"/> once per frame (from EnemyNavigation).
    ///   Query <see cref="GetBestCoverPoint"/>, <see cref="GetOpenDirections"/>,
    ///   <see cref="NearestObstacleDistance"/> as needed.
    /// </summary>
    public sealed class EnvironmentSensor
    {
        // -----------------------------------------------------------------------
        // Configuration
        // -----------------------------------------------------------------------

        /// <summary>How many frames between full scans. Actual cost is amortised across this window.</summary>
        public int ScanIntervalFrames = 4;

        /// <summary>How far each lidar ray travels.</summary>
        public float ScanRadius = 14f;

        /// <summary>Layer mask for obstacle detection (terrain + buildings + props).</summary>
        public LayerMask ObstacleMask = Physics.DefaultRaycastLayers;

        /// <summary>How far behind an obstacle face to place a cover candidate point.</summary>
        public float CoverOffset = 1.2f;

        /// <summary>Minimum height clearance for a cover point (enemy must fit).</summary>
        public float CoverClearance = 1.8f;

        // -----------------------------------------------------------------------
        // Ray directions (generated once, shared across all sensors)
        // -----------------------------------------------------------------------

        private const int RayCount = 26; // 24 hemisphere + 2 vertical (up/down)
        private static readonly Vector3[] s_LocalDirs = BuildDirections();

        private static Vector3[] BuildDirections()
        {
            // Fibonacci hemisphere (uniform distribution on upper hemisphere)
            // plus a straight-down and straight-forward ray for grounding helpers.
            var dirs = new Vector3[RayCount];
            int hemiCount = RayCount - 2;
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < hemiCount; i++)
            {
                float t   = (float)i / hemiCount;
                float inc = Mathf.Acos(1f - t);          // tilt from up
                float az  = goldenAngle * i;
                dirs[i] = new Vector3(
                    Mathf.Sin(inc) * Mathf.Cos(az),
                    Mathf.Cos(inc),
                    Mathf.Sin(inc) * Mathf.Sin(az)
                ).normalized;
            }
            dirs[hemiCount]     = Vector3.down;
            dirs[hemiCount + 1] = Vector3.forward;
            return dirs;
        }

        // -----------------------------------------------------------------------
        // Per-sensor state
        // -----------------------------------------------------------------------

        private readonly int    _frameOffset;
        private readonly Transform _transform;

        // Latest completed scan results (main-thread safe to read)
        private readonly ScanResult[] _results = new ScanResult[RayCount];
        private bool _hasResults;

        // Cover candidates derived from the last scan
        private readonly List<Vector3> _coverCandidates = new(16);

        // Job tracking
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit>     _hits;
        private Unity.Jobs.JobHandle        _pendingJob;
        private bool                        _jobInFlight;

        // -----------------------------------------------------------------------

        private struct ScanResult
        {
            public Vector3 WorldDir;
            public bool    DidHit;
            public Vector3 HitPoint;
            public Vector3 HitNormal;
            public float   Distance;
        }

        // -----------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------

        public EnvironmentSensor(Transform owner)
        {
            _transform   = owner;
            _frameOffset = Mathf.Abs(owner.gameObject.GetInstanceID()) % ScanIntervalFrames;
        }

        // -----------------------------------------------------------------------
        // Main loop — call once per frame
        // -----------------------------------------------------------------------

        public void Tick()
        {
            // Consume completed job from last trigger
            if (_jobInFlight && _pendingJob.IsCompleted)
            {
                _pendingJob.Complete();
                ProcessResults();
                _commands.Dispose();
                _hits.Dispose();
                _jobInFlight = false;
            }

            // Trigger a new scan on this sensor's scheduled frame
            if (Time.frameCount % ScanIntervalFrames == _frameOffset && !_jobInFlight)
                ScheduleScan();
        }

        // -----------------------------------------------------------------------
        // Public queries (read last completed frame — zero main-thread cost)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Finds the nearest cover point that breaks line-of-sight from <paramref name="dangerPos"/>
        /// and has enough clearance for an enemy to stand in.
        /// Returns null if no valid cover is found.
        /// </summary>
        public Vector3? GetBestCoverPoint(Vector3 dangerPos)
        {
            if (!_hasResults || _coverCandidates.Count == 0) return null;

            Vector3 origin  = _transform.position;
            float   bestDist = float.MaxValue;
            Vector3? best   = null;

            foreach (Vector3 candidate in _coverCandidates)
            {
                // Must break line-of-sight to danger
                if (Physics.Linecast(candidate + Vector3.up * 1f, dangerPos + Vector3.up * 1f,
                        ObstacleMask))
                {
                    // Must have clearance (enemy can stand there)
                    if (!Physics.CheckSphere(candidate + Vector3.up * (CoverClearance * 0.5f),
                            0.35f, ObstacleMask))
                    {
                        float d = Vector3.Distance(origin, candidate);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best     = candidate;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Returns directions (in world space) that have no obstacle within <paramref name="minDistance"/>.
        /// Useful for flanking: pick an open direction that isn't straight toward or away from the target.
        /// </summary>
        public List<Vector3> GetOpenDirections(float minDistance = -1f)
        {
            if (minDistance < 0f) minDistance = ScanRadius * 0.5f;
            var open = new List<Vector3>(8);
            if (!_hasResults) return open;
            foreach (ScanResult r in _results)
            {
                if (!r.DidHit || r.Distance > minDistance)
                    open.Add(r.WorldDir);
            }
            return open;
        }

        /// <summary>
        /// Returns the distance to the nearest obstacle in approximately <paramref name="worldDir"/>.
        /// Uses the closest cached ray direction. Returns <see cref="ScanRadius"/> if unobstructed.
        /// </summary>
        public float NearestObstacleDistance(Vector3 worldDir)
        {
            if (!_hasResults) return ScanRadius;

            worldDir.Normalize();
            float bestDot  = -1f;
            float bestDist = ScanRadius;

            foreach (ScanResult r in _results)
            {
                float d = Vector3.Dot(r.WorldDir, worldDir);
                if (d > bestDot)
                {
                    bestDot  = d;
                    bestDist = r.DidHit ? r.Distance : ScanRadius;
                }
            }

            return bestDist;
        }

        /// <summary>True once the first scan has completed.</summary>
        public bool HasData => _hasResults;

        // -----------------------------------------------------------------------
        // Job scheduling
        // -----------------------------------------------------------------------

        private void ScheduleScan()
        {
            Vector3 origin = _transform.position + Vector3.up * 0.8f; // chest height

            _commands = new NativeArray<RaycastCommand>(RayCount, Allocator.TempJob);
            _hits     = new NativeArray<RaycastHit>(RayCount,     Allocator.TempJob);

            var queryParams = new QueryParameters(ObstacleMask, false, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < RayCount; i++)
            {
                _commands[i] = new RaycastCommand(origin, s_LocalDirs[i], queryParams, ScanRadius);
            }

            // Schedule — runs on job-system worker threads
            _pendingJob  = RaycastCommand.ScheduleBatch(_commands, _hits, 4);
            _jobInFlight = true;
        }

        // -----------------------------------------------------------------------
        // Result processing (called on main thread after job completes)
        // -----------------------------------------------------------------------

        private void ProcessResults()
        {
            _coverCandidates.Clear();

            for (int i = 0; i < RayCount; i++)
            {
                RaycastHit hit = _hits[i];
                bool didHit    = hit.collider != null;

                _results[i] = new ScanResult
                {
                    WorldDir  = s_LocalDirs[i],
                    DidHit    = didHit,
                    HitPoint  = didHit ? hit.point  : Vector3.zero,
                    HitNormal = didHit ? hit.normal  : Vector3.zero,
                    Distance  = didHit ? hit.distance : ScanRadius,
                };

                // Generate a cover candidate: step behind the obstacle face
                // using the surface normal so the point is on the far side of the wall
                if (didHit && hit.distance > 0.5f)
                {
                    Vector3 candidate = hit.point + hit.normal * CoverOffset;
                    // Project to ground
                    if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                            4f, ObstacleMask))
                        candidate = ground.point + Vector3.up * 0.05f;

                    _coverCandidates.Add(candidate);
                }
            }

            _hasResults = true;
        }

        // -----------------------------------------------------------------------
        // Cleanup — call from OnDestroy of the owning MonoBehaviour
        // -----------------------------------------------------------------------

        public void Dispose()
        {
            if (_jobInFlight)
            {
                _pendingJob.Complete();
                if (_commands.IsCreated) _commands.Dispose();
                if (_hits.IsCreated)     _hits.Dispose();
                _jobInFlight = false;
            }
        }
    }
}
