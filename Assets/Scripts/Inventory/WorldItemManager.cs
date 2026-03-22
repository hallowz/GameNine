using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for all WorldItems. Replaces per-item MonoBehaviour.Update with
/// a single Update that drives all items, handles lifetime/distance despawning,
/// enforces a max item cap, and staggers merge checks across frames.
/// </summary>
public class WorldItemManager : MonoBehaviour
{
    public static WorldItemManager Instance { get; private set; }

    private readonly List<WorldItem> _items = new();
    private Transform _playerTransform;
    private int _mergeRobin;

    private const int MaxWorldItems      = 150;
    private const float DespawnLifetime  = 180f;         // 3 minutes
    private const float DespawnDistSq    = 100f * 100f;  // 100 m
    private const int MergeChecksPerFrame = 6;
    private const float SkipTickDistSq   = 50f * 50f;    // stop bobbing/magnet beyond 50 m

    /// <summary>Lazily create the manager if it doesn't exist.</summary>
    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("WorldItemManager");
        go.AddComponent<WorldItemManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(WorldItem item) => _items.Add(item);

    public void Unregister(WorldItem item) => _items.Remove(item);

    /// <summary>Cached player transform (looked up once).</summary>
    public Transform PlayerTransform
    {
        get
        {
            if (_playerTransform == null)
            {
                var pi = FindFirstObjectByType<PlayerInventory>();
                if (pi != null) _playerTransform = pi.transform;
            }
            return _playerTransform;
        }
    }

    private void Update()
    {
        Transform player = PlayerTransform;
        float dt   = Time.deltaTime;
        float time = Time.time;

        // --- Tick + despawn (reverse iterate so Destroy is safe) ---
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            WorldItem item = _items[i];
            if (item == null) { _items.RemoveAt(i); continue; }

            // Lifetime despawn
            if (time - item.SpawnTime > DespawnLifetime)
            {
                Destroy(item.gameObject);
                continue;
            }

            // Distance despawn (grounded only — let falling items land first)
            if (item.IsGrounded && player != null)
            {
                float distSq = (item.transform.position - player.position).sqrMagnitude;
                if (distSq > DespawnDistSq)
                {
                    Destroy(item.gameObject);
                    continue;
                }

                // Skip full tick for distant items (no one can see the bobbing)
                if (distSq > SkipTickDistSq)
                    continue;
            }

            item.Tick(dt, player);
        }

        // --- Hard cap — remove oldest when over limit ---
        while (_items.Count > MaxWorldItems)
        {
            float oldestTime = float.MaxValue;
            int   oldestIdx  = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && _items[i].SpawnTime < oldestTime)
                {
                    oldestTime = _items[i].SpawnTime;
                    oldestIdx  = i;
                }
            }

            if (_items[oldestIdx] != null)
                Destroy(_items[oldestIdx].gameObject);
            else
                _items.RemoveAt(oldestIdx);
        }

        // --- Staggered merge checks (round-robin, N per frame) ---
        if (_items.Count > 0)
        {
            int checks = Mathf.Min(MergeChecksPerFrame, _items.Count);
            for (int c = 0; c < checks; c++)
            {
                _mergeRobin = (_mergeRobin + 1) % _items.Count;
                _items[_mergeRobin]?.TryMergeNearby();
            }
        }
    }
}
