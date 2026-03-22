using UnityEngine;
using UnityEditor;
using Voidborne.Combat;

/// <summary>
/// Editor utility that creates all 5 starter GunDefinition ScriptableObject assets in one click.
/// Run via: Voidborne > Create Starter Guns
///
/// Note: modelPrefab1P and modelPrefab3P are left null for all guns — placeholder art will be
/// assigned once 3D models are available.
/// </summary>
public static class GunAssetCreator
{
    private const string OutputPath = "Assets/ScriptableObjects/Guns";

    [MenuItem("Voidborne/Create Starter Guns")]
    public static void CreateStarterGuns()
    {
        // Ensure the destination folder exists (create parent first if needed).
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Guns");

        CreateHandmadeRevolver();
        CreateRattlerSMG();
        CreateIronbarkAR();
        CreatePumpShotgun();
        CreateBoltSniper();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GunAssetCreator] All 5 starter gun assets created in " + OutputPath);
    }

    // -----------------------------------------------------------------------
    // Gun factory helpers
    // -----------------------------------------------------------------------

    private static void CreateHandmadeRevolver()
    {
        GunDefinition g = CreateAsset("HandmadeRevolver");

        g.gunName            = "Handmade Revolver";
        g.fireMode           = FireMode.Semi;
        g.bulletType         = BulletType.Hitscan;
        g.damage             = 45f;
        g.fireRate           = 120f;   // 2 shots/sec
        g.magazineSize       = 6;
        g.reloadTime         = 2.5f;
        g.range              = 80f;
        g.penetration        = 0.5f;
        g.spreadStanding     = 0.5f;
        g.spreadMoving       = 2.5f;
        g.spreadCrouching    = 0.2f;
        g.spreadAirborne     = 4f;
        g.adsZoomMultiplier  = 1.5f;
        g.equipTime          = 0.4f;
        g.pelletCount        = 1;
        g.recoilPattern      = new Vector2[]
        {
            new Vector2( 0.0f, 2.5f),
            new Vector2( 0.2f, 2.0f),
            new Vector2(-0.1f, 2.0f),
            new Vector2( 0.3f, 2.5f),
            new Vector2(-0.2f, 2.0f),
            new Vector2( 0.0f, 3.0f),
        };

        EditorUtility.SetDirty(g);
    }

    private static void CreateRattlerSMG()
    {
        GunDefinition g = CreateAsset("Rattler_SMG");

        g.gunName            = "Rattler SMG";
        g.fireMode           = FireMode.Auto;
        g.bulletType         = BulletType.Hitscan;
        g.damage             = 18f;
        g.fireRate           = 800f;
        g.magazineSize       = 30;
        g.reloadTime         = 2.0f;
        g.range              = 50f;
        g.penetration        = 0.2f;
        g.spreadStanding     = 1.5f;
        g.spreadMoving       = 3.0f;
        g.spreadCrouching    = 0.8f;
        g.spreadAirborne     = 5.0f;
        g.adsZoomMultiplier  = 1.2f;
        g.equipTime          = 0.25f;
        g.pelletCount        = 1;
        // Alternating left/right with upward climb (8 entries)
        g.recoilPattern      = new Vector2[]
        {
            new Vector2( 0.0f, 1.5f),
            new Vector2( 0.3f, 1.5f),
            new Vector2(-0.3f, 1.5f),
            new Vector2( 0.4f, 2.0f),
            new Vector2(-0.4f, 2.0f),
            new Vector2( 0.3f, 2.0f),
            new Vector2(-0.3f, 2.0f),
            new Vector2( 0.0f, 1.5f),
        };

        EditorUtility.SetDirty(g);
    }

    private static void CreateIronbarkAR()
    {
        GunDefinition g = CreateAsset("Ironbark_AR");

        g.gunName            = "Ironbark AR";
        g.fireMode           = FireMode.Auto;
        g.bulletType         = BulletType.Hitscan;
        g.damage             = 28f;
        g.fireRate           = 600f;
        g.magazineSize       = 25;
        g.reloadTime         = 2.3f;
        g.range              = 120f;
        g.penetration        = 0.4f;
        g.spreadStanding     = 0.8f;
        g.spreadMoving       = 2.0f;
        g.spreadCrouching    = 0.4f;
        g.spreadAirborne     = 3.5f;
        g.adsZoomMultiplier  = 1.5f;
        g.equipTime          = 0.3f;
        g.pelletCount        = 1;
        // Pull-down-left pattern (8 entries)
        g.recoilPattern      = new Vector2[]
        {
            new Vector2( 0.0f, 2.0f),
            new Vector2(-0.2f, 2.0f),
            new Vector2(-0.4f, 2.5f),
            new Vector2(-0.3f, 2.0f),
            new Vector2(-0.5f, 2.5f),
            new Vector2(-0.4f, 2.0f),
            new Vector2(-0.3f, 2.0f),
            new Vector2(-0.2f, 1.5f),
        };

        EditorUtility.SetDirty(g);
    }

    private static void CreatePumpShotgun()
    {
        GunDefinition g = CreateAsset("PumpShotgun");

        g.gunName            = "Pump Shotgun";
        g.fireMode           = FireMode.Semi;   // pump = one shell per trigger pull
        g.bulletType         = BulletType.Hitscan;
        g.damage             = 15f;             // per pellet; 8 pellets per shot = up to 120 damage
        g.fireRate           = 60f;             // 1 shot/sec
        g.magazineSize       = 6;
        g.reloadTime         = 3.5f;
        g.range              = 30f;
        g.penetration        = 0.1f;
        g.spreadStanding     = 4.0f;
        g.spreadMoving       = 7.0f;
        g.spreadCrouching    = 3.0f;
        g.spreadAirborne     = 9.0f;
        g.adsZoomMultiplier  = 1.1f;
        g.equipTime          = 0.5f;
        g.pelletCount        = 8;               // 8 independent hitscan rays per shot
        g.recoilPattern      = new Vector2[]
        {
            new Vector2(0.0f, 5.0f),            // single heavy kick
        };

        EditorUtility.SetDirty(g);
    }

    private static void CreateBoltSniper()
    {
        GunDefinition g = CreateAsset("BoltSniper");

        g.gunName            = "Bolt Sniper";
        g.fireMode           = FireMode.Semi;   // bolt-action = one shot per trigger pull
        g.bulletType         = BulletType.Hitscan;
        g.damage             = 110f;
        g.fireRate           = 30f;             // 0.5 shots/sec
        g.magazineSize       = 5;
        g.reloadTime         = 3.5f;
        g.range              = 500f;
        g.penetration        = 2.0f;
        g.spreadStanding     = 0.10f;
        g.spreadMoving       = 3.0f;
        g.spreadCrouching    = 0.05f;
        g.spreadAirborne     = 6.0f;
        g.adsZoomMultiplier  = 4.0f;
        g.equipTime          = 0.6f;
        g.pelletCount        = 1;
        // Strong vertical kick with slight left drift on follow-through
        g.recoilPattern      = new Vector2[]
        {
            new Vector2( 0.0f, 8.0f),
            new Vector2(-0.1f, 5.0f),
        };

        EditorUtility.SetDirty(g);
    }

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new GunDefinition asset at <see cref="OutputPath"/>/<paramref name="assetName"/>.asset.
    /// If an asset already exists at that path it is overwritten.
    /// </summary>
    private static GunDefinition CreateAsset(string assetName)
    {
        string assetPath = $"{OutputPath}/{assetName}.asset";

        GunDefinition existing = AssetDatabase.LoadAssetAtPath<GunDefinition>(assetPath);
        if (existing != null)
        {
            Debug.Log($"[GunAssetCreator] Overwriting existing asset: {assetPath}");
            return existing;
        }

        GunDefinition g = ScriptableObject.CreateInstance<GunDefinition>();
        AssetDatabase.CreateAsset(g, assetPath);
        return g;
    }
}
