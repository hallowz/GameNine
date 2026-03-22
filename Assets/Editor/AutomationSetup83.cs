#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Voidborne.Automation;
using Voidborne.Building.Electricity;

/// <summary>
/// Editor setup script for Vol 8.3 — Machines (Electric Furnace, Grinder, Press, Assembler,
/// Tier 2: Circuit Etcher, Component Press, Battery Fabricator).
///
/// Menu: Voidborne > Setup Machines Vol 8.3
///
/// Creates:
///   Tier 1: ElectricFurnace, Grinder, Press, Assembler — prefabs + item SOs
///   Tier 2: CircuitEtcher, ComponentPress, BatteryFabricator — prefabs + item SOs
///   Example recipes: 2 SmeltingRecipes (Iron Ore, Iron Dust), 2 GrinderRecipes, 2 PressRecipes
///   Registers all items in ItemDatabase.
/// </summary>
public static class AutomationSetup83
{
    private const string PrefabFolder  = "Assets/Prefabs/Automation";
    private const string ItemFolder    = "Assets/ScriptableObjects/Items/Automation";
    private const string RecipeFolder  = "Assets/ScriptableObjects/Recipes/Machines";

    [MenuItem("Voidborne/Setup Machines Vol 8.3")]
    public static void SetupMachinesVol83()
    {
        EnsureFolders();

        // ── Tier 1 prefabs ─────────────────────────────────────────────────
        var efPrefab  = CreateMachinePrefab<ElectricFurnace>("ElectricFurnace",
            new Color(0.7f, 0.35f, 0.1f), new Color(0.9f, 0.6f, 0.1f), 60f);

        var grPrefab  = CreateMachinePrefab<Grinder>("Grinder",
            new Color(0.3f, 0.3f, 0.3f), new Color(0.5f, 0.5f, 0.2f), 40f);

        var prPrefab  = CreateMachinePrefab<Press>("Press",
            new Color(0.2f, 0.3f, 0.5f), new Color(0.4f, 0.4f, 0.7f), 50f);

        var asPrefab  = CreateMachinePrefab<Assembler>("Assembler",
            new Color(0.1f, 0.4f, 0.3f), new Color(0.2f, 0.7f, 0.5f), 75f);

        // ── Tier 2 prefabs ─────────────────────────────────────────────────
        var cePrefab  = CreateMachinePrefab<CircuitEtcher>("CircuitEtcher",
            new Color(0.1f, 0.1f, 0.5f), new Color(0.0f, 0.6f, 1.0f), 100f);

        var cpPrefab  = CreateMachinePrefab<ComponentPress>("ComponentPress",
            new Color(0.2f, 0.1f, 0.4f), new Color(0.6f, 0.2f, 0.8f), 70f);

        var bfPrefab  = CreateMachinePrefab<BatteryFabricator>("BatteryFabricator",
            new Color(0.4f, 0.4f, 0.0f), new Color(0.8f, 0.8f, 0.0f), 80f);

        // ── Item SOs ───────────────────────────────────────────────────────
        var efItem = CreateMachineItem("ElectricFurnaceItem", "Electric Furnace", efPrefab,
            "Smelts ore using grid power. Faster than the manual furnace; draws 60 W.", 60f);

        var grItem = CreateMachineItem("GrinderItem", "Grinder",  grPrefab,
            "Converts raw ore into ore dust for 1.5x smelting yield. Draws 40 W.", 40f);

        var prItem = CreateMachineItem("PressItem", "Press", prPrefab,
            "Shapes ingots into Plates and Rods in bulk. Draws 50 W.", 50f);

        var asItem = CreateMachineItem("AssemblerItem", "Assembler", asPrefab,
            "Auto-crafts any recipe encoded on a Schematic Card. Draws 75 W.", 75f);

        var ceItem = CreateMachineItem("CircuitEtcherItem", "Circuit Etcher", cePrefab,
            "[Tier 2] Produces Circuit Boards (8 s each). Draws 100 W.", 100f);

        var cpItem = CreateMachineItem("ComponentPressItem", "Component Press", cpPrefab,
            "[Tier 2] Presses Capacitors, Resistors, Transistors. Draws 70 W.", 70f);

        var bfItem = CreateMachineItem("BatteryFabricatorItem", "Battery Fabricator", bfPrefab,
            "[Tier 2] Mass-produces Battery Cells. Draws 80 W.", 80f);

        // ── SchematicCard item SO ──────────────────────────────────────────
        CreateSchematicCardItem();

        // ── Example recipe SOs ─────────────────────────────────────────────
        CreateExampleRecipes();

        // ── Register in ItemDatabase ───────────────────────────────────────
        foreach (var item in new AutomationItem[] { efItem, grItem, prItem, asItem, ceItem, cpItem, bfItem })
            RegisterInDatabase(item);

        AssetDatabase.SaveAssets();
        Debug.Log("[AutomationSetup83] Vol 8.3 machine assets created.");
        EditorUtility.DisplayDialog("Vol 8.3 Setup",
            "Tier 1 & Tier 2 machine prefabs and item SOs created.\n\n" +
            "Next steps:\n" +
            "1. Assign SmeltingRecipes / GrinderRecipes / PressRecipes in each machine prefab.\n" +
            "2. Craft Schematic Cards at the Workbench and insert into Assembler.\n" +
            "3. Tier 2 machines require Architect Relic unlock — set crafting recipe requirements.",
            "OK");
    }

