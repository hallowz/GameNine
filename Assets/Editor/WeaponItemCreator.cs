using UnityEngine;
using UnityEditor;
using Voidborne.Combat;

/// <summary>
/// Editor utility that creates one WeaponItem ScriptableObject for each starter GunDefinition
/// and registers all five in the ItemDatabase.
///
/// Run via: Voidborne > Create Weapon Items
///
/// Output folder: Assets/ScriptableObjects/Items/Weapons/
/// Input assets:  Assets/ScriptableObjects/Guns/
/// </summary>
public static class WeaponItemCreator
{
    private const string GunsFolder        = "Assets/ScriptableObjects/Guns";
    private const string WeaponsFolder     = "Assets/ScriptableObjects/Items/Weapons";
    private const string ItemsParentFolder = "Assets/ScriptableObjects/Items";
    private const string SOFolder          = "Assets/ScriptableObjects";

    [MenuItem("Voidborne/Create Weapon Items")]
    public static void CreateWeaponItems()
    {
        // ----------------------------------------------------------------
        // Ensure folder hierarchy exists
        // ----------------------------------------------------------------
        if (!AssetDatabase.IsValidFolder(SOFolder))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        if (!AssetDatabase.IsValidFolder(ItemsParentFolder))
            AssetDatabase.CreateFolder(SOFolder, "Items");

        if (!AssetDatabase.IsValidFolder(WeaponsFolder))
            AssetDatabase.CreateFolder(ItemsParentFolder, "Weapons");

        // ----------------------------------------------------------------
        // Load GunDefinition assets
        // ----------------------------------------------------------------
        GunDefinition revolver    = LoadGun("HandmadeRevolver");
        GunDefinition rattler     = LoadGun("Rattler_SMG");
        GunDefinition ironbark    = LoadGun("Ironbark_AR");
        GunDefinition shotgun     = LoadGun("PumpShotgun");
        GunDefinition sniper      = LoadGun("BoltSniper");

        // ----------------------------------------------------------------
        // Create / update WeaponItem assets
        // ----------------------------------------------------------------
        WeaponItem wi_revolver = CreateOrLoad("Revolver.asset");
        wi_revolver.itemId        = "revolver";
        wi_revolver.displayName   = revolver != null ? revolver.gunName : "Handmade Revolver";
        wi_revolver.description   = "A crude but reliable six-shooter. Hits hard, reloads slow.";
        wi_revolver.itemType      = ItemType.Weapon;
        wi_revolver.maxStackSize  = 1;
        wi_revolver.weight        = 2f;
        wi_revolver.gunDefinition = revolver;
        EditorUtility.SetDirty(wi_revolver);

        WeaponItem wi_rattler = CreateOrLoad("Rattler_SMG.asset");
        wi_rattler.itemId        = "rattler_smg";
        wi_rattler.displayName   = rattler != null ? rattler.gunName : "Rattler SMG";
        wi_rattler.description   = "A rattling submachine gun. Low damage per shot, devastating up close.";
        wi_rattler.itemType      = ItemType.Weapon;
        wi_rattler.maxStackSize  = 1;
        wi_rattler.weight        = 2f;
        wi_rattler.gunDefinition = rattler;
        EditorUtility.SetDirty(wi_rattler);

        WeaponItem wi_ironbark = CreateOrLoad("Ironbark_AR.asset");
        wi_ironbark.itemId        = "ironbark_ar";
        wi_ironbark.displayName   = ironbark != null ? ironbark.gunName : "Ironbark AR";
        wi_ironbark.description   = "A sturdy assault rifle carved from ironbark. Versatile mid-range fighter.";
        wi_ironbark.itemType      = ItemType.Weapon;
        wi_ironbark.maxStackSize  = 1;
        wi_ironbark.weight        = 3f;
        wi_ironbark.gunDefinition = ironbark;
        EditorUtility.SetDirty(wi_ironbark);

        WeaponItem wi_shotgun = CreateOrLoad("PumpShotgun.asset");
        wi_shotgun.itemId        = "pump_shotgun";
        wi_shotgun.displayName   = shotgun != null ? shotgun.gunName : "Pump Shotgun";
        wi_shotgun.description   = "Eight pellets per pull. Brutal at close range; useless at distance.";
        wi_shotgun.itemType      = ItemType.Weapon;
        wi_shotgun.maxStackSize  = 1;
        wi_shotgun.weight        = 4f;
        wi_shotgun.gunDefinition = shotgun;
        EditorUtility.SetDirty(wi_shotgun);

        WeaponItem wi_sniper = CreateOrLoad("BoltSniper.asset");
        wi_sniper.itemId        = "bolt_sniper";
        wi_sniper.displayName   = sniper != null ? sniper.gunName : "Bolt Sniper";
        wi_sniper.description   = "One shot, one kill. Extreme range and penetration; demands patience.";
        wi_sniper.itemType      = ItemType.Weapon;
        wi_sniper.maxStackSize  = 1;
        wi_sniper.weight        = 5f;
        wi_sniper.gunDefinition = sniper;
        EditorUtility.SetDirty(wi_sniper);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ----------------------------------------------------------------
        // Register all five in the ItemDatabase
        // ----------------------------------------------------------------
        RegisterInItemDatabase(wi_revolver, wi_rattler, wi_ironbark, wi_shotgun, wi_sniper);

        Debug.Log("[WeaponItemCreator] Created 5 WeaponItem assets in " + WeaponsFolder +
                  " and registered them in the ItemDatabase.");

        EditorUtility.DisplayDialog("Weapon Items Created",
            "Successfully created:\n" +
            "  Revolver.asset\n" +
            "  Rattler_SMG.asset\n" +
            "  Ironbark_AR.asset\n" +
            "  PumpShotgun.asset\n" +
            "  BoltSniper.asset\n\n" +
            "All five have been added to the ItemDatabase.",
            "OK");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GunDefinition LoadGun(string assetName)
    {
        string path = $"{GunsFolder}/{assetName}.asset";
        GunDefinition def = AssetDatabase.LoadAssetAtPath<GunDefinition>(path);
        if (def == null)
            Debug.LogWarning($"[WeaponItemCreator] GunDefinition not found at '{path}'. " +
                             "Run Voidborne > Create Starter Guns first.");
        return def;
    }

    private static WeaponItem CreateOrLoad(string fileName)
    {
        string assetPath = $"{WeaponsFolder}/{fileName}";
        WeaponItem existing = AssetDatabase.LoadAssetAtPath<WeaponItem>(assetPath);
        if (existing != null)
            return existing;

        WeaponItem instance = ScriptableObject.CreateInstance<WeaponItem>();
        AssetDatabase.CreateAsset(instance, assetPath);
        return instance;
    }

    /// <summary>
    /// Finds the ItemDatabase asset and appends any of the given WeaponItems that are
    /// not already present. Uses SerializedObject so changes are properly tracked.
    /// </summary>
    private static void RegisterInItemDatabase(params WeaponItem[] weapons)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[WeaponItemCreator] ItemDatabase asset not found. " +
                             "Please add the weapon items to ItemDatabase manually.");
            return;
        }

        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null)
        {
            Debug.LogWarning($"[WeaponItemCreator] Could not load ItemDatabase at '{dbPath}'.");
            return;
        }

        SerializedObject serializedDb = new SerializedObject(db);
        SerializedProperty itemsProp  = serializedDb.FindProperty("items");

        foreach (WeaponItem weapon in weapons)
        {
            if (weapon == null) continue;

            bool alreadyPresent = false;
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                SerializedProperty element = itemsProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == weapon)
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
                newElement.objectReferenceValue = weapon;
                Debug.Log($"[WeaponItemCreator] Added '{weapon.itemId}' to ItemDatabase.");
            }
            else
            {
                Debug.Log($"[WeaponItemCreator] '{weapon.itemId}' already in ItemDatabase — skipped.");
            }
        }

        serializedDb.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }
}
