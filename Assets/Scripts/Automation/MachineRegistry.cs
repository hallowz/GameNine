using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Singleton ScriptableObject holding every generated
    /// <see cref="MachineDefinition"/>. Lives at
    /// <c>Assets/Resources/MachineRegistry.asset</c> and is loaded via
    /// <c>Resources.Load</c>. Populated by
    /// <c>Assets/Editor/Data/MachineSoGenerator.cs</c>.
    ///
    /// Indexes built on enable / Reindex:
    ///   - <c>_byId</c>: itemId -> MachineDefinition.
    ///   - <c>_byTier</c>: tier (0..7) -> list of machines.
    ///   - <c>_byCategory</c>: <see cref="MachineCategory"/> -> list of machines.
    /// </summary>
    /// <remarks>
    /// Coop note: read-only data registry. Editor-only mutation via the
    /// V2.4 generator.
    /// </remarks>
    [CreateAssetMenu(fileName = "MachineRegistry", menuName = "Voidborne/Automation/Machine Registry")]
    public class MachineRegistry : ScriptableObject
    {
        private static MachineRegistry _instance;

        /// <summary>
        /// Resolves the active MachineRegistry, loading from
        /// <c>Assets/Resources/MachineRegistry.asset</c> if not yet initialised.
        /// </summary>
        public static MachineRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var loaded = Resources.Load<MachineRegistry>("MachineRegistry");
                if (loaded == null)
                {
                    Debug.LogError("[MachineRegistry] No MachineRegistry found in Resources. Run Voidborne/Generate/Machines to create one.");
                    return null;
                }
                _instance = loaded;
                return loaded;
            }
        }

        [Tooltip("All generated MachineDefinitions, id-sorted for deterministic diffs.")]
        public List<MachineDefinition> allMachines = new List<MachineDefinition>();

        private Dictionary<string, MachineDefinition> _byId;
        private Dictionary<int, List<MachineDefinition>> _byTier;
        private Dictionary<MachineCategory, List<MachineDefinition>> _byCategory;
        private int _indexBuiltForCount = -1;

        private void OnEnable()
        {
            _instance = this;
            Reindex();
        }

        /// <summary>Read-only view of every registered machine.</summary>
        public IReadOnlyList<MachineDefinition> AllMachines => allMachines;

        /// <summary>O(1) lookup by machine ID. Returns null when not found.</summary>
        public MachineDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndexed();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>Returns every machine whose <see cref="MachineDefinition.tier"/> equals <paramref name="tier"/>.</summary>
        public IEnumerable<MachineDefinition> ByTier(int tier)
        {
            EnsureIndexed();
            if (_byTier.TryGetValue(tier, out var list)) return list;
            return System.Array.Empty<MachineDefinition>();
        }

        /// <summary>Returns every machine whose category equals <paramref name="category"/>.</summary>
        public IEnumerable<MachineDefinition> ByCategory(MachineCategory category)
        {
            EnsureIndexed();
            if (_byCategory.TryGetValue(category, out var list)) return list;
            return System.Array.Empty<MachineDefinition>();
        }

        /// <summary>Rebuilds the lookup indexes from <see cref="allMachines"/>.</summary>
        public void Reindex()
        {
            int count = allMachines?.Count ?? 0;
            _byId = new Dictionary<string, MachineDefinition>(count);
            _byTier = new Dictionary<int, List<MachineDefinition>>(8);
            _byCategory = new Dictionary<MachineCategory, List<MachineDefinition>>();
            _indexBuiltForCount = count;

            if (allMachines == null) return;

            foreach (var m in allMachines)
            {
                if (m == null) continue;
                if (string.IsNullOrEmpty(m.itemId)) continue;

                if (!_byId.ContainsKey(m.itemId))
                {
                    _byId[m.itemId] = m;
                }
                else
                {
                    Debug.LogWarning($"[MachineRegistry] Duplicate machine ID '{m.itemId}' — skipping second entry.", this);
                }

                if (!_byTier.TryGetValue(m.tier, out var tierList))
                {
                    tierList = new List<MachineDefinition>(4);
                    _byTier[m.tier] = tierList;
                }
                tierList.Add(m);

                if (!_byCategory.TryGetValue(m.category, out var catList))
                {
                    catList = new List<MachineDefinition>(4);
                    _byCategory[m.category] = catList;
                }
                catList.Add(m);
            }
        }

        private void EnsureIndexed()
        {
            int currentCount = allMachines?.Count ?? 0;
            if (_byId == null || _indexBuiltForCount != currentCount) Reindex();
        }
    }
}
