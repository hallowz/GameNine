#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Voidborne.Data.Schema;

namespace Voidborne.Data
{
    /// <summary>
    /// Editor-only loader for the JSON design data under
    /// <c>Design Documents/GameDesign/data/</c>. Returns plain POCOs that the Volume 2
    /// generators convert into ScriptableObjects.
    /// </summary>
    /// <remarks>
    /// Volume 2 is an editor-time content pipeline — the JSON files do not ship in builds.
    /// Hence the class is wrapped in <c>#if UNITY_EDITOR</c>.
    ///
    /// A per-editor-session cache prevents re-parsing the (large) items.json every time a
    /// generator script touches the loader. Call <see cref="ClearCache"/> after re-extracting
    /// the JSON to pick up changes without restarting the editor.
    ///
    /// Coop note: returns stateless data; never mutates the JSON files.
    /// </remarks>
    public static class GameDesignJsonLoader
    {
        // ---- Cache (editor session only) ----
        private static Dictionary<string, ItemJson> _items;
        private static List<NpcJson> _npcs;
        private static Dictionary<string, List<string>> _categories;
        private static List<BuildMaterialJson> _buildMaterials;
        private static Dictionary<string, ItemJson> _decoBlocks;
        private static Dictionary<string, object> _formTemplates;
        private static Dictionary<string, object> _synergyAlts;
        private static Dictionary<string, object> _summary;

        /// <summary>Folder containing the extracted JSON files, relative to the Unity project root.</summary>
        public const string DataDirRelativePath = "Design Documents/GameDesign/data";

        /// <summary>Absolute path to the JSON data folder.</summary>
        public static string DataDirAbsolutePath
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", DataDirRelativePath));

        /// <summary>Clears the in-memory cache. Call after re-extracting JSON to force a re-read.</summary>
        public static void ClearCache()
        {
            _items = null;
            _npcs = null;
            _categories = null;
            _buildMaterials = null;
            _decoBlocks = null;
            _formTemplates = null;
            _synergyAlts = null;
            _summary = null;
        }

        /// <summary>
        /// Loads <c>items_core.json</c> (the Phase-1 Core 60 roster, V2.9). Top-level shape
        /// is a dictionary keyed by item ID. Underscore-prefixed metadata keys
        /// (<c>_schema_version</c>, <c>_schema_notes</c>) are filtered out — these are
        /// human-readable annotations whose values are not ItemJson shapes (the schema
        /// version is an int, schema_notes is an array of strings) so they must be
        /// dropped BEFORE Newtonsoft tries to coerce them into <see cref="ItemJson"/>.
        /// </summary>
        public static Dictionary<string, ItemJson> LoadItems()
        {
            if (_items != null) return _items;
            _items = LoadFilteredItemDict("items_core.json");
            return _items;
        }

        /// <summary>
        /// Opt-in loader for <c>items_backlog.json</c> (the 1031-item content reserve, V2.9).
        /// NOT called by default generators — backlog expansion is M7 Expansion 7.
        /// </summary>
        public static Dictionary<string, ItemJson> LoadItemsBacklog()
        {
            return LoadFilteredItemDict("items_backlog.json");
        }

        /// <summary>
        /// Loads <c>npcs_core.json</c> (the Phase-1 7-NPC roster, V2.9). Top-level shape
        /// is a flat array. Array entries lacking a <c>name</c> field (i.e. the leading
        /// <c>_schema_notes</c> placeholder object) are filtered out.
        /// </summary>
        public static List<NpcJson> LoadNpcs()
        {
            if (_npcs != null) return _npcs;
            var raw = ReadJson<List<NpcJson>>("npcs_core.json");
            _npcs = FilterUnnamedNpcs(raw);
            return _npcs;
        }

        /// <summary>
        /// Opt-in loader for <c>npcs_backlog.json</c> (the 70-NPC content reserve, V2.9).
        /// NOT called by default generators — backlog expansion is M7.
        /// </summary>
        public static List<NpcJson> LoadNpcsBacklog()
        {
            var raw = ReadJson<List<NpcJson>>("npcs_backlog.json");
            return FilterUnnamedNpcs(raw);
        }

        /// <summary>Loads categories.json as a category → item-id-list dictionary.</summary>
        public static Dictionary<string, List<string>> LoadCategories()
        {
            if (_categories != null) return _categories;
            _categories = ReadJson<Dictionary<string, List<string>>>("categories.json");
            return _categories;
        }

        /// <summary>Loads categories.json wrapped in <see cref="CategoriesJson"/>.</summary>
        public static CategoriesJson LoadCategoriesWrapped()
        {
            return new CategoriesJson(LoadCategories());
        }

        /// <summary>Loads build_materials.json (array of build material descriptors).</summary>
        public static List<BuildMaterialJson> LoadBuildMaterials()
        {
            if (_buildMaterials != null) return _buildMaterials;
            _buildMaterials = ReadJson<List<BuildMaterialJson>>("build_materials.json");
            return _buildMaterials;
        }

