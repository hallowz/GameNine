using UnityEditor;
using UnityEngine;
using Voidborne.Vehicles;

public static class RegisterVehiclePartsEditor
{
    [MenuItem("Tools/Register Vehicle Parts in ItemDatabase")]
    public static void RegisterAll()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Resources/ItemDatabase.asset");
        if (db == null)
        {
            Debug.LogError("ItemDatabase not found at Assets/Resources/ItemDatabase.asset");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:VehiclePartItem", new[] { "Assets/ScriptableObjects/Items/VehicleParts" });
        int added = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var part = AssetDatabase.LoadAssetAtPath<VehiclePartItem>(path);
            if (part == null) continue;
            if (db.items.Contains(part)) continue;
            db.items.Add(part);
            added++;
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RegisterVehicleParts] Added {added} VehiclePartItem(s) to ItemDatabase. Total items: {db.items.Count}");
        }
        else
        {
            Debug.Log("[RegisterVehicleParts] All vehicle parts already registered.");
        }
    }
}
