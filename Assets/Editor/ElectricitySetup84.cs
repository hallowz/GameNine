#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Voidborne.Building.Electricity;

/// <summary>
/// Editor setup script for Vol 8.4 — Power Grid (Advanced Generators &amp; Void Filament).
///
/// Menu: Voidborne > Setup Power Grid Vol 8.4
///
/// Creates:
///   • SolarPanel prefab + ElectricityItem SO
///   • VoidFilamentGenerator prefab + ElectricityItem SO
///   • DayNightCycle prefab (standalone — add to scene manually)
///   Registers both items in ItemDatabase.
/// </summary>
public static class ElectricitySetup84
{
    private const string PrefabFolder = "Assets/Prefabs/Building/Electricity";
    private const string ItemFolder   = "Assets/ScriptableObjects/Items/Electricity";

    [MenuItem("Voidborne/Setup Power Grid Vol 8.4")]
    public static void SetupPowerGridVol84()
    {
        EnsureFolders();

        // ── Solar Panel ────────────────────────────────────────────────────────
        var solarPrefab = CreateSolarPanelPrefab();
        var solarItem   = CreateElectricityItem(
            "SolarPanelItem",
            "Solar Panel",
            "Generates 75W during daytime using solar energy. Requires surface placement with clear sky.",
            solarPrefab,
            outputWatts: 75f,
            drawWatts:   0f);

        // ── Void Filament Generator ────────────────────────────────────────────
        var voidPrefab = CreateVoidFilamentPrefab();
        var voidItem   = CreateElectricityItem(
            "VoidFilamentItem",
            "Void Filament Generator",
            "Channels 2000W from the deep void. Abyssal zone only (Y < -512). Attracts VORD patrols after 5 minutes.",
            voidPrefab,
            outputWatts: 2000f,
            drawWatts:   0f);

        // ── Day/Night Cycle prefab ─────────────────────────────────────────────
        CreateDayNightCyclePrefab();

        // ── Register items ─────────────────────────────────────────────────────
        RegisterInDatabase(solarItem);
        RegisterInDatabase(voidItem);

        AssetDatabase.SaveAssets();
        Debug.Log("[ElectricitySetup84] Vol 8.4 power grid assets created.");
        EditorUtility.DisplayDialog("Vol 8.4 Setup",
            "Solar Panel and Void Filament Generator created.\n\n" +
            "Next steps:\n" +
            "1. Add the DayNightCycle prefab (Assets/Prefabs/Building/Electricity/DayNightCycle) to your scene.\n" +
            "2. Assign a directional Sun Light to the DayNightCycle component.\n" +
            "3. Assign a VORD EnemyDefinition to the VoidFilamentGenerator prefab's 'vordPatrolDefinition' field.\n" +
            "4. Place SolarPanels on the surface, VoidFilament deep underground (Y < -512).",
            "OK");
    }

    // ── Solar Panel prefab ─────────────────────────────────────────────────────

