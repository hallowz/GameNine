#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Voidborne.Data;
using Voidborne.Data.Schema;

namespace Voidborne.Editor.Cleanup
{
    /// <summary>
    /// Volume 2.9 — moves Pre-Core-60 generated ScriptableObjects out of the live
    /// <c>Assets/ScriptableObjects/Generated/</c> tree into
    /// <c>Assets/ScriptableObjects/_Archived/</c>.
    ///
    /// "Surplus" is defined as: any generated SO whose id is NOT in the live
    /// Core-60 set (loaded from <c>items_core.json</c>) for items, or whose
    /// outputItemId is not in the live set for recipes, or whose item id is not
    /// a live Core 60 machine for machines.
    ///
    /// Idempotent — re-running just moves any newly-surplus assets. Already-archived
    /// files are not touched.
    /// </summary>
    /// <remarks>
    /// Coop note: editor-only asset reorganisation. No runtime state involved.
    /// </remarks>
    public static class ArchiveLegacySos
    {
        private const string GeneratedRoot = "Assets/ScriptableObjects/Generated";
        private const string ArchivedRoot = "Assets/ScriptableObjects/_Archived";

        // Subfolders under Generated/ to scan + their archive destinations.
        // Recipes are filtered by outputItemId; everything else by file name (== id.asset).
        private static readonly (string subfolder, string label)[] Scopes =
        {
            ("Items",    "Items"),
            ("Recipes",  "Recipes"),
            ("Machines", "Machines"),
            ("Fauna",    "Fauna"),
            ("Enemies",  "Enemies"),
            ("NPCs",     "NPCs"),
        };

        [MenuItem("Voidborne/Cleanup/Archive Pre-Core-60 SOs")]
        public static void Run()
        {
            int moved = RunSilently();
            EditorUtility.DisplayDialog(
                "Archive Legacy SOs",
                moved == 0
                    ? "Nothing to archive — all generated SOs are already in the Core 60 set."
                    : $"Archived {moved} surplus generated asset(s) to {ArchivedRoot}.",
                "OK");
        }

        /// <summary>
        /// Programmatic entry point. Returns the number of assets archived.
        /// Used by both the menu item and (optionally) the regenerate orchestrator.
        /// </summary>
        public static int RunSilently()
        {
            // Build live id sets from items_core.json + npcs_core.json.
            HashSet<string> liveItemIds;
            HashSet<string> liveMachineIds;
            HashSet<string> liveNpcLikeIds;

            try
            {
                GameDesignJsonLoader.ClearCache();
                Dictionary<string, ItemJson> items = GameDesignJsonLoader.LoadItems();
                List<NpcJson> npcs = GameDesignJsonLoader.LoadNpcs();

                liveItemIds = new HashSet<string>(items.Keys, StringComparer.Ordinal);

                liveMachineIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var kvp in items)
                {
                    if (kvp.Value != null
                        && string.Equals(kvp.Value.kind, "machine", StringComparison.OrdinalIgnoreCase))
                    {
                        liveMachineIds.Add(kvp.Key);
                    }
                }

                liveNpcLikeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in npcs)
                {
                    if (n == null || string.IsNullOrEmpty(n.name)) continue;
                    liveNpcLikeIds.Add(NameToId(n.name));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ArchiveLegacySos] Failed to load core data: {ex.Message}");
                return 0;
            }

            EnsureFolder(ArchivedRoot);