    // ── Machine prefab builder ─────────────────────────────────────────────

    private static GameObject CreateMachinePrefab<T>(string name, Color bodyColor, Color accentColor,
                                                      float powerDraw) where T : MonoBehaviour
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"[AutomationSetup83] {name}.prefab already exists — skipping.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = new GameObject(name);

        // Main body
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale    = new Vector3(1.5f, 1.5f, 1.5f);
        body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        SetColor(body, bodyColor);

        // Accent panel (front face indicator)
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "AccentPanel";
        panel.transform.SetParent(root.transform, false);
        panel.transform.localScale    = new Vector3(1.0f, 0.8f, 0.05f);
        panel.transform.localPosition = new Vector3(0f, 0.85f, 0.78f);
        SetColor(panel, accentColor);

        // Status light
        var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        light.name = "StatusLight";
        light.transform.SetParent(root.transform, false);
        light.transform.localScale    = Vector3.one * 0.15f;
        light.transform.localPosition = new Vector3(0.55f, 1.55f, 0f);
        SetColor(light, accentColor);

        // Input port connector
        var inPort = new GameObject("input_port");
        inPort.transform.SetParent(root.transform, false);
        inPort.transform.localPosition = new Vector3(-0.9f, 0.75f, 0f);
        var inInd = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        inInd.name = "Indicator";
        inInd.transform.SetParent(inPort.transform, false);
        inInd.transform.localScale = Vector3.one * 0.12f;
        SetColor(inInd, Color.yellow);
        var inConn = inPort.AddComponent<AutomationConnector>();
        inConn.portType  = AutomationConnector.PortType.Input;
        inConn.portId    = "input";
        inConn.indicator = inInd.GetComponent<Renderer>();

        // Output port connector
        var outPort = new GameObject("output_port");
        outPort.transform.SetParent(root.transform, false);
        outPort.transform.localPosition = new Vector3(0.9f, 0.75f, 0f);
        var outInd = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        outInd.name = "Indicator";
        outInd.transform.SetParent(outPort.transform, false);
        outInd.transform.localScale = Vector3.one * 0.12f;
        SetColor(outInd, Color.green);
        var outConn = outPort.AddComponent<AutomationConnector>();
        outConn.portType  = AutomationConnector.PortType.Output;
        outConn.portId    = "output";
        outConn.indicator = outInd.GetComponent<Renderer>();

        // PowerConsumer
        var power = root.AddComponent<PowerConsumer>();
        power.powerDraw   = powerDraw;
        power.powerOutput = 0f;
        power.priority    = 5;

        // Machine component
        root.AddComponent<T>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ── Item SO builder ────────────────────────────────────────────────────