        /// <summary>Loads deco_blocks.json. Shape mirrors items.json (dictionary of item-like entries).</summary>
        public static Dictionary<string, ItemJson> LoadDecoBlocks()
        {
            if (_decoBlocks != null) return _decoBlocks;
            _decoBlocks = ReadJson<Dictionary<string, ItemJson>>("deco_blocks.json");
            return _decoBlocks;
        }

        /// <summary>Loads form_templates.json as a loose dictionary (shape varies — concrete typing deferred to V2.4).</summary>
        public static Dictionary<string, object> LoadFormTemplates()
        {
            if (_formTemplates != null) return _formTemplates;
            _formTemplates = ReadJson<Dictionary<string, object>>("form_templates.json");
            return _formTemplates;
        }

        /// <summary>Loads synergy_alts.json as a loose dictionary (shape varies — concrete typing deferred to V2.3).</summary>
        public static Dictionary<string, object> LoadSynergyAlts()
        {
            if (_synergyAlts != null) return _synergyAlts;
            _synergyAlts = ReadJson<Dictionary<string, object>>("synergy_alts.json");
            return _synergyAlts;
        }

        /// <summary>Loads summary.json as a loose dictionary. Diagnostic metadata only.</summary>
        public static Dictionary<string, object> LoadSummary()
        {
            if (_summary != null) return _summary;
            _summary = ReadJson<Dictionary<string, object>>("summary.json");
            return _summary;
        }

        // ---- Internals ----

        /// <summary>
        /// Reads an items JSON file at <see cref="DataDirAbsolutePath"/>, drops any
        /// underscore-prefixed top-level keys (metadata), then materialises the rest as
        /// <see cref="ItemJson"/>. Must use the JObject path rather than direct
        /// dictionary deserialisation because the metadata values (e.g.
        /// <c>_schema_version: 3</c>) are not ItemJson-shaped and would crash the
        /// dictionary-typed convertor.
        /// </summary>
        private static Dictionary<string, ItemJson> LoadFilteredItemDict(string fileName)
        {
            string fullPath = Path.Combine(DataDirAbsolutePath, fileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"GameDesignJsonLoader: JSON data file not found at '{fullPath}'. " +
                    $"Ensure the design data has been extracted to '{DataDirRelativePath}'.",
                    fullPath);
            }

            string text = File.ReadAllText(fullPath);
            JObject root;
            try
            {
                root = JObject.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new System.Exception(
                    $"GameDesignJsonLoader: failed to parse '{fileName}' as JObject: {ex.Message}", ex);
            }

            var result = new Dictionary<string, ItemJson>(root.Count);
            foreach (var prop in root.Properties())
            {
                if (string.IsNullOrEmpty(prop.Name)) continue;
                if (prop.Name.StartsWith("_", System.StringComparison.Ordinal)) continue;
                if (prop.Value == null || prop.Value.Type != JTokenType.Object) continue;

                try
                {
                    var item = prop.Value.ToObject<ItemJson>();
                    if (item != null) result[prop.Name] = item;
                }
                catch (JsonException ex)
                {
                    throw new System.Exception(
                        $"GameDesignJsonLoader: failed to parse '{fileName}' entry '{prop.Name}' as ItemJson: {ex.Message}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Filters out NPC array entries lacking a <c>name</c> field. npcs_core.json's
        /// first entry is a <c>_schema_notes</c> placeholder object with no name; npcs_backlog.json
        /// may also accumulate such placeholders over time.
        /// </summary>
        private static List<NpcJson> FilterUnnamedNpcs(List<NpcJson> raw)
        {
            if (raw == null) return new List<NpcJson>();
            var filtered = new List<NpcJson>(raw.Count);
            foreach (var npc in raw)
            {
                if (npc == null) continue;
                if (string.IsNullOrWhiteSpace(npc.name)) continue;
                filtered.Add(npc);
            }
            return filtered;
        }

        private static T ReadJson<T>(string fileName)
        {
            string fullPath = Path.Combine(DataDirAbsolutePath, fileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"GameDesignJsonLoader: JSON data file not found at '{fullPath}'. " +
                    $"Ensure the design data has been extracted to '{DataDirRelativePath}'.",
                    fullPath);
            }

            string text = File.ReadAllText(fullPath);
            try
            {
                return JsonConvert.DeserializeObject<T>(text);
            }
            catch (JsonException ex)
            {
                throw new System.Exception(
                    $"GameDesignJsonLoader: failed to parse '{fileName}' as {typeof(T).Name}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// POCO mirroring an entry in build_materials.json
    /// (id/label/base/via/color/baseQty).
    /// </summary>
    [System.Serializable]
    public class BuildMaterialJson
    {
        public string id;
        public string label;

        /// <summary>
        /// Base item ID consumed by the build-material recipe (e.g. "wood_planks").
        /// Mapped to the JSON property "base" via <see cref="JsonPropertyAttribute"/>
        /// because <c>base</c> is a reserved C# keyword.
        /// </summary>
        [JsonProperty("base")]
        public string baseItem;

        /// <summary>The machine via which the build-material recipe is crafted (e.g. "carpenter_bench").</summary>
        public string via;

        /// <summary>Color key (e.g. "build").</summary>
        public string color;

        /// <summary>Base ingredient quantity per build-material unit.</summary>
        public int baseQty;
    }
}
#endif