            int totalMoved = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var (subfolder, label) in Scopes)
                {
                    string srcFolder = $"{GeneratedRoot}/{subfolder}";
                    string dstFolder = $"{ArchivedRoot}/{subfolder}";
                    if (!AssetDatabase.IsValidFolder(srcFolder)) continue;
                    EnsureFolder(dstFolder);

                    int movedHere = ArchiveScope(
                        srcFolder, dstFolder, label,
                        liveItemIds, liveMachineIds, liveNpcLikeIds);
                    totalMoved += movedHere;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArchiveLegacySos] Archived {totalMoved} surplus asset(s) total.");
            return totalMoved;
        }

        // -----------------------------------------------------------------
        // Per-scope archiving
        // -----------------------------------------------------------------

        private static int ArchiveScope(
            string srcFolder, string dstFolder, string label,
            HashSet<string> liveItemIds,
            HashSet<string> liveMachineIds,
            HashSet<string> liveNpcLikeIds)
        {
            string absSrc = Path.GetFullPath(srcFolder);
            if (!Directory.Exists(absSrc)) return 0;

            int moved = 0;
            foreach (string filePath in Directory.GetFiles(absSrc, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                string idOrKey = Path.GetFileNameWithoutExtension(fileName);
                string srcAssetPath = $"{srcFolder}/{fileName}";

                bool isLive = IsLive(label, idOrKey, liveItemIds, liveMachineIds, liveNpcLikeIds);
                if (isLive) continue;

                string dstAssetPath = $"{dstFolder}/{fileName}";
                // If the destination already exists (idempotent re-run), just delete
                // the duplicate at the source so we don't get name collisions.
                if (AssetDatabase.LoadMainAssetAtPath(dstAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(srcAssetPath);
                    moved++;
                    continue;
                }

                string err = AssetDatabase.MoveAsset(srcAssetPath, dstAssetPath);
                if (string.IsNullOrEmpty(err))
                {
                    moved++;
                }
                else
                {
                    Debug.LogWarning(
                        $"[ArchiveLegacySos] Failed to move '{srcAssetPath}' -> '{dstAssetPath}': {err}");
                }
            }

            if (moved > 0)
            {
                Debug.Log($"[ArchiveLegacySos] {label}: archived {moved} surplus asset(s).");
            }
            return moved;
        }

        private static bool IsLive(
            string label,
            string idOrKey,
            HashSet<string> liveItemIds,
            HashSet<string> liveMachineIds,
            HashSet<string> liveNpcLikeIds)
        {
            switch (label)
            {
                case "Items":
                    return liveItemIds.Contains(idOrKey);

                case "Recipes":
                    // Filename convention: {outputItemId}__r{idx}.asset
                    string outputId = ExtractRecipeOutputId(idOrKey);
                    return outputId != null && liveItemIds.Contains(outputId);

                case "Machines":
                    return liveMachineIds.Contains(idOrKey);

                case "Fauna":
                case "Enemies":
                case "NPCs":
                    return liveNpcLikeIds.Contains(idOrKey);
            }
            return false;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static string ExtractRecipeOutputId(string fileStem)
        {
            // RecipeSoGenerator emits "{outputId}__r{idx}". Split on the marker.
            const string marker = "__r";
            int i = fileStem.LastIndexOf(marker, StringComparison.Ordinal);
            if (i <= 0) return null;
            return fileStem.Substring(0, i);
        }

        private static string NameToId(string name)
        {
            // Mirrors ParsingHelpers.SlugifyName (Voidborne.Editor.Data) — replicated
            // here to avoid an asmdef reference from the default Assembly-CSharp-Editor.
            // "Mist Hunter" -> "mist_hunter"; non-alphanumeric runs collapse to a single
            // underscore; leading/trailing underscores trimmed.
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var sb = new System.Text.StringBuilder(name.Length);
            bool lastWasSeparator = true; // suppress leading underscore

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c >= 'A' && c <= 'Z')
                {
                    sb.Append((char)(c + 32));
                    lastWasSeparator = false;
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    lastWasSeparator = false;
                }
                else
                {
                    if (!lastWasSeparator)
                    {
                        sb.Append('_');
                        lastWasSeparator = true;
                    }
                }
            }

            int len = sb.Length;
            while (len > 0 && sb[len - 1] == '_') len--;
            sb.Length = len;
            return sb.ToString();
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