    private static GameObject CreateSolarPanelPrefab()
    {
        const string path = PrefabFolder + "/SolarPanel.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[ElectricitySetup84] SolarPanel.prefab already exists — skipping.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = new GameObject("SolarPanel");

        // Frame
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Frame";
        frame.transform.SetParent(root.transform, false);
        frame.transform.localScale    = new Vector3(2f, 0.08f, 1.2f);
        frame.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        SetColor(frame, new Color(0.25f, 0.25f, 0.3f));

        // Panel face (the photovoltaic surface)
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "PanelFace";
        panel.transform.SetParent(root.transform, false);
        panel.transform.localScale    = new Vector3(1.9f, 0.02f, 1.1f);
        panel.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        SetColor(panel, new Color(0.05f, 0.15f, 0.55f)); // dark blue solar cell

        // Support pole
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(root.transform, false);
        pole.transform.localScale    = new Vector3(0.08f, 0.45f, 0.08f);
        pole.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        SetColor(pole, new Color(0.3f, 0.3f, 0.3f));

        // SolarPanel component
        var sp = root.AddComponent<SolarPanel>();
        sp.powerOutput    = 75f;
        sp.outputWatts    = 75f;
        sp.minimumSurfaceY = 0f;

        // Cache panel renderer on component
        sp.panelRenderer = panel.GetComponent<Renderer>();

        // PowerNode base fields
        sp.powerDraw   = 0f;
        sp.priority    = 0;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ── Void Filament Generator prefab ────────────────────────────────────────

    private static GameObject CreateVoidFilamentPrefab()
    {
        const string path = PrefabFolder + "/VoidFilamentGenerator.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("[ElectricitySetup84] VoidFilamentGenerator.prefab already exists — skipping.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = new GameObject("VoidFilamentGenerator");

        // Base platform (2×2 footprint)
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "Base";
        basePlate.transform.SetParent(root.transform, false);
        basePlate.transform.localScale    = new Vector3(2f, 0.15f, 2f);
        basePlate.transform.localPosition = new Vector3(0f, 0.075f, 0f);
        SetColor(basePlate, new Color(0.05f, 0.02f, 0.12f));

        // Central column
        var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = "Column";
        column.transform.SetParent(root.transform, false);
        column.transform.localScale    = new Vector3(0.3f, 1.0f, 0.3f);
        column.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        SetColor(column, new Color(0.12f, 0.04f, 0.25f));

        // Filament orb
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "FilamentOrb";
        orb.transform.SetParent(root.transform, false);
        orb.transform.localScale    = Vector3.one * 0.7f;
        orb.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        SetEmissiveColor(orb, new Color(0.5f, 0f, 1.2f)); // emissive purple

        // Glow light
        var glowGo = new GameObject("GlowLight");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        var glow = glowGo.AddComponent<Light>();
        glow.type      = LightType.Point;
        glow.color     = new Color(0.3f, 0f, 1f);
        glow.intensity = 3f;
        glow.range     = 12f;

        // VoidFilamentGenerator component
        var vfg = root.AddComponent<VoidFilamentGenerator>();
        vfg.powerOutput        = 2000f;
        vfg.outputWatts        = 2000f;
        vfg.maximumPlacementY  = -512f;
        vfg.vordAlertDelay     = 300f;
        vfg.filamentGlow       = glow;
        vfg.glowColor          = new Color(0.3f, 0f, 1f);
        vfg.powerDraw          = 0f;
        vfg.priority           = 0;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ── DayNightCycle prefab ───────────────────────────────────────────────────

    private static void CreateDayNightCyclePrefab()
    {
        const string path = PrefabFolder + "/DayNightCycle.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("DayNightCycle");
        var dnc = root.AddComponent<DayNightCycle>();
        dnc.dayLengthSeconds = 1440f;
        dnc.startTimeOfDay   = 0.3f;
        dnc.dayIntensity     = 1.2f;
        // sunLight left null — must be assigned in scene

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[ElectricitySetup84] DayNightCycle.prefab created.");
    }

    // ── ElectricityItem SO ────────────────────────────────────────────────────

    private static ElectricityItem CreateElectricityItem(
        string fileName, string displayName, string description,
        GameObject prefab, float outputWatts, float drawWatts)
    {
        string path = $"{ItemFolder}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ElectricityItem>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<ElectricityItem>();
        item.itemId       = fileName;
        item.displayName  = displayName;
        item.description  = description;
        item.devicePrefab = prefab;
        item.outputWatts  = outputWatts;
        item.drawWatts    = drawWatts;
        item.maxStackSize = 1;

        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    // ── Database registration ─────────────────────────────────────────────────

    private static void RegisterInDatabase(ItemDefinition item)
    {
        if (item == null) return;
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0) return;
        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null) return;
        foreach (var e in db.items)
            if (e != null && e.itemId == item.itemId) return;
        db.items.Add(item);
        EditorUtility.SetDirty(db);
    }

    // ── Folder helpers ────────────────────────────────────────────────────────

    private static void EnsureFolders()
    {
        foreach (var (parent, child) in new (string, string)[]
        {
            ("Assets", "Prefabs"),
            ("Assets/Prefabs", "Building"),
            ("Assets/Prefabs/Building", "Electricity"),
            ("Assets", "ScriptableObjects"),
            ("Assets/ScriptableObjects", "Items"),
            ("Assets/ScriptableObjects/Items", "Electricity"),
        })
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = c;
        r.sharedMaterial = mat;
    }

    private static void SetEmissiveColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = c;
        mat.SetColor("_EmissionColor", c);
        mat.EnableKeyword("_EMISSION");
        r.sharedMaterial = mat;
    }
}
#endif
