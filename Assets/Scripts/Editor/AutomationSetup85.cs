#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voidborne.Automation;

/// <summary>
/// Editor setup for Volume 8.5 — Computer Terminal, Storage Drives & Scripted Automation.
/// Menu: Voidborne > Setup Terminal Vol 8.5
/// Creates:
///   - ComputerTerminal prefab
///   - DriveRack prefab
///   - StorageDrive prefab (world unit)
///   - 4 DriveItem SOs (one per category)
///   - Drive Repair Kit ItemDefinition SO
///   - NetworkCable prefab
/// </summary>
public class AutomationSetup85
{
    private const string PrefabRoot = "Assets/Prefabs/Automation";
    private const string ItemRoot   = "Assets/Items/Automation";

    [MenuItem("Voidborne/Setup Terminal Vol 8.5")]
    public static void Setup()
    {
        EnsureDirectory(PrefabRoot);
        EnsureDirectory(ItemRoot);

        CreateComputerTerminalPrefab();
        CreateDriveRackPrefab();
        CreateStorageDrivePrefab();
        CreateDriveItems();
        CreateNetworkCablePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AutomationSetup85] Vol 8.5 assets created. " +
                  "Assign DriveItem SOs to StorageDrive.driveItemDefinition fields as needed.");
    }

    // ── ComputerTerminal ───────────────────────────────────────────────────

    private static void CreateComputerTerminalPrefab()
    {
        string path = $"{PrefabRoot}/ComputerTerminal.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[AutomationSetup85] ComputerTerminal.prefab already exists — skipped.");
            return;
        }

        var go = new GameObject("ComputerTerminal");

        // Body — flat panel shape
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale    = new Vector3(0.8f, 0.6f, 0.15f);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        SetPrimitiveColor(body, new Color(0.12f, 0.13f, 0.15f));
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // Screen (glowing quad)
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.transform.SetParent(go.transform, false);
        screen.transform.localScale    = new Vector3(0.7f, 0.5f, 1f);
        screen.transform.localPosition = new Vector3(0f, 0.6f, -0.08f);
        SetPrimitiveColor(screen, new Color(0.05f, 0.35f, 0.5f));
        Object.DestroyImmediate(screen.GetComponent<Collider>());

        // Keyboard base
        var keyboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keyboard.transform.SetParent(go.transform, false);
        keyboard.transform.localScale    = new Vector3(0.7f, 0.04f, 0.35f);
        keyboard.transform.localPosition = new Vector3(0f, 0.32f, 0.3f);
        SetPrimitiveColor(keyboard, new Color(0.15f, 0.16f, 0.18f));
        Object.DestroyImmediate(keyboard.GetComponent<Collider>());

        // Stand
        var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stand.transform.SetParent(go.transform, false);
        stand.transform.localScale    = new Vector3(0.1f, 0.3f, 0.1f);
        stand.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        SetPrimitiveColor(stand, new Color(0.2f, 0.21f, 0.23f));
        Object.DestroyImmediate(stand.GetComponent<Collider>());

        // Collider
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.6f, 0f);
        col.size   = new Vector3(0.9f, 0.7f, 0.5f);

        go.AddComponent<ComputerTerminal>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[AutomationSetup85] Created ComputerTerminal.prefab");
    }

    // ── DriveRack ──────────────────────────────────────────────────────────

    private static void CreateDriveRackPrefab()
    {
        string path = $"{PrefabRoot}/DriveRack.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[AutomationSetup85] DriveRack.prefab already exists — skipped.");
            return;
        }

        var go = new GameObject("DriveRack");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale    = new Vector3(0.5f, 0.8f, 0.3f);
        body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        SetPrimitiveColor(body, new Color(0.1f, 0.11f, 0.13f));
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // 4 drive bays
        for (int i = 0; i < 4; i++)
        {
            var bay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bay.transform.SetParent(go.transform, false);
            bay.transform.localScale    = new Vector3(0.42f, 0.12f, 0.25f);
            bay.transform.localPosition = new Vector3(0f, 0.14f + i * 0.17f, -0.02f);
            SetPrimitiveColor(bay, new Color(0.18f, 0.19f, 0.22f));
            Object.DestroyImmediate(bay.GetComponent<Collider>());
        }

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.4f, 0f);
        col.size   = new Vector3(0.55f, 0.85f, 0.35f);

        go.AddComponent<DriveRack>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[AutomationSetup85] Created DriveRack.prefab");
    }

    // ── StorageDrive ───────────────────────────────────────────────────────

    private static void CreateStorageDrivePrefab()
    {
        string path = $"{PrefabRoot}/StorageDrive.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[AutomationSetup85] StorageDrive.prefab already exists — skipped.");
            return;
        }

        var go = new GameObject("StorageDrive");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale    = new Vector3(0.4f, 0.09f, 0.22f);
        body.transform.localPosition = Vector3.zero;
        SetPrimitiveColor(body, new Color(0.08f, 0.09f, 0.11f));
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // Status light
        var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        light.transform.SetParent(go.transform, false);
        light.transform.localScale    = new Vector3(0.04f, 0.04f, 0.04f);
        light.transform.localPosition = new Vector3(0.16f, 0.05f, 0.08f);
        SetPrimitiveColor(light, Color.green);
        Object.DestroyImmediate(light.GetComponent<Collider>());

        go.AddComponent<BoxCollider>().size = new Vector3(0.42f, 0.1f, 0.24f);

        var drive = go.AddComponent<StorageDrive>();
        SerializedObject so = new SerializedObject(drive);
        so.FindProperty("statusLights").arraySize = 1;
        var lightRenderer = light.GetComponent<Renderer>();
        so.FindProperty("statusLights").GetArrayElementAtIndex(0).objectReferenceValue = lightRenderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[AutomationSetup85] Created StorageDrive.prefab");
    }

    // ── DriveItem SOs ──────────────────────────────────────────────────────

    private static void CreateDriveItems()
    {
        var categories = new[]
        {
            (DriveCategory.RawMaterials, "Drive_RawMaterials", "Raw Materials Drive",
             "Stores up to 50,000 raw resource items digitally. Corruption-resistant if shielded.", 1f),
            (DriveCategory.Components,  "Drive_Components",   "Components Drive",
             "Stores up to 50,000 processed components digitally.", 1f),
            (DriveCategory.Fuel,        "Drive_Fuel",         "Fuel Drive",
             "Stores up to 50,000 fuel items digitally.", 1f),
            (DriveCategory.Misc,        "Drive_Misc",         "Misc Drive",
             "Stores up to 50,000 miscellaneous items digitally.", 1f),
        };

        foreach (var (cat, id, name, desc, w) in categories)
        {
            string path = $"{ItemRoot}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<DriveItem>(path) != null) continue;

            var item          = ScriptableObject.CreateInstance<DriveItem>();
            item.itemId       = id;
            item.displayName  = name;
            item.description  = desc;
            item.itemType     = ItemType.Machine;
            item.maxStackSize = 1;
            item.weight       = w;
            item.category     = cat;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[AutomationSetup85] Created {name} SO");
        }

        // Drive Repair Kit
        string repairPath = $"{ItemRoot}/DriveRepairKit.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(repairPath) == null)
        {
            var kit          = ScriptableObject.CreateInstance<ItemDefinition>();
            kit.itemId       = "DriveRepairKit";
            kit.displayName  = "Drive Repair Kit";
            kit.description  = "Restores a corrupted StorageDrive to working order. Does not recover lost items.";
            kit.itemType     = ItemType.Consumable;
            kit.maxStackSize = 5;
            kit.weight       = 0.5f;
            AssetDatabase.CreateAsset(kit, repairPath);
            Debug.Log("[AutomationSetup85] Created Drive Repair Kit SO");
        }
    }

    // ── NetworkCable prefab ────────────────────────────────────────────────

    private static void CreateNetworkCablePrefab()
    {
        string path = $"{PrefabRoot}/NetworkCable.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[AutomationSetup85] NetworkCable.prefab already exists — skipped.");
            return;
        }

        var go = new GameObject("NetworkCable");
        go.AddComponent<NetworkCable>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[AutomationSetup85] Created NetworkCable.prefab");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void EnsureDirectory(string assetPath)
    {
        // Walk each segment, creating any missing folder via AssetDatabase.CreateFolder.
        // AssetDatabase.CreateFolder is synchronous and immediately valid for the next call.
        string[] parts = assetPath.Split('/');
        string current = parts[0]; // always "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void SetPrimitiveColor(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        rend.sharedMaterial = new Material(Shader.Find("Standard"));
        rend.sharedMaterial.color = color;
    }
}
#endif
