#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: Voidborne > Setup Backpacks Vol 7.3
/// Creates four BackpackItem ScriptableObject assets and registers them in ItemDatabase.
/// </summary>
public static class BackpackSetup
{
    private const string BackpackDir = "Assets/ScriptableObjects/Backpacks";

    [MenuItem("Voidborne/Setup Backpacks Vol 7.3")]
    public static void Setup()
    {
        if (!Directory.Exists(BackpackDir))
        {
            Directory.CreateDirectory(BackpackDir);
            AssetDatabase.Refresh();
        }

        var satchel       = CreateBackpack("Satchel",        "satchel",        3, 3, 1.2f,
            "A simple satchel stitched from rough hide. Cramped but better than nothing.");

        var travelPack    = CreateBackpack("Travel Pack",    "travel_pack",    5, 4, 2.4f,
            "Reinforced fabric panels and iron buckles. A reliable companion for long expeditions.");

        var expeditionRig = CreateBackpack("Expedition Rig", "expedition_rig", 6, 5, 4.0f,
            "Titanium frame with treated leather. Carries everything you need — and then some.");

        var voidPocket    = CreateBackpack("Void Pocket",    "void_pocket",    6, 6, 1.0f,
            "Woven from Dimensional Fabric around a Void Crystal core. Space folds inside it.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RegisterItems(satchel, travelPack, expeditionRig, voidPocket);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BackpackSetup] 4 backpack assets created and registered in ItemDatabase.");
        EditorUtility.DisplayDialog("Backpacks Created",
            "4 BackpackItem assets created and registered:\n• Satchel (3×3)\n• Travel Pack (4×5)\n• Expedition Rig (5×6)\n• Void Pocket (6×6)", "OK");
    }

    // ── Asset creation ─────────────────────────────────────────────────────

    private static BackpackItem CreateBackpack(string displayName, string itemId,
                                               int rows, int cols, float weight,
                                               string description)
    {
        string assetPath = $"{BackpackDir}/{itemId}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BackpackItem>(assetPath);
        if (existing != null) return existing;

        var bp           = ScriptableObject.CreateInstance<BackpackItem>();
        bp.displayName   = displayName;
        bp.itemId        = itemId;
        bp.description   = description;
        bp.itemType      = ItemType.Backpack;
        bp.maxStackSize  = 1;
        bp.weight        = weight;
        bp.extraRows     = rows;
        bp.extraColumns  = cols;

        AssetDatabase.CreateAsset(bp, assetPath);
        return bp;
    }

    // ── ItemDatabase registration ──────────────────────────────────────────

    private static void RegisterItems(params ItemDefinition[] newItems)
    {
        var db = ItemDatabase.GetOrLoad();
        if (db == null)
        {
            Debug.LogWarning("[BackpackSetup] ItemDatabase not found in Resources. Register backpack items manually.");
            return;
        }

        bool changed = false;
        foreach (var item in newItems)
        {
            if (item == null) continue;
            bool found = false;
            foreach (var existing in db.items)
                if (existing != null && existing.itemId == item.itemId) { found = true; break; }

            if (!found)
            {
                db.items.Add(item);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(db);
            Debug.Log("[BackpackSetup] Registered backpack items in ItemDatabase.");
        }
    }
}
#endif
