using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Fauna
{
    /// <summary>
    /// Singleton ScriptableObject holding every generated
    /// <see cref="FaunaDefinition"/>. Lives at
    /// <c>Assets/Resources/FaunaRegistry.asset</c> and is loaded via
    /// <c>Resources.Load</c>. Populated by
    /// <c>Assets/Editor/Data/FaunaSoGenerator.cs</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "FaunaRegistry", menuName = "Voidborne/Fauna/Fauna Registry")]
    public class FaunaRegistry : ScriptableObject
    {
        private static FaunaRegistry _instance;

        public static FaunaRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var loaded = Resources.Load<FaunaRegistry>("FaunaRegistry");
                if (loaded == null)
                {
                    Debug.LogError("[FaunaRegistry] No FaunaRegistry found in Resources. Run Voidborne/Generate/Fauna to create one.");
                    return null;
                }
                _instance = loaded;
                return loaded;
            }
        }

        [Tooltip("All generated FaunaDefinitions, id-sorted.")]
        public List<FaunaDefinition> allFauna = new List<FaunaDefinition>();

        private Dictionary<string, FaunaDefinition> _byId;
        private int _indexBuiltForCount = -1;

        private void OnEnable()
        {
            _instance = this;
            Reindex();
        }

        public IReadOnlyList<FaunaDefinition> AllFauna => allFauna;

        public FaunaDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndexed();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>Enumerates every fauna entry flagged tameable.</summary>
        public IEnumerable<FaunaDefinition> Tameable
        {
            get
            {
                if (allFauna == null) yield break;
                foreach (var f in allFauna)
                {
                    if (f == null) continue;
                    if (f.isTameable) yield return f;
                }
            }
        }

        /// <summary>Enumerates every fauna entry flagged aggressive.</summary>
        public IEnumerable<FaunaDefinition> Aggressive
        {
            get
            {
                if (allFauna == null) yield break;
                foreach (var f in allFauna)
                {
                    if (f == null) continue;
                    if (f.isAggressive) yield return f;
                }
            }
        }

        public void Reindex()
        {
            int count = allFauna?.Count ?? 0;
            _byId = new Dictionary<string, FaunaDefinition>(count);
            _indexBuiltForCount = count;

            if (allFauna == null) return;

            foreach (var f in allFauna)
            {
                if (f == null || string.IsNullOrEmpty(f.id)) continue;
                if (_byId.ContainsKey(f.id))
                {
                    Debug.LogWarning($"[FaunaRegistry] Duplicate fauna ID '{f.id}' — skipping second entry.", this);
                    continue;
                }
                _byId[f.id] = f;
            }
        }

        private void EnsureIndexed()
        {
            int currentCount = allFauna?.Count ?? 0;
            if (_byId == null || _indexBuiltForCount != currentCount) Reindex();
        }
    }
}
