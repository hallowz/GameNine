using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Voidborne.Combat.Melee;
using Voidborne.Combat;

/// <summary>
/// Editor utility that creates all 5 starter MeleeDefinition + WeaponItem ScriptableObject
/// assets in one click, then registers the WeaponItem assets in the ItemDatabase.
/// Run via: Voidborne > Create Starter Melee Weapons
///
/// Weapons created:
///   WoodenClub      — Slow, decent damage, starter. Easy to read swings.
///   IronDagger      — Fast, short range, low damage per hit; excels at feint/morph combos.
///   IronLongsword   — Balanced speed, range, and damage. The default melee weapon.
///   TitaniumSpear   — Long reach, stab-focused; slower horizontal slashes.
///   VoidWarhammer   — Slow, massive damage, 30% armor penetration, huge stagger on hit.
/// </summary>
public static class MeleeAssetCreator
{
    private const string MeleeDefsPath  = "Assets/ScriptableObjects/MeleeWeapons";
    private const string WeaponItemPath = "Assets/ScriptableObjects/WeaponItems";
    private const string DatabasePath   = "Assets/Resources/ItemDatabase.asset";

    [MenuItem("Voidborne/Create Starter Melee Weapons")]
    public static void CreateStarterMeleeWeapons()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(MeleeDefsPath,  "Assets/ScriptableObjects", "MeleeWeapons");
        EnsureFolder(WeaponItemPath, "Assets/ScriptableObjects", "WeaponItems");

        // Create MeleeDefinition assets and paired WeaponItem assets.
        var pairs = new List<(MeleeDefinition def, WeaponItem wi)>
        {
            BuildWoodenClub(),
            BuildIronDagger(),
            BuildIronLongsword(),
            BuildTitaniumSpear(),
            BuildVoidWarhammer(),
        };

        AssetDatabase.SaveAssets();

