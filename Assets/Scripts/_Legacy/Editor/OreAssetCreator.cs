// LEGACY — Volume 5.3. Editor-only utility that built the 6 pre-Core-60
// ore definition assets at Assets/ScriptableObjects/Ores/. Those assets
// were archived to _Archived/Legacy/Ores/ in V5.1; this script is
// preserved for historical reference + GUID stability only. The Core-60
// ore set will be regenerated from items_core.json + biome rework (V12).
// Do not invoke. The "Voidborne/Create Ore Assets" menu it registered
// is intentionally still available so the editor can find legacy assets
// if a designer needs to rebuild the V1-era ore set.
using System.IO;
using UnityEditor;
using UnityEngine;
using Voidborne.World.Generation;

/// <summary>
/// Editor utility that creates all 6 ore definition assets and the OreRegistry asset
/// under Assets/ScriptableObjects/Ores/.
/// Run via: Voidborne > Create Ore Assets
/// </summary>
public static class OreAssetCreator
{
    private const string OresFolder = "Assets/ScriptableObjects/Ores";
    private const string ItemsFolder = "Assets/ScriptableObjects/Items";

    [MenuItem("Voidborne/Create Ore Assets")]
    public static void CreateOreAssets()
    {
        // Ensure the Ores folder exists.
        if (!AssetDatabase.IsValidFolder(OresFolder))
        {
            string parentFolder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(parentFolder))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            AssetDatabase.CreateFolder(parentFolder, "Ores");
        }

        // Load existing item definitions (may be null if not yet created).
        ItemDefinition ironOreItem     = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/IronOre.asset");
        ItemDefinition copperOreItem   = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/CopperOre.asset");
        ItemDefinition coalItem        = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/Coal.asset");

        // Gold, Titanium, Diamond item assets do not exist yet — leave associatedItem null.
        // They can be wired up when those item assets are created in a later chunk.

        // Create ore definitions.
        OreDefinition iron = CreateOrLoad<OreDefinition>("IronOre.asset");
        iron.oreTypeId       = 1;
        iron.oreName         = "Iron Ore";
        iron.associatedItem  = ironOreItem;
        iron.colorTint       = new Color(0.5f, 0.3f, 0.2f, 1f);
        iron.minY            = -200f;
        iron.maxY            = 50f;
        iron.noiseFrequency  = 0.04f;
        iron.noiseThreshold  = 0.75f;
        iron.requiredBiomeId = 0;
        EditorUtility.SetDirty(iron);

        OreDefinition copper = CreateOrLoad<OreDefinition>("CopperOre.asset");
        copper.oreTypeId       = 2;
        copper.oreName         = "Copper Ore";
        copper.associatedItem  = copperOreItem;
        copper.colorTint       = new Color(0.8f, 0.4f, 0.1f, 1f);
        copper.minY            = -100f;
        copper.maxY            = 80f;
        copper.noiseFrequency  = 0.045f;
        copper.noiseThreshold  = 0.77f;
        copper.requiredBiomeId = 0;
        EditorUtility.SetDirty(copper);

        OreDefinition coal = CreateOrLoad<OreDefinition>("CoalOre.asset");
        coal.oreTypeId       = 3;
        coal.oreName         = "Coal Ore";
        coal.associatedItem  = coalItem;
        coal.colorTint       = new Color(0.1f, 0.1f, 0.1f, 1f);
        coal.minY            = -300f;
        coal.maxY            = 60f;
        coal.noiseFrequency  = 0.035f;
        coal.noiseThreshold  = 0.73f;
        coal.requiredBiomeId = 0;
        EditorUtility.SetDirty(coal);

        OreDefinition gold = CreateOrLoad<OreDefinition>("GoldOre.asset");
        gold.oreTypeId       = 4;
        gold.oreName         = "Gold Ore";
        gold.associatedItem  = null; // No item asset yet
        gold.colorTint       = new Color(1.0f, 0.85f, 0.0f, 1f);
        gold.minY            = -400f;
        gold.maxY            = -20f;
        gold.noiseFrequency  = 0.05f;
        gold.noiseThreshold  = 0.82f;
        gold.requiredBiomeId = 0;
        EditorUtility.SetDirty(gold);

        OreDefinition titanium = CreateOrLoad<OreDefinition>("TitaniumOre.asset");
        titanium.oreTypeId       = 5;
        titanium.oreName         = "Titanium Ore";
        titanium.associatedItem  = null; // No item asset yet
        titanium.colorTint       = new Color(0.7f, 0.8f, 0.9f, 1f);
        titanium.minY            = -600f;
        titanium.maxY            = -100f;
        titanium.noiseFrequency  = 0.03f;
        titanium.noiseThreshold  = 0.84f;
        titanium.requiredBiomeId = 0;
        EditorUtility.SetDirty(titanium);

        OreDefinition diamond = CreateOrLoad<OreDefinition>("DiamondOre.asset");
        diamond.oreTypeId       = 6;
        diamond.oreName         = "Diamond Ore";
        diamond.associatedItem  = null; // No item asset yet
        diamond.colorTint       = new Color(0.5f, 0.9f, 1.0f, 1f);
        diamond.minY            = -800f;
        diamond.maxY            = -300f;
        diamond.noiseFrequency  = 0.06f;
        diamond.noiseThreshold  = 0.88f;
        diamond.requiredBiomeId = 0;
        EditorUtility.SetDirty(diamond);

        // Create OreRegistry asset.
        OreRegistry registry = CreateOrLoad<OreRegistry>("OreRegistry.asset");
        registry.oreDefinitions = new OreDefinition[] { iron, copper, coal, gold, titanium, diamond };
        EditorUtility.SetDirty(registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[OreAssetCreator] Created 6 ore definitions and OreRegistry in Assets/ScriptableObjects/Ores/");
        EditorUtility.DisplayDialog("Ore Assets Created",
            "Successfully created:\n" +
            "  IronOre.asset\n  CopperOre.asset\n  CoalOre.asset\n" +
            "  GoldOre.asset\n  TitaniumOre.asset\n  DiamondOre.asset\n  OreRegistry.asset",
            "OK");
    }

    private static T CreateOrLoad<T>(string fileName) where T : ScriptableObject
    {
        string assetPath = $"{OresFolder}/{fileName}";
        T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
            return existing;

        T instance = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(instance, assetPath);
        return instance;
    }
}
