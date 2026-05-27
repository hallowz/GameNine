using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.NPCs
{
    /// <summary>
    /// Singleton ScriptableObject holding every generated
    /// <see cref="NpcDefinition"/>. Lives at
    /// <c>Assets/Resources/NpcRegistry.asset</c> and is loaded via
    /// <c>Resources.Load</c>. Populated by
    /// <c>Assets/Editor/Data/NpcSoGenerator.cs</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcRegistry", menuName = "Voidborne/NPCs/NPC Registry")]
    public class NpcRegistry : ScriptableObject
    {
        private static NpcRegistry _instance;

        /// <summary>Section string used by the V2.4 generator to flag trader NPCs.</summary>
        public const string TraderSectionKey = "Travelling Merchants";

        public static NpcRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var loaded = Resources.Load<NpcRegistry>("NpcRegistry");
                if (loaded == null)
                {
                    Debug.LogError("[NpcRegistry] No NpcRegistry found in Resources. Run Voidborne/Generate/NPCs to create one.");
                    return null;
                }
                _instance = loaded;
                return loaded;
            }
        }

        [Tooltip("All generated NpcDefinitions, id-sorted.")]
        public List<NpcDefinition> allNpcs = new List<NpcDefinition>();

        private Dictionary<string, NpcDefinition> _byId;
        private int _indexBuiltForCount = -1;

        private void OnEnable()
        {
            _instance = this;
            Reindex();
        }

        public IReadOnlyList<NpcDefinition> AllNpcs => allNpcs;

        public NpcDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndexed();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>Enumerates every NPC flagged as a quest giver (named Kin).</summary>
        public IEnumerable<NpcDefinition> QuestGivers
        {
            get
            {
                if (allNpcs == null) yield break;
                foreach (var n in allNpcs)
                {
                    if (n == null) continue;
                    if (n.isQuestGiver) yield return n;
                }
            }
        }

        /// <summary>Enumerates every NPC whose section identifies them as a trader.</summary>
        public IEnumerable<NpcDefinition> Traders
        {
            get
            {
                if (allNpcs == null) yield break;
                foreach (var n in allNpcs)
                {
                    if (n == null) continue;
                    if (!string.IsNullOrEmpty(n.section) &&
                        n.section.IndexOf(TraderSectionKey, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        yield return n;
                    }
                }
            }
        }

        public void Reindex()
        {
            int count = allNpcs?.Count ?? 0;
            _byId = new Dictionary<string, NpcDefinition>(count);
            _indexBuiltForCount = count;

            if (allNpcs == null) return;

            foreach (var n in allNpcs)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                if (_byId.ContainsKey(n.id))
                {
                    Debug.LogWarning($"[NpcRegistry] Duplicate NPC ID '{n.id}' — skipping second entry.", this);
                    continue;
                }
                _byId[n.id] = n;
            }
        }

        private void EnsureIndexed()
        {
            int currentCount = allNpcs?.Count ?? 0;
            if (_byId == null || _indexBuiltForCount != currentCount) Reindex();
        }
    }
}