        // Register WeaponItems in the ItemDatabase.
        RegisterInDatabase(pairs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MeleeAssetCreator] All 5 starter melee weapon assets created and registered in ItemDatabase.");
    }

    // -----------------------------------------------------------------------
    // Weapon factory helpers
    // -----------------------------------------------------------------------

    private static (MeleeDefinition, WeaponItem) BuildWoodenClub()
    {
        var d = GetOrCreateMeleeDef("WoodenClub");
        d.weaponName             = "Wooden Club";
        d.damage                 = 38f;
        d.damageType             = DamageType.Melee;
        d.range                  = 2.5f;
        d.hitRadius              = 0.45f;
        d.windupTime             = 0.45f;
        d.releaseTime            = 0.28f;
        d.recoveryTime           = 0.55f;
        d.staminaCost            = 20f;
        d.attackSpeed            = 0.70f;
        d.maxComboChain          = 2;
        d.comboExhaustionPenalty = 0.60f;
        d.armorPenetration       = 0f;
        d.hitStaggerDuration     = 0.25f;
        EditorUtility.SetDirty(d);

        var wi = GetOrCreateWeaponItem("WoodenClub_Item");
        wi.itemId          = "wooden_club";
        wi.displayName     = "Wooden Club";
        wi.description     = "A thick hardwood club. Slow, but sends enemies stumbling with every hit.";
        wi.itemType        = ItemType.Weapon;
        wi.maxStackSize    = 1;
        wi.weight          = 3.0f;
        wi.meleeDefinition = d;
        EditorUtility.SetDirty(wi);

        return (d, wi);
    }

    private static (MeleeDefinition, WeaponItem) BuildIronDagger()
    {
        var d = GetOrCreateMeleeDef("IronDagger");
        d.weaponName             = "Iron Dagger";
        d.damage                 = 18f;
        d.damageType             = DamageType.Melee;
        d.range                  = 1.8f;
        d.hitRadius              = 0.30f;
        d.windupTime             = 0.15f;
        d.releaseTime            = 0.15f;
        d.recoveryTime           = 0.20f;
        d.staminaCost            = 8f;
        d.attackSpeed            = 1.60f;
        d.maxComboChain          = 5;
        d.comboExhaustionPenalty = 0.20f;
        d.armorPenetration       = 0f;
        d.hitStaggerDuration     = 0f;
        EditorUtility.SetDirty(d);

        var wi = GetOrCreateWeaponItem("IronDagger_Item");
        wi.itemId          = "iron_dagger";
        wi.displayName     = "Iron Dagger";
        wi.description     = "A nimble iron dagger. Low damage per hit, but blazing fast — perfect for feints and morphs.";
        wi.itemType        = ItemType.Weapon;
        wi.maxStackSize    = 1;
        wi.weight          = 0.8f;
        wi.meleeDefinition = d;
        EditorUtility.SetDirty(wi);

        return (d, wi);
    }

    private static (MeleeDefinition, WeaponItem) BuildIronLongsword()
    {
        var d = GetOrCreateMeleeDef("IronLongsword");
        d.weaponName             = "Iron Longsword";
        d.damage                 = 35f;
        d.damageType             = DamageType.Melee;
        d.range                  = 2.8f;
        d.hitRadius              = 0.40f;
        d.windupTime             = 0.28f;
        d.releaseTime            = 0.22f;
        d.recoveryTime           = 0.38f;
        d.staminaCost            = 15f;
        d.attackSpeed            = 1.00f;
        d.maxComboChain          = 3;
        d.comboExhaustionPenalty = 0.35f;
        d.armorPenetration       = 0f;
        d.hitStaggerDuration     = 0f;
        EditorUtility.SetDirty(d);

        var wi = GetOrCreateWeaponItem("IronLongsword_Item");
        wi.itemId          = "iron_longsword";
        wi.displayName     = "Iron Longsword";
        wi.description     = "A well-balanced longsword. Reliable damage, reach, and speed — the standard melee weapon.";
        wi.itemType        = ItemType.Weapon;
        wi.maxStackSize    = 1;
        wi.weight          = 2.0f;
        wi.meleeDefinition = d;
        EditorUtility.SetDirty(wi);

        return (d, wi);
    }

    private static (MeleeDefinition, WeaponItem) BuildTitaniumSpear()
    {
        var d = GetOrCreateMeleeDef("TitaniumSpear");
        d.weaponName             = "Titanium Spear";
        d.damage                 = 44f;
        d.damageType             = DamageType.Melee;
        d.range                  = 4.0f;
        d.hitRadius              = 0.25f;
        d.windupTime             = 0.38f;
        d.releaseTime            = 0.20f;
        d.recoveryTime           = 0.48f;
        d.staminaCost            = 18f;
        d.attackSpeed            = 0.85f;
        d.maxComboChain          = 3;
        d.comboExhaustionPenalty = 0.50f;
        d.armorPenetration       = 0.15f;
        d.hitStaggerDuration     = 0f;
        EditorUtility.SetDirty(d);

        var wi = GetOrCreateWeaponItem("TitaniumSpear_Item");
        wi.itemId          = "titanium_spear";
        wi.displayName     = "Titanium Spear";
        wi.description     = "A long titanium-tipped spear. Exceptional reach and armor penetration; slower side swings.";
        wi.itemType        = ItemType.Weapon;
        wi.maxStackSize    = 1;
        wi.weight          = 2.8f;
        wi.meleeDefinition = d;
        EditorUtility.SetDirty(wi);

        return (d, wi);
    }

    private static (MeleeDefinition, WeaponItem) BuildVoidWarhammer()
    {
        var d = GetOrCreateMeleeDef("VoidWarhammer");
        d.weaponName             = "Void Warhammer";
        d.damage                 = 95f;
        d.damageType             = DamageType.Melee;
        d.range                  = 2.5f;
        d.hitRadius              = 0.55f;
        d.windupTime             = 0.75f;
        d.releaseTime            = 0.32f;
        d.recoveryTime           = 0.85f;
        d.staminaCost            = 38f;
        d.attackSpeed            = 0.55f;
        d.maxComboChain          = 2;
        d.comboExhaustionPenalty = 1.00f;
        d.armorPenetration       = 0.30f;
        d.hitStaggerDuration     = 1.20f;
        EditorUtility.SetDirty(d);

        var wi = GetOrCreateWeaponItem("VoidWarhammer_Item");
        wi.itemId          = "void_warhammer";
        wi.displayName     = "Void Warhammer";
        wi.description     = "A void-forged warhammer. Devastatingly slow, but ignores 30% armor and leaves survivors staggered for over a second.";
        wi.itemType        = ItemType.Weapon;
        wi.maxStackSize    = 1;
        wi.weight          = 8.0f;
        wi.meleeDefinition = d;
        EditorUtility.SetDirty(wi);

        return (d, wi);
    }

    // -----------------------------------------------------------------------
    // ItemDatabase registration
    // -----------------------------------------------------------------------

    private static void RegisterInDatabase(List<(MeleeDefinition def, WeaponItem wi)> pairs)
    {
        // Load or create the ItemDatabase.
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (db == null)
        {
            EnsureFolder("Assets/Resources");
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            Debug.Log("[MeleeAssetCreator] Created new ItemDatabase at " + DatabasePath);
        }

        bool changed = false;
        foreach (var (_, wi) in pairs)
        {
            // Check if already registered by itemId.
            bool found = false;
            foreach (var existing in db.items)
            {
                if (existing != null && existing.itemId == wi.itemId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                db.items.Add(wi);
                changed = true;
                Debug.Log($"[MeleeAssetCreator] Registered '{wi.itemId}' in ItemDatabase.");
            }
            else
            {
                Debug.Log($"[MeleeAssetCreator] '{wi.itemId}' already in ItemDatabase — skipped.");
            }
        }

        if (changed)
            EditorUtility.SetDirty(db);
    }

    // -----------------------------------------------------------------------
    // Asset creation utilities
    // -----------------------------------------------------------------------

    private static MeleeDefinition GetOrCreateMeleeDef(string fileName)
    {
        string path = $"{MeleeDefsPath}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MeleeDefinition>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<MeleeDefinition>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static WeaponItem GetOrCreateWeaponItem(string fileName)
    {
        string path = $"{WeaponItemPath}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<WeaponItem>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<WeaponItem>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            int lastSlash = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, lastSlash);
            string name   = folderPath.Substring(lastSlash + 1);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void EnsureFolder(string fullPath, string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
