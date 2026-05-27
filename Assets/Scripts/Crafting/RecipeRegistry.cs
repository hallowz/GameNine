using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Crafting
{
    /// <summary>
    /// Singleton ScriptableObject that holds every generated
    /// <see cref="RecipeDefinition"/> for fast runtime lookup. Lives at
    /// <c>Assets/Resources/RecipeRegistry.asset</c> and is loaded via
    /// <c>Resources.Load</c>. Populated by
    /// <c>Assets/Editor/Data/RecipeSoGenerator.cs</c>.
    ///
    /// Two indexes are built on enable:
    ///   - <c>_byMachine</c>: <c>viaMachineId ?? ""</c> -> list of recipes.
    ///     The empty string key (<c>""</c>) is the personal-grid /
    ///     bootstrap bucket (null and empty string are both normalised to
    ///     <c>""</c> at indexing time).
    ///   - <c>_byOutput</c>: <c>outputItemId</c> -> list of recipes.
    /// </summary>
    /// <remarks>
    /// Coop note: read-only data registry. No runtime mutation outside
    /// editor tools. Volume 6 will wire crafting against this registry.
    /// </remarks>
    [CreateAssetMenu(fileName = "RecipeRegistry", menuName = "Voidborne/Crafting/Recipe Registry (v2)")]
    public class RecipeRegistry : ScriptableObject
    {
        /// <summary>Sentinel key used for null/empty <c>viaMachineId</c> values (personal grid / bootstrap).</summary>
        public const string PersonalGridKey = "";

        /// <summary>Cached singleton, populated on enable. Use <see cref="Instance"/> for lazy access.</summary>
        private static RecipeRegistry _instance;

        /// <summary>
        /// Resolves the active RecipeRegistry, loading from
        /// <c>Assets/Resources/RecipeRegistry.asset</c> if not yet initialised.
        /// </summary>
        public static RecipeRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var loaded = Resources.Load<RecipeRegistry>("RecipeRegistry");
                if (loaded == null)
                {
                    Debug.LogError("[RecipeRegistry] No RecipeRegistry found in Resources. Run Voidborne/Generate/Recipes to create one.");
                    return null;
                }
                _instance = loaded;
                return loaded;
            }
        }

        [Tooltip("All generated RecipeDefinitions, ordered by outputItemId then recipe index for deterministic diffs.")]
        public List<RecipeDefinition> allRecipes = new List<RecipeDefinition>();

        // Lazy / OnEnable-built indexes.
        private Dictionary<string, List<RecipeDefinition>> _byMachine;
        private Dictionary<string, List<RecipeDefinition>> _byOutput;
        private int _indexBuiltForCount = -1;

        private void OnEnable()
        {
            _instance = this;
            Reindex();
        }

        /// <summary>Read-only view of every registered recipe.</summary>
        public IReadOnlyList<RecipeDefinition> AllRecipes => allRecipes;

        /// <summary>
        /// Returns every recipe registered for <paramref name="machineId"/>.
        /// Pass <c>null</c> or <c>""</c> to query the personal crafting grid /
        /// bootstrap bucket.
        /// </summary>
        public IEnumerable<RecipeDefinition> ByMachine(string machineId)
        {
            EnsureIndexed();
            string key = NormalizeMachineKey(machineId);
            if (_byMachine.TryGetValue(key, out var list)) return list;
            return System.Array.Empty<RecipeDefinition>();
        }

        /// <summary>Returns every recipe whose output is <paramref name="outputItemId"/>.</summary>
        public IEnumerable<RecipeDefinition> ByOutput(string outputItemId)
        {
            EnsureIndexed();
            if (string.IsNullOrEmpty(outputItemId)) return System.Array.Empty<RecipeDefinition>();
            if (_byOutput.TryGetValue(outputItemId, out var list)) return list;
            return System.Array.Empty<RecipeDefinition>();
        }

        /// <summary>
        /// Enumerates every recipe whose <see cref="RecipeDefinition.isBootstrap"/>
        /// flag is set. Used by the bootstrap-path validator (V6.4) and the
        /// onboarding panel.
        /// </summary>
        public IEnumerable<RecipeDefinition> Bootstrap
        {
            get
            {
                if (allRecipes == null) yield break;
                foreach (var r in allRecipes)
                {
                    if (r == null) continue;
                    if (r.isBootstrap) yield return r;
                }
            }
        }

        /// <summary>
        /// Rebuilds the by-machine and by-output indexes from <see cref="allRecipes"/>.
        /// Called automatically on enable; exposed publicly so the V2.3 generator
        /// can refresh indexes after writing fresh data.
        /// </summary>
        public void Reindex()
        {
            int count = allRecipes?.Count ?? 0;
            _byMachine = new Dictionary<string, List<RecipeDefinition>>(count);
            _byOutput = new Dictionary<string, List<RecipeDefinition>>(count);
            _indexBuiltForCount = count;

            if (allRecipes == null) return;

            foreach (var r in allRecipes)
            {
                if (r == null) continue;

                // by-machine bucket
                string machineKey = NormalizeMachineKey(r.viaMachineId);
                if (!_byMachine.TryGetValue(machineKey, out var machineList))
                {
                    machineList = new List<RecipeDefinition>(4);
                    _byMachine[machineKey] = machineList;
                }
                machineList.Add(r);

                // by-output bucket
                if (!string.IsNullOrEmpty(r.outputItemId))
                {
                    if (!_byOutput.TryGetValue(r.outputItemId, out var outList))
                    {
                        outList = new List<RecipeDefinition>(2);
                        _byOutput[r.outputItemId] = outList;
                    }
                    outList.Add(r);
                }
            }
        }

        private void EnsureIndexed()
        {
            int currentCount = allRecipes?.Count ?? 0;
            if (_byMachine == null || _byOutput == null || _indexBuiltForCount != currentCount)
                Reindex();
        }

        /// <summary>
        /// Normalises a viaMachineId for lookup. Null and empty string both map
        /// to <see cref="PersonalGridKey"/> (the empty string sentinel).
        /// </summary>
        private static string NormalizeMachineKey(string machineId)
        {
            return string.IsNullOrEmpty(machineId) ? PersonalGridKey : machineId;
        }
    }
}
