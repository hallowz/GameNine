#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Voidborne.Editor.Cleanup
{
    /// <summary>
    /// Volume 5.1 — archives whole *pre-Core-60* ScriptableObject folders that predate
    /// the items_core.json switch (Volume 2.9). These are the original "Asset Scrap
    /// Manifest" folders from the early master prompt:
    /// <c>Items/</c>, <c>Recipes/</c>, <c>SmeltingRecipes/</c>, <c>Ores/</c>,
    /// <c>Guns/</c>, <c>MeleeWeapons/</c>, <c>WeaponItems/</c>, <c>Bows/</c>,
    /// <c>Throwables/</c>, <c>Projectiles/</c>, <c>Weapons/</c>, <c>Backpacks/</c>.
    ///
    /// Also moves the two superseded scene prefabs
    /// <c>Assets/Prefabs/Workbench.prefab</c> and <c>Assets/Prefabs/Furnace.prefab</c>
    /// (replaced by Volume 3.3's procedural workbench/furnace) into
    /// <c>_Archived/Legacy/Prefabs/</c>.
    ///
    /// Assets land under <c>Assets/ScriptableObjects/_Archived/Legacy/{subfolder}/</c>.
    /// The <c>_Archived/</c> root is gitignored (per Volume 3.4 fix) so the move does
    /// not bloat the repo — assets stay recoverable on disk.
    ///
    /// Idempotent — re-running just sweeps any newly-arrived stragglers; already-
    /// archived files are skipped.
    /// </summary>
    /// <remarks>
    /// Companion to <see cref="ArchiveLegacySos"/> (which scoped to
    /// <c>Generated/</c>). This handles the pre-Generated era. Both should be run
    /// from <c>Voidborne/Cleanup/Archive Pre-Core-60 SOs</c> (the umbrella menu
    /// invokes both).
    /// </remarks>
    public static class ArchivePreCore60Folders
    {
        private const string ScriptableObjectsRoot = "Assets/ScriptableObjects";
        private const string ArchivedLegacyRoot   = "Assets/ScriptableObjects/_Archived/Legacy";
        private const string PrefabsRoot           = "Assets/Prefabs";
        private const string ArchivedPrefabsRoot   = "Assets/ScriptableObjects/_Archived/Legacy/Prefabs";

        // Subfolders directly under Assets/ScriptableObjects/ that are pre-Core-60
        // and should be swept wholesale into _Archived/Legacy/.
        private static readonly string[] LegacySoFolders =
        {
            "Items",
            "Recipes",
            "SmeltingRecipes",
            "Ores",
            "Guns",
            "MeleeWeapons",
            "WeaponItems",
            "Bows",
            "Throwables",
            "Projectiles",
            "Weapons",
            "Backpacks",
        };

        // Individual asset paths that have a Core-60 replacement.
        // Workbench / Furnace prefabs are superseded by Volume 3.3's procedural
        // versions under Assets/ScriptableObjects/Generated/Items/{workbench,furnace}.asset
        // plus the generated world prefab variant.
        private static readonly string[] LegacyPrefabs =
        {
            "Assets/Prefabs/Workbench.prefab",
            "Assets/Prefabs/Furnace.prefab",
        };

        [MenuItem("Voidborne/Cleanup/Archive Pre-Core-60 Legacy Folders")]
        public static void RunMenu()
        {
            int moved = RunSilently();
            EditorUtility.DisplayDialog(
                "Archive Pre-Core-60 Folders",
                moved == 0
                    ? "Nothing to archive — all pre-Core-60 SO folders are already empty or archived."
                    : $"Archived {moved} pre-Core-60 asset(s) to {ArchivedLegacyRoot}.",
                "OK");
        }

        /// <summary>
        /// Programmatic entry point. Returns the total count of moved/archived assets
        /// across all legacy folders plus the legacy prefabs. Idempotent.
        /// </summary>
        public static int RunSilently()
        {
            // IMPORTANT: AssetDatabase.CreateFolder must run OUTSIDE
            // Start/StopAssetEditing (the new folder isn't registered until the
            // batch closes, and MoveAsset will then reject "parent dir not in
            // asset DB" and Unity auto-suffixes a " 1"/" 2"/... clone). So we
            // pre-create the full destination tree first, walking the source
            // tree to mirror its structure, then run all the moves in a batch.
            EnsureFolder(ArchivedLegacyRoot);
            foreach (string sub in LegacySoFolders)
            {
                string srcFolder = $"{ScriptableObjectsRoot}/{sub}";
                string dstFolder = $"{ArchivedLegacyRoot}/{sub}";
                if (!AssetDatabase.IsValidFolder(srcFolder)) continue;
                PrecreateFolderTreeMirror(srcFolder, dstFolder);
            }
            EnsureFolder(ArchivedPrefabsRoot);

            int total = 0;
            var perScopeCounts = new Dictionary<string, int>();

            AssetDatabase.StartAssetEditing();
            try
            {
                // Recursive sweep of each legacy SO folder.
                foreach (string sub in LegacySoFolders)
                {
                    string srcFolder = $"{ScriptableObjectsRoot}/{sub}";
                    string dstFolder = $"{ArchivedLegacyRoot}/{sub}";
                    if (!AssetDatabase.IsValidFolder(srcFolder)) continue;

                    int moved = SweepFolderRecursive(srcFolder, dstFolder);
                    if (moved > 0)
                    {
                        perScopeCounts[sub] = moved;
                        total += moved;
                    }
                }

                int prefabMoved = 0;
                foreach (string prefabPath in LegacyPrefabs)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(prefabPath) == null) continue;
                    string fileName = Path.GetFileName(prefabPath);
                    string dst = $"{ArchivedPrefabsRoot}/{fileName}";
                    if (AssetDatabase.LoadMainAssetAtPath(dst) != null)
                    {
                        // Idempotent re-run — drop the duplicate at the source.
                        AssetDatabase.DeleteAsset(prefabPath);
                        prefabMoved++;
                        continue;
                    }
                    string err = AssetDatabase.MoveAsset(prefabPath, dst);
                    if (string.IsNullOrEmpty(err))
                    {
                        prefabMoved++;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[ArchivePreCore60Folders] Failed to move '{prefabPath}' -> '{dst}': {err}");
                    }
                }
                if (prefabMoved > 0)
                {
                    perScopeCounts["Prefabs"] = prefabMoved;
                    total += prefabMoved;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (total > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[ArchivePreCore60Folders] Archived {total} asset(s) total: ");
                bool first = true;
                foreach (var kv in perScopeCounts)
                {
                    if (!first) sb.Append(", ");
                    sb.Append($"{kv.Key}={kv.Value}");
                    first = false;
                }
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.Log("[ArchivePreCore60Folders] Nothing to archive.");
            }
            return total;
        }

        // Walk srcFolder; for every subdirectory that exists in the source, ensure a
        // matching folder exists in the asset database at dstFolder. Called BEFORE
        // StartAssetEditing so MoveAsset later finds the parent dirs registered.
        private static void PrecreateFolderTreeMirror(string srcFolder, string dstFolder)
        {
            string absSrc = Path.GetFullPath(srcFolder);
            if (!Directory.Exists(absSrc)) return;
            EnsureFolder(dstFolder);
            foreach (string subDir in Directory.GetDirectories(absSrc))
            {
                string subName = Path.GetFileName(subDir);
                string nextSrc = $"{srcFolder}/{subName}";
                string nextDst = $"{dstFolder}/{subName}";
                PrecreateFolderTreeMirror(nextSrc, nextDst);
            }
        }

        // Recursively sweep every .asset under srcFolder into the parallel structure
        // under dstFolder. Preserves subfolder layout (e.g. Items/Tools/* -> Legacy/Items/Tools/*).
        // Caller must have already pre-created the dst folder tree.
        private static int SweepFolderRecursive(string srcFolder, string dstFolder)
        {
            string absSrc = Path.GetFullPath(srcFolder);
            if (!Directory.Exists(absSrc)) return 0;

            int moved = 0;

            // .asset files at this level
            foreach (string filePath in Directory.GetFiles(absSrc, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                string srcAssetPath = $"{srcFolder}/{fileName}";
                string dstAssetPath = $"{dstFolder}/{fileName}";

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
                        $"[ArchivePreCore60Folders] Failed to move '{srcAssetPath}' -> '{dstAssetPath}': {err}");
                }
            }

            // Subdirectories
            foreach (string subDir in Directory.GetDirectories(absSrc))
            {
                string subName = Path.GetFileName(subDir);
                string nextSrc = $"{srcFolder}/{subName}";
                string nextDst = $"{dstFolder}/{subName}";
                moved += SweepFolderRecursive(nextSrc, nextDst);
            }

            return moved;
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
