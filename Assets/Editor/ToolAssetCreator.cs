using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that creates the three starter pickaxe ToolDefinition assets
/// under Assets/ScriptableObjects/Items/Tools/ and registers them in the ItemDatabase.
///
/// Run via: Voidborne > Create Tool Assets
/// </summary>
public static class ToolAssetCreator
{
    private const string ToolsFolder      = "Assets/ScriptableObjects/Items/Tools";
    private const string ItemsParentFolder = "Assets/ScriptableObjects/Items";
    private const string ScriptableObjectsFolder = "Assets/ScriptableObjects";

    [MenuItem("Voidborne/Create Tool Assets")]
    public static void CreateToolAssets()
    {
        // Ensure folder hierarchy exists
        if (!AssetDatabase.IsValidFolder(ScriptableObjectsFolder))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        if (!AssetDatabase.IsValidFolder(ItemsParentFolder))
            AssetDatabase.CreateFolder(ScriptableObjectsFolder, "Items");

        if (!AssetDatabase.IsValidFolder(ToolsFolder))
            AssetDatabase.CreateFolder(ItemsParentFolder, "Tools");

        // Create Wood Pickaxe
        ToolDefinition woodPickaxe = CreateOrLoad("WoodPickaxe.asset");
        woodPickaxe.itemId               = "wood_pickaxe";
        woodPickaxe.displayName          = "Wooden Pickaxe";
        woodPickaxe.description          = "A crude wooden pickaxe. Can mine stone and dirt but cannot harvest ores.";
        woodPickaxe.itemType             = ItemType.Tool;
        woodPickaxe.maxStackSize         = 1;
        woodPickaxe.weight               = 2f;
        woodPickaxe.toolType             = ToolType.Pickaxe;
        woodPickaxe.toolTier             = ToolTier.Wood;
        woodPickaxe.miningSpeedMultiplier = 0.7f;
        woodPickaxe.maxDurability        = 30;
        EditorUtility.SetDirty(woodPickaxe);

        // Create Stone Pickaxe
        ToolDefinition stonePickaxe = CreateOrLoad("StonePickaxe.asset");
        stonePickaxe.itemId               = "stone_pickaxe";
        stonePickaxe.displayName          = "Stone Pickaxe";
        stonePickaxe.description          = "A sturdy stone pickaxe. Can mine iron ore and below.";
        stonePickaxe.itemType             = ItemType.Tool;
        stonePickaxe.maxStackSize         = 1;
        stonePickaxe.weight               = 3f;
        stonePickaxe.toolType             = ToolType.Pickaxe;
        stonePickaxe.toolTier             = ToolTier.Stone;
        stonePickaxe.miningSpeedMultiplier = 1.0f;
        stonePickaxe.maxDurability        = 60;
        EditorUtility.SetDirty(stonePickaxe);

        // Create Iron Pickaxe
        ToolDefinition ironPickaxe = CreateOrLoad("IronPickaxe.asset");
        ironPickaxe.itemId               = "iron_pickaxe";
        ironPickaxe.displayName          = "Iron Pickaxe";
        ironPickaxe.description          = "A reliable iron pickaxe. Can mine copper ore, coal, and below.";
        ironPickaxe.itemType             = ItemType.Tool;
        ironPickaxe.maxStackSize         = 1;
        ironPickaxe.weight               = 4f;
        ironPickaxe.toolType             = ToolType.Pickaxe;
        ironPickaxe.toolTier             = ToolTier.Iron;
        ironPickaxe.miningSpeedMultiplier = 1.5f;
        ironPickaxe.maxDurability        = 120;
        EditorUtility.SetDirty(ironPickaxe);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Register in ItemDatabase
        RegisterInItemDatabase(woodPickaxe, stonePickaxe, ironPickaxe);

        Debug.Log("[ToolAssetCreator] Created WoodPickaxe, StonePickaxe, IronPickaxe in Assets/ScriptableObjects/Items/Tools/");
        EditorUtility.DisplayDialog("Tool Assets Created",
            "Successfully created:\n" +
            "  WoodPickaxe.asset\n  StonePickaxe.asset\n  IronPickaxe.asset\n\n" +
            "All three have been added to the ItemDatabase.",
            "OK");
    }

    /// <summary>
    /// Finds the ItemDatabase asset and adds the three tool definitions to it
    /// if they are not already present. Uses SerializedObject so changes are
    /// properly tracked by the AssetDatabase.
    /// </summary>
    private static void RegisterInItemDatabase(params ToolDefinition[] tools)
    {
        // Find the ItemDatabase asset
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[ToolAssetCreator] ItemDatabase asset not found. " +
                             "Please add the tool items to ItemDatabase manually.");
            return;
        }

        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null)
        {
            Debug.LogWarning($"[ToolAssetCreator] Could not load ItemDatabase at '{dbPath}'.");
            return;
        }

        SerializedObject serializedDb = new SerializedObject(db);
        SerializedProperty itemsProp  = serializedDb.FindProperty("items");

        foreach (ToolDefinition tool in tools)
        {
            // Check if already registered (avoid duplicates)
            bool alreadyPresent = false;
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                SerializedProperty element = itemsProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == tool)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                int newIndex = itemsProp.arraySize;
                itemsProp.InsertArrayElementAtIndex(newIndex);
                SerializedProperty newElement = itemsProp.GetArrayElementAtIndex(newIndex);
                newElement.objectReferenceValue = tool;
                Debug.Log($"[ToolAssetCreator] Added '{tool.itemId}' to ItemDatabase.");
            }
            else
            {
                Debug.Log($"[ToolAssetCreator] '{tool.itemId}' already in ItemDatabase — skipped.");
            }
        }

        serializedDb.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static ToolDefinition CreateOrLoad(string fileName)
    {
        string assetPath = $"{ToolsFolder}/{fileName}";
        ToolDefinition existing = AssetDatabase.LoadAssetAtPath<ToolDefinition>(assetPath);
        if (existing != null)
            return existing;

        ToolDefinition instance = ScriptableObject.CreateInstance<ToolDefinition>();
        AssetDatabase.CreateAsset(instance, assetPath);
        return instance;
    }
}
