#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
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

        /// <summary>Loads items.json. Top-level shape is a dictionary keyed by item ID.</summary>
        public static Dictionary<string, ItemJson> LoadItems()
        {
            if (_items != null) return _items;
            _items = ReadJson<Dictionary<string, ItemJson>>("items.json");
            return _items;
        }

        /// <summary>Loads npcs.json. Top-level shape is a flat array.</summary>
        public static List<NpcJson> LoadNpcs()
        {
            if (_npcs != null) return _npcs;
            _npcs = ReadJson<List<NpcJson>>("npcs.json");
            return _npcs;
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