    private static AutomationItem CreateMachineItem(string fileName, string displayName,
                                                     GameObject prefab, string description,
                                                     float powerWatts)
    {
        string path = $"{ItemFolder}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<AutomationItem>(path);
        if (existing != null) return existing;

        var item = ScriptableObject.CreateInstance<AutomationItem>();
        item.itemId          = fileName;
        item.displayName     = displayName;
        item.description     = description;
        item.devicePrefab    = prefab;
        item.powerDrawWatts  = powerWatts;
        item.maxStackSize    = 1;

        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static void CreateSchematicCardItem()
    {
        string path = $"{ItemFolder}/SchematicCardItem.asset";
        if (AssetDatabase.LoadAssetAtPath<SchematicCard>(path) != null) return;

        var card = ScriptableObject.CreateInstance<SchematicCard>();
        card.itemId       = "SchematicCardItem";
        card.displayName  = "Schematic Card";
        card.description  = "Encodes a crafting recipe. Insert into an Assembler to automate production.";
        card.maxStackSize = 1;
        AssetDatabase.CreateAsset(card, path);
        RegisterInDatabase(card);
    }

    // ── Example recipe SOs ────────────────────────────────────────────────

    private static void CreateExampleRecipes()
    {
        // Grinder recipe: placeholder (assign real ItemDefinitions in Inspector)
        CreateRecipeAsset<GrinderRecipe>("GrinderRecipe_IronOre",
            $"{RecipeFolder}/Grinder_IronOre.asset", r =>
            {
                r.processTime = 3f;
                Debug.Log("[AutomationSetup83] Created Grinder_IronOre recipe — assign inputItem/outputItem in Inspector.");
            });

        CreateRecipeAsset<GrinderRecipe>("GrinderRecipe_CopperOre",
            $"{RecipeFolder}/Grinder_CopperOre.asset", r =>
            {
                r.processTime = 3f;
            });

        // Press recipe: placeholder
        CreateRecipeAsset<PressRecipe>("PressRecipe_IronIngot_Plate",
            $"{RecipeFolder}/Press_IronIngot_Plate.asset", r =>
            {
                r.processTime    = 4f;
                r.outputQuantity = 2;
                Debug.Log("[AutomationSetup83] Created Press_IronIngot_Plate recipe — assign inputItem/outputItem in Inspector.");
            });

        CreateRecipeAsset<PressRecipe>("PressRecipe_IronIngot_Rod",
            $"{RecipeFolder}/Press_IronIngot_Rod.asset", r =>
            {
                r.processTime    = 4f;
                r.outputQuantity = 4;
            });

        // SmeltingRecipe for dust (2x yield) — placeholder
        CreateRecipeAsset<SmeltingRecipe>("SmeltingRecipe_IronDust",
            $"{RecipeFolder}/Smelt_IronDust.asset", r =>
            {
                r.smeltTime      = 2f;
                r.outputQuantity = 2;
                Debug.Log("[AutomationSetup83] Created Smelt_IronDust recipe — assign inputItem/outputItem in Inspector.");
            });

        // EtchingRecipe for Circuit Board
        CreateRecipeAsset<EtchingRecipe>("EtchingRecipe_CircuitBoard",
            $"{RecipeFolder}/Etch_CircuitBoard.asset", r =>
            {
                r.processTime    = 8f;
                r.outputQuantity = 1;
                Debug.Log("[AutomationSetup83] Created Etch_CircuitBoard recipe — assign inputItemA (Copper Plate), inputItemB (Silica), outputItem (Circuit Board) in Inspector.");
            });

        // FabricationRecipe for Battery Cell
        CreateRecipeAsset<FabricationRecipe>("FabricationRecipe_BatteryCell",
            $"{RecipeFolder}/Fab_BatteryCell.asset", r =>
            {
                r.processTime    = 5f;
                r.outputQuantity = 2;
                Debug.Log("[AutomationSetup83] Created Fab_BatteryCell recipe — assign inputItemA (Lithium), inputItemB (Copper Plate), outputItem (Battery Cell) in Inspector.");
            });
    }

    private static void CreateRecipeAsset<T>(string assetName, string path,
                                              System.Action<T> configure) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
        var recipe = ScriptableObject.CreateInstance<T>();
        recipe.name = assetName;
        configure(recipe);
        AssetDatabase.CreateAsset(recipe, path);
    }

    // ── Database registration ─────────────────────────────────────────────

    private static void RegisterInDatabase(ItemDefinition item)
    {
        if (item == null) return;
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0) return;
        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null) return;
        foreach (var existing in db.items)
            if (existing != null && existing.itemId == item.itemId) return;
        db.items.Add(item);
        EditorUtility.SetDirty(db);
    }

    // ── Folder helpers ────────────────────────────────────────────────────

    private static void EnsureFolders()
    {
        foreach (var (parent, child) in new (string, string)[]
        {
            ("Assets", "Prefabs"),
            ("Assets/Prefabs", "Automation"),
            ("Assets", "ScriptableObjects"),
            ("Assets/ScriptableObjects", "Items"),
            ("Assets/ScriptableObjects/Items", "Automation"),
            ("Assets/ScriptableObjects", "Recipes"),
            ("Assets/ScriptableObjects/Recipes", "Machines"),
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
}
#endif
