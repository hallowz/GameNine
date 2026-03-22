using System.IO;
using UnityEngine;
using UnityEditor;
using Voidborne.Combat.Projectiles;

/// <summary>
/// Creates all bow and throwable weapon assets for Volume 6.2.
/// Run via: Voidborne > Create Ranged Weapon Assets
///
/// Creates:
///   Projectile Definitions (arrows/bolts/spear/axe)
///   Bow Definitions:    ShortBow, LongBow, Crossbow
///   Throwable Defs:     ThrowingSpear, ThrowingAxe
///   WeaponItems:        one per weapon, linked to the definitions above
/// </summary>
public static class ProjectileWeaponAssetCreator
{
    private const string ProjDefPath   = "Assets/ScriptableObjects/Projectiles";
    private const string BowDefPath    = "Assets/ScriptableObjects/Bows";
    private const string ThrowDefPath  = "Assets/ScriptableObjects/Throwables";
    private const string WeaponPath    = "Assets/ScriptableObjects/Weapons";

    [MenuItem("Voidborne/Create Ranged Weapon Assets")]
    public static void CreateAll()
    {
        EnsureDir(ProjDefPath);
        EnsureDir(BowDefPath);
        EnsureDir(ThrowDefPath);
        EnsureDir(WeaponPath);

        // ----------------------------------------------------------------
        // Arrow / bolt / projectile definitions
        // ----------------------------------------------------------------
        var woodArrow = CreateProjectileDef("Arrow_Wood",
            baseDamage: 28f, launchVelocity: 50f, gravityMult: 0.8f,
            drag: 0.04f, lifetime: 10f, penetrationDepth: 0.12f,
            tumbles: false, checkBlade: false);

        var longArrow = CreateProjectileDef("Arrow_Long",
            baseDamage: 45f, launchVelocity: 65f, gravityMult: 0.6f,
            drag: 0.02f, lifetime: 12f, penetrationDepth: 0.15f,
            tumbles: false, checkBlade: false);

        var bolt = CreateProjectileDef("Bolt_Crossbow",
            baseDamage: 55f, launchVelocity: 75f, gravityMult: 0.5f,
            drag: 0.03f, lifetime: 8f, penetrationDepth: 0.18f,
            tumbles: false, checkBlade: false);

        var spearProjectile = CreateProjectileDef("Projectile_ThrowingSpear",
            baseDamage: 65f, launchVelocity: 38f, gravityMult: 0.4f,
            drag: 0.02f, lifetime: 10f, penetrationDepth: 0.25f,
            tumbles: false, checkBlade: false);

        var axeProjectile = CreateProjectileDef("Projectile_ThrowingAxe",
            baseDamage: 50f, launchVelocity: 30f, gravityMult: 0.9f,
            drag: 0.05f, lifetime: 8f, penetrationDepth: 0.20f,
            tumbles: true, tumbleSpd: 420f,
            checkBlade: true, handleMult: 0.4f);

        // ----------------------------------------------------------------
        // Bow definitions
        // ----------------------------------------------------------------
        CreateBowDef("ShortBow",
            arrowDef: woodArrow,
            minDrawTime: 0.15f, fullDrawTime: 0.9f,
            minVelocity: 18f,   maxVelocity: 45f,
            spreadMin: 6f,      spreadMax: 1.0f,
            wobbleDelay: 1.2f,  wobbleAngle: 5f,
            stamDrain: 7f,      drawZoom: 1.05f);

        CreateBowDef("LongBow",
            arrowDef: longArrow,
            minDrawTime: 0.3f, fullDrawTime: 1.5f,
            minVelocity: 15f,  maxVelocity: 65f,
            spreadMin: 8f,     spreadMax: 0.5f,
            wobbleDelay: 1.8f, wobbleAngle: 7f,
            stamDrain: 10f,    drawZoom: 1.2f);

        CreateBowDef("Crossbow",
            arrowDef: bolt,
            minDrawTime: 0.05f, fullDrawTime: 0.4f,
            minVelocity: 72f,   maxVelocity: 75f,
            spreadMin: 1.5f,    spreadMax: 0.3f,
            wobbleDelay: 3f,    wobbleAngle: 3f,
            stamDrain: 4f,      drawZoom: 1.3f);

        // ----------------------------------------------------------------
        // Throwable definitions
        // ----------------------------------------------------------------
        CreateThrowableDef("ThrowingSpear",
            projDef: spearProjectile,
            windupTime: 0.4f, throwVelocity: 38f,
            tumbles: false, tumbleSpd: 0f,
            bladeMult: 1f, handleMult: 1f);

        CreateThrowableDef("ThrowingAxe",
            projDef: axeProjectile,
            windupTime: 0.35f, throwVelocity: 30f,
            tumbles: true, tumbleSpd: 420f,
            bladeMult: 1f, handleMult: 0.4f);

        // ----------------------------------------------------------------
        // WeaponItems — load bow/throwable defs back and build items
        // ----------------------------------------------------------------
        CreateBowWeaponItem("ShortBow",   "short_bow");
        CreateBowWeaponItem("LongBow",    "long_bow");
        CreateBowWeaponItem("Crossbow",   "crossbow");
        CreateThrowableWeaponItem("ThrowingSpear", "throwing_spear");
        CreateThrowableWeaponItem("ThrowingAxe",   "throwing_axe");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ----------------------------------------------------------------
        // Register all WeaponItems in the ItemDatabase so AddStarterItems()
        // picks them up automatically when Play is hit.
        // ----------------------------------------------------------------
        WeaponItem wiShortBow    = AssetDatabase.LoadAssetAtPath<WeaponItem>($"{WeaponPath}/WeaponItem_ShortBow.asset");
        WeaponItem wiLongBow     = AssetDatabase.LoadAssetAtPath<WeaponItem>($"{WeaponPath}/WeaponItem_LongBow.asset");
        WeaponItem wiCrossbow    = AssetDatabase.LoadAssetAtPath<WeaponItem>($"{WeaponPath}/WeaponItem_Crossbow.asset");
        WeaponItem wiSpear       = AssetDatabase.LoadAssetAtPath<WeaponItem>($"{WeaponPath}/WeaponItem_ThrowingSpear.asset");
        WeaponItem wiAxe         = AssetDatabase.LoadAssetAtPath<WeaponItem>($"{WeaponPath}/WeaponItem_ThrowingAxe.asset");

        RegisterInItemDatabase(wiShortBow, wiLongBow, wiCrossbow, wiSpear, wiAxe);

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Ranged Assets Created",
            "Created all bow and throwable weapon assets:\n\n" +
            "Bows:         ShortBow, LongBow, Crossbow\n" +
            "Throwables:   ThrowingSpear, ThrowingAxe\n" +
            "Projectiles:  Arrow_Wood, Arrow_Long, Bolt_Crossbow,\n" +
            "              Projectile_ThrowingSpear, Projectile_ThrowingAxe\n" +
            "WeaponItems:  5 items in Assets/ScriptableObjects/Weapons/\n\n" +
            "Drag them into the Player's hotbar in the Inspector or\n" +
            "add them via Inventory.AddStarterItems().",
            "OK");
    }

    // ----------------------------------------------------------------
    // Factories
    // ----------------------------------------------------------------

    private static ProjectileDefinition CreateProjectileDef(
        string assetName,
        float baseDamage, float launchVelocity, float gravityMult,
        float drag, float lifetime, float penetrationDepth,
        bool tumbles, float tumbleSpd = 360f,
        bool checkBlade = false, float handleMult = 0.4f)
    {
        string path = $"{ProjDefPath}/{assetName}.asset";
        var def = LoadOrCreate<ProjectileDefinition>(path);

        def.projectileName    = assetName;
        def.baseDamage        = baseDamage;
        def.launchVelocity    = launchVelocity;
        def.gravityMultiplier = gravityMult;
        def.drag              = drag;
        def.lifetime          = lifetime;
        def.penetrationDepth  = penetrationDepth;
        def.bounceOnSurface   = false;
        def.isRetrievable     = true;
        def.tumbles           = tumbles;
        def.tumbleSpeed       = tumbleSpd;
        def.checkBladeOrientation   = checkBlade;
        def.handleDamageMultiplier  = handleMult;

        EditorUtility.SetDirty(def);
        return def;
    }

    private static void CreateBowDef(
        string assetName,
        ProjectileDefinition arrowDef,
        float minDrawTime, float fullDrawTime,
        float minVelocity, float maxVelocity,
        float spreadMin, float spreadMax,
        float wobbleDelay, float wobbleAngle,
        float stamDrain, float drawZoom)
    {
        string path = $"{BowDefPath}/{assetName}.asset";
        var def = LoadOrCreate<BowDefinition>(path);

        def.bowName               = assetName;
        def.arrowDefinition       = arrowDef;
        def.minDrawTime           = minDrawTime;
        def.fullDrawTime          = fullDrawTime;
        def.minVelocity           = minVelocity;
        def.maxVelocity           = maxVelocity;
        def.spreadAtMinDraw       = spreadMin;
        def.spreadAtFullDraw      = spreadMax;
        def.wobbleStartDelay      = wobbleDelay;
        def.wobbleMaxAngle        = wobbleAngle;
        def.staminaDrainPerSecond = stamDrain;
        def.drawZoom              = drawZoom;

        EditorUtility.SetDirty(def);
    }

    private static void CreateThrowableDef(
        string assetName,
        ProjectileDefinition projDef,
        float windupTime, float throwVelocity,
        bool tumbles, float tumbleSpd,
        float bladeMult, float handleMult)
    {
        string path = $"{ThrowDefPath}/{assetName}.asset";
        var def = LoadOrCreate<ThrowableDefinition>(path);

        def.weaponName            = assetName;
        def.projectileDefinition  = projDef;
        def.windupTime            = windupTime;
        def.throwVelocity         = throwVelocity;
        def.tumbles               = tumbles;
        def.tumbleSpeed           = tumbleSpd;
        def.bladeHitMultiplier    = bladeMult;
        def.handleHitMultiplier   = handleMult;

        EditorUtility.SetDirty(def);
    }

    private static void CreateBowWeaponItem(string bowName, string itemId)
    {
        var bowDef = AssetDatabase.LoadAssetAtPath<BowDefinition>($"{BowDefPath}/{bowName}.asset");
        if (bowDef == null) { Debug.LogWarning($"[ProjectileWeaponAssetCreator] BowDef not found: {bowName}"); return; }

        string path = $"{WeaponPath}/WeaponItem_{bowName}.asset";
        var item = LoadOrCreate<WeaponItem>(path);

        item.itemId         = itemId;
        item.displayName    = bowName.Replace("_", " ");
        item.description    = BowDescription(bowName);
        item.itemType       = ItemType.Weapon;
        item.maxStackSize   = 1;
        item.bowDefinition  = bowDef;
        item.gunDefinition      = null;
        item.meleeDefinition    = null;
        item.throwableDefinition = null;

        EditorUtility.SetDirty(item);
    }

    private static void CreateThrowableWeaponItem(string throwableName, string itemId)
    {
        var throwDef = AssetDatabase.LoadAssetAtPath<ThrowableDefinition>($"{ThrowDefPath}/{throwableName}.asset");
        if (throwDef == null) { Debug.LogWarning($"[ProjectileWeaponAssetCreator] ThrowableDef not found: {throwableName}"); return; }

        string path = $"{WeaponPath}/WeaponItem_{throwableName}.asset";
        var item = LoadOrCreate<WeaponItem>(path);

        item.itemId                  = itemId;
        item.displayName             = throwableName.Replace("_", " ");
        item.description             = ThrowableDescription(throwableName);
        item.itemType                = ItemType.Weapon;
        item.maxStackSize            = 5;
        item.throwableDefinition     = throwDef;
        item.gunDefinition           = null;
        item.meleeDefinition         = null;
        item.bowDefinition           = null;

        EditorUtility.SetDirty(item);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string BowDescription(string name)
    {
        switch (name)
        {
            case "ShortBow":  return "A quick, lightweight bow. Easy to draw on the move. Less power than a longbow but forgiving at close range.";
            case "LongBow":   return "A tall war bow that punches through armour at distance. Demands a full draw to unlock its devastating range.";
            case "Crossbow":  return "Mechanical precision in a compact frame. No draw skill needed — just aim, hold, and release.";
            default:          return "A ranged bow weapon.";
        }
    }

    private static string ThrowableDescription(string name)
    {
        switch (name)
        {
            case "ThrowingSpear": return "A balanced iron spear built to be thrown. Flies true and embeds deep. Retrieve it from the terrain after each throw.";
            case "ThrowingAxe":   return "A weighted axe that tumbles through the air. Hit with the blade for full damage — handle strikes hurt far less.";
            default:              return "A throwable weapon. Retrieve it after it lands.";
        }
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static void RegisterInItemDatabase(params WeaponItem[] weapons)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[ProjectileWeaponAssetCreator] ItemDatabase not found — add weapons manually.");
            return;
        }

        string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
        if (db == null) return;

        SerializedObject so    = new SerializedObject(db);
        SerializedProperty arr = so.FindProperty("items");

        foreach (WeaponItem weapon in weapons)
        {
            if (weapon == null) continue;

            bool found = false;
            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == weapon)
                { found = true; break; }
            }

            if (!found)
            {
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = weapon;
                Debug.Log($"[ProjectileWeaponAssetCreator] Added '{weapon.itemId}' to ItemDatabase.");
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
    }
}
