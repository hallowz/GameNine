using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voidborne.Building.Electricity;
using Voidborne.Player;
using Voidborne.World.Biomes;
using Voidborne.World.Generation;
using Random = UnityEngine.Random;

namespace Voidborne.Enemies
{
    [System.Serializable]
    public class SpawnRule
    {
        public EnemyDefinition enemyDefinition;
        [Min(1f)] public float spawnInterval = 30f;
        [Min(5f)] public float minSpawnDistance  = 20f;
        [Min(10f)] public float maxSpawnDistance = 60f;
    }

    /// <summary>
    /// Places enemies into the world based on biome, Y-depth, population caps,
    /// and time of day. Surface spawns only happen at night; cave spawns are always allowed.
    /// Enemies are ground-snapped to the density field surface on spawn.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private List<SpawnRule> _rules = new();

        [Header("Global Settings")]
        [SerializeField] private int _maxAttempts = 10;

        [Header("Biome Filtering")]
        [SerializeField] private bool _useBiomeFiltering = true;

        private readonly Dictionary<EnemyDefinition, int> _livingCounts = new();
        private DensityFieldNavigator _densityNav;

        private void OnEnable()
        {
            EnemyManager.OnAnyEnemyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            EnemyManager.OnAnyEnemyDied -= HandleEnemyDied;
        }

        private void Start()
        {
            _densityNav = new DensityFieldNavigator();

            foreach (SpawnRule rule in _rules)
            {
                if (rule.enemyDefinition == null) continue;
                _livingCounts[rule.enemyDefinition] = 0;
                StartCoroutine(SpawnLoop(rule));
            }
        }

        private IEnumerator SpawnLoop(SpawnRule rule)
        {
            while (true)
            {
                yield return new WaitForSeconds(rule.spawnInterval);

                if (rule.enemyDefinition == null || rule.enemyDefinition.prefab == null) continue;
                if (EnemyManager.Instance == null) continue;

                EnemyDefinition def = rule.enemyDefinition;
                _livingCounts.TryGetValue(def, out int current);
                if (current >= def.populationCap) continue;

                Transform player = PlayerManager.Instance?.PlayerTransform;
                if (player == null) continue;

                float playerY = player.position.y;
                if (playerY < def.minDepthY || playerY > def.maxDepthY) continue;
                if (!BiomeAllowed(def, player.position)) continue;

                int groupSize = def.category == EnemyCategory.Directed
                    ? Random.Range(1, 3)
                    : Random.Range(def.groupSizeMin, def.groupSizeMax + 1);

                groupSize = Mathf.Min(groupSize, def.populationCap - current);
                if (groupSize <= 0) continue;

                Vector3? spawnOrigin = FindSpawnPosition(player, rule);
                if (spawnOrigin == null) continue;

                // Day/night check: surface spawns only at night
                bool isCave = IsUnderground(spawnOrigin.Value);
                if (!isCave)
                {
                    // Surface or sky island — only spawn at night
                    if (DayNightCycle.Instance != null && DayNightCycle.Instance.IsDaytime)
                        continue;
                }

                // Ground-snap the spawn origin
                float surfaceY = _densityNav.GetSurfaceHeightAt(spawnOrigin.Value, 16f);
                Vector3 snappedOrigin = new Vector3(spawnOrigin.Value.x, surfaceY, spawnOrigin.Value.z);

                // Allocate a shared squad ID for this group
                int squadId = groupSize > 1
                    ? EnemyManager.Instance.Squad.AllocateSquadId()
                    : -1;

                for (int i = 0; i < groupSize; i++)
                {
                    Vector3 offset = Random.insideUnitSphere * 2f;
                    offset.y = 0f;
                    Vector3 spawnPos = snappedOrigin + offset;

                    // Ground-snap each individual spawn position too
                    float individualSurfaceY = _densityNav.GetSurfaceHeightAt(spawnPos, 8f);
                    spawnPos.y = individualSurfaceY;

                    GameObject go = Instantiate(def.prefab, spawnPos,
                        Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                    if (!go.TryGetComponent(out EnemyEntity entity))
                        entity = go.AddComponent<EnemyEntity>();

                    StartCoroutine(SetSquadIdNextFrame(entity, squadId));

                    if (!_livingCounts.ContainsKey(def)) _livingCounts[def] = 0;
                    _livingCounts[def]++;
                }
            }
        }

        private IEnumerator SetSquadIdNextFrame(EnemyEntity entity, int squadId)
        {
            yield return null;
            if (entity != null && entity.ManagerIndex >= 0 && EnemyManager.Instance != null)
            {
                EnemyManager.Instance.SetSquadId(entity.ManagerIndex, squadId);
            }
        }

        private Vector3? FindSpawnPosition(Transform player, SpawnRule rule)
        {
            Camera cam = Camera.main;

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                Vector2 rng  = Random.insideUnitCircle.normalized;
                float   dist = Random.Range(rule.minSpawnDistance, rule.maxSpawnDistance);
                Vector3 candidate = player.position + new Vector3(rng.x, 0f, rng.y) * dist;

                if (cam != null)
                {
                    Vector3 toCandidate = (candidate - cam.transform.position).normalized;
                    float   dot         = Vector3.Dot(cam.transform.forward, toCandidate);
                    if (dot > 0.3f) continue;
                }

                // Verify there's a valid surface at this position
                float surfaceY = _densityNav.GetSurfaceHeightAt(candidate, 16f);
                if (surfaceY < candidate.y - 16f) continue; // no ground found

                return candidate;
            }

            return null;
        }

        /// <summary>
        /// Check if a position is underground using a single upward physics raycast,
        /// matching the player's underground detection approach.
        /// </summary>
        private const int TerrainLayerMask = 1 << 8; // Layer 8 = Terrain

        private bool IsUnderground(Vector3 pos)
        {
            Vector3 origin = pos + Vector3.up * 1f;
            if (!Physics.Raycast(origin, Vector3.up, out RaycastHit hit, 200f,
                    TerrainLayerMask, QueryTriggerInteraction.Ignore))
                return false; // nothing above = surface

            return IsRelevantCeiling(pos.y, hit.point.y);
        }

        /// <summary>
        /// Determines if a ceiling hit actually means "underground" based on altitude.
        /// Below sky zone: ignore sky island hits above. On a sky island: only count
        /// hits within the same altitude band.
        /// </summary>
        private static bool IsRelevantCeiling(float entityY, float hitY)
        {
            if (entityY < DensityFunction.SkyTransitionStart)
                return hitY < DensityFunction.SkyTransitionStart;

            const float bandSize = 176f;
            float entityRel = entityY - DensityFunction.SkyStart;
            float hitRel = hitY - DensityFunction.SkyStart;

            int entityBand = (int)Mathf.Floor(entityRel / bandSize);
            int hitBand = (int)Mathf.Floor(hitRel / bandSize);

            return hitBand == entityBand;
        }

        private bool BiomeAllowed(EnemyDefinition def, Vector3 position)
        {
            if (def.biomeRestrictions == null || def.biomeRestrictions.Count == 0) return true;
            if (!_useBiomeFiltering) return true;

            var currentBiome = BiomeMap.GetBiome(new float2(position.x, position.z));
            if (currentBiome == null) return true;

            return def.biomeRestrictions.Contains(currentBiome);
        }

        private void HandleEnemyDied(EnemyEntity entity)
        {
            if (entity.Definition == null) return;
            if (_livingCounts.ContainsKey(entity.Definition))
                _livingCounts[entity.Definition] = Mathf.Max(0, _livingCounts[entity.Definition] - 1);
        }
    }
}
