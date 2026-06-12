using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Enemies.V2
{
    /// <summary>
    /// Singleton ScriptableObject holding every generated
    /// V2.4 <see cref="EnemyDefinition"/>. Lives at
    /// <c>Assets/Resources/EnemyRegistry.asset</c> and is loaded via
    /// <c>Resources.Load</c>. Populated by
    /// <c>Assets/Editor/Data/EnemySoGenerator.cs</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyRegistry", menuName = "Voidborne/Enemies/Enemy Registry")]
    public class EnemyRegistry : ScriptableObject
    {
        private static EnemyRegistry _instance;

        public static EnemyRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var loaded = Resources.Load<EnemyRegistry>("EnemyRegistry");
                if (loaded == null)
                {
                    Debug.LogError("[EnemyRegistry] No EnemyRegistry found in Resources. Run Voidborne/Generate/Enemies to create one.");
                    return null;
                }
                _instance = loaded;
                return loaded;
            }
        }

        [Tooltip("All generated EnemyDefinitions, id-sorted.")]
        public List<EnemyDefinition> allEnemies = new List<EnemyDefinition>();

        private Dictionary<string, EnemyDefinition> _byId;
        private Dictionary<EnemyArchetype, List<EnemyDefinition>> _byFamily;
        private int _indexBuiltForCount = -1;

        private void OnEnable()
        {
            _instance = this;
            Reindex();
        }

        public IReadOnlyList<EnemyDefinition> AllEnemies => allEnemies;

        public EnemyDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndexed();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>Enumerates every enemy whose <see cref="EnemyDefinition.isBoss"/> flag is set (includes finale entries).</summary>
        public IEnumerable<EnemyDefinition> Bosses
        {
            get
            {
                if (allEnemies == null) yield break;
                foreach (var e in allEnemies)
                {
                    if (e == null) continue;
                    if (e.isBoss) yield return e;
                }
            }
        }

        /// <summary>Enumerates every non-boss fodder enemy.</summary>
        public IEnumerable<EnemyDefinition> Fodder
        {
            get
            {
                if (allEnemies == null) yield break;
                foreach (var e in allEnemies)
                {
                    if (e == null) continue;
                    if (!e.isBoss && e.family == EnemyArchetype.Fodder) yield return e;
                }
            }
        }

        public IEnumerable<EnemyDefinition> ByFamily(EnemyArchetype family)
        {
            EnsureIndexed();
            if (_byFamily.TryGetValue(family, out var list)) return list;
            return System.Array.Empty<EnemyDefinition>();
        }

        public void Reindex()
        {
            int count = allEnemies?.Count ?? 0;
            _byId = new Dictionary<string, EnemyDefinition>(count);
            _byFamily = new Dictionary<EnemyArchetype, List<EnemyDefinition>>();
            _indexBuiltForCount = count;

            if (allEnemies == null) return;

            foreach (var e in allEnemies)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                if (!_byId.ContainsKey(e.id))
                {
                    _byId[e.id] = e;
                }
                else
                {
                    Debug.LogWarning($"[EnemyRegistry] Duplicate enemy ID '{e.id}' — skipping second entry.", this);
                }

                if (!_byFamily.TryGetValue(e.family, out var list))
                {
                    list = new List<EnemyDefinition>(8);
                    _byFamily[e.family] = list;
                }
                list.Add(e);
            }
        }

        private void EnsureIndexed()
        {
            int currentCount = allEnemies?.Count ?? 0;
            if (_byId == null || _indexBuiltForCount != currentCount) Reindex();
        }
    }
}
