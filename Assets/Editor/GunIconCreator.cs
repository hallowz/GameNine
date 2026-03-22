using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility that generates distinct 64x64 pixel art gun icons for each
/// starter weapon and assigns them to the corresponding WeaponItem ScriptableObjects.
///
/// Run via: Voidborne > Create Gun Icons
/// </summary>
public static class GunIconCreator
{
    private const int    IconSize    = 64;
    private const string OutputPath  = "Assets/Textures/GunIcons";
    private const string WeaponsPath = "Assets/ScriptableObjects/Items/Weapons";

    [MenuItem("Voidborne/Create Gun Icons")]
    public static void CreateGunIcons()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/Textures", "GunIcons");

        GenerateAndAssign("Revolver.asset",    DrawRevolver,  "revolver_icon.png");
        GenerateAndAssign("Rattler_SMG.asset", DrawSMG,       "smg_icon.png");
        GenerateAndAssign("Ironbark_AR.asset", DrawAR,        "ar_icon.png");
        GenerateAndAssign("PumpShotgun.asset", DrawShotgun,   "shotgun_icon.png");
        GenerateAndAssign("BoltSniper.asset",  DrawSniper,    "sniper_icon.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GunIconCreator] 5 gun icons created in " + OutputPath + " and assigned to WeaponItems.");
        EditorUtility.DisplayDialog("Gun Icons Created",
            "5 distinct gun icons generated and assigned to WeaponItems:\n\n" +
            "  Revolver  — dark blue, cylinder-frame pistol\n" +
            "  Rattler SMG  — burnt orange, compact box\n" +
            "  Ironbark AR  — forest green, medium rifle\n" +
            "  Pump Shotgun — deep red, wide dual-tube\n" +
            "  Bolt Sniper  — dark purple, long scoped rifle\n\n" +
            "Icons saved to: " + OutputPath,
            "OK");
    }

    // -----------------------------------------------------------------------
    // Per-gun draw routines
    // Each receives a Texture2D (cleared to bgColor) and draws on it.
    // Y=0 is bottom of the texture; all shapes use pixel coordinates.
    // -----------------------------------------------------------------------

    private static void DrawRevolver(Texture2D tex)
    {
        // Dark navy background
        Fill(tex, new Color32(18, 35, 90, 255));

        Color32 gun   = new Color32(220, 220, 220, 255);
        Color32 dark  = new Color32(150, 150, 150, 255);
        Color32 black = new Color32(40,  40,  40,  255);

        // Barrel — short and slightly thick
        FillRect(tex, 36, 30, 20, 7, gun);

        // Cylinder (frame) — wide rounded block
        FillRect(tex, 18, 26, 22, 14, gun);

        // Grip — angled downward
        FillRect(tex, 20, 13, 10, 14, dark);
        FillRect(tex, 22, 11, 8,   4, dark);

        // Trigger guard
        FillRect(tex, 28, 18,  2,  8, black);
        FillRect(tex, 26, 18,  8,  2, black);

        // Hammer spur
        FillRect(tex, 36, 38,  4,  5, dark);

        // Muzzle tip highlight
        FillRect(tex, 54, 31,  3,  5, black);

        // Front sight post
        FillRect(tex, 52, 37,  2,  3, gun);

        // Cylinder detail lines
        FillRect(tex, 24, 34, 14,  1, black);
        FillRect(tex, 24, 30, 14,  1, black);
    }

    private static void DrawSMG(Texture2D tex)
    {
        // Burnt orange background
        Fill(tex, new Color32(120, 55, 5, 255));

        Color32 gun   = new Color32(225, 215, 195, 255);
        Color32 dark  = new Color32(140, 130, 110, 255);
        Color32 black = new Color32(30,  25,  15,  255);

        // Compact boxy receiver
        FillRect(tex, 12, 27, 36, 12, gun);

        // Short barrel
        FillRect(tex, 48, 29, 12,  8, gun);
        // Muzzle device (wider box)
        FillRect(tex, 56, 28,  4, 10, dark);

        // Stock (folded, flat on back)
        FillRect(tex,  6, 28,  8,  8, dark);
        FillRect(tex,  6, 27,  4,  2, black);

        // Pistol grip
        FillRect(tex, 18, 14, 10, 14, dark);
        FillRect(tex, 20, 12,  8,  4, dark);

        // Vertical magazine (extends below grip)
        FillRect(tex, 30,  7, 10, 20, dark);
        // Mag catch detail
        FillRect(tex, 30, 26,  2,  2, black);

        // Charging handle nub on top
        FillRect(tex, 38, 39,  4,  3, black);

        // Rail / iron sight
        FillRect(tex, 16, 39, 20,  2, dark);
        FillRect(tex, 22, 41,  2,  3, gun);   // front sight
        FillRect(tex, 14, 41,  2,  3, gun);   // rear sight
    }

    private static void DrawAR(Texture2D tex)
    {
        // Forest green background
        Fill(tex, new Color32(15, 55, 18, 255));

        Color32 gun   = new Color32(200, 215, 195, 255);
        Color32 dark  = new Color32(120, 135, 115, 255);
        Color32 black = new Color32(20,  30,  20,  255);

        // Long barrel
        FillRect(tex, 42, 30, 20,  6, gun);

        // Upper receiver
        FillRect(tex, 16, 32, 30, 10, gun);

        // Lower receiver
        FillRect(tex, 16, 24, 28,  9, dark);

        // Stock (right-angle)
        FillRect(tex,  4, 28, 14,  8, dark);
        FillRect(tex,  4, 24,  6,  4, dark);

        // Pistol grip
        FillRect(tex, 18, 13, 10, 12, dark);
        FillRect(tex, 20, 11,  8,  4, dark);

        // Curved magazine
        FillRect(tex, 26,  6, 10, 20, dark);
        FillRect(tex, 28,  6, 10, 16, dark);
        FillRect(tex, 26, 22,  2,  3, black);  // mag catch

        // Carry handle / rear sight
        FillRect(tex, 22, 42, 16,  4, dark);
        FillRect(tex, 24, 46,  3,  3, gun);
        FillRect(tex, 32, 46,  3,  3, gun);

        // Gas block / front sight
        FillRect(tex, 42, 36,  4,  4, dark);
        FillRect(tex, 43, 40,  2,  3, gun);

        // Handguard detail
        FillRect(tex, 30, 31, 12,  1, black);
        FillRect(tex, 30, 35, 12,  1, black);
    }

    private static void DrawShotgun(Texture2D tex)
    {
        // Deep red background
        Fill(tex, new Color32(100, 12, 12, 255));

        Color32 gun   = new Color32(220, 210, 185, 255);
        Color32 wood  = new Color32(140, 85,  40,  255);
        Color32 dark  = new Color32(140, 130, 110, 255);
        Color32 black = new Color32(30,  20,  10,  255);

        // Upper barrel tube
        FillRect(tex, 14, 35, 44,  8, gun);

        // Lower barrel / magazine tube
        FillRect(tex, 18, 27, 38,  8, gun);

        // Receiver block
        FillRect(tex, 10, 25, 16, 18, dark);

        // Wooden stock
        FillRect(tex,  4, 24, 14, 12, wood);
        FillRect(tex,  4, 22, 10,  4, wood);

        // Pistol grip
        FillRect(tex, 12, 14, 10, 12, wood);
        FillRect(tex, 14, 12,  8,  4, wood);

        // Pump / foregrip (distinct color, slides forward)
        FillRect(tex, 30, 22, 14,  5, wood);
        FillRect(tex, 28, 21, 18,  2, dark);  // pump rails

        // Muzzle end — two barrel openings
        FillRect(tex, 56, 36,  4,  6, black);  // upper barrel hole
        FillRect(tex, 56, 28,  4,  6, black);  // lower barrel hole
        FillRect(tex, 58, 35,  4,  2, dark);
        FillRect(tex, 58, 27,  4,  2, dark);

        // Ejection port
        FillRect(tex, 18, 36,  8,  4, black);

        // Bead sight
        FillRect(tex, 53, 43,  2,  2, gun);
    }

    private static void DrawSniper(Texture2D tex)
    {
        // Dark purple background
        Fill(tex, new Color32(48, 16, 76, 255));

        Color32 gun   = new Color32(205, 205, 215, 255);
        Color32 dark  = new Color32(130, 130, 140, 255);
        Color32 scope = new Color32(50,  90, 160, 255);  // Blue-tinted scope lens
        Color32 black = new Color32(20,  15,  30,  255);

        // Very long barrel
        FillRect(tex,  8, 30, 54,  6, gun);

        // Receiver
        FillRect(tex, 12, 27, 26, 12, gun);

        // Stock
        FillRect(tex,  4, 24, 14, 12, dark);
        FillRect(tex,  4, 22,  8,  4, dark);  // cheek rest
        FillRect(tex,  4, 36,  8,  2, dark);  // butt plate

        // Grip
        FillRect(tex, 14, 14, 10, 14, dark);
        FillRect(tex, 16, 12,  8,  4, dark);

        // Scope body — raised above receiver
        FillRect(tex, 22, 39, 22,  6, dark);
        FillRect(tex, 20, 38, 26,  2, black);  // scope mount ring front
        FillRect(tex, 38, 38,  4,  2, black);  // scope mount ring rear
        // Scope lens (front, blue)
        FillRect(tex, 22, 40,  5,  4, scope);
        // Scope eye piece (rear, slightly larger)
        FillRect(tex, 38, 39,  4,  5, scope);
        // Elevation turret
        FillRect(tex, 30, 45,  4,  4, dark);

        // Bolt handle
        FillRect(tex, 36, 27,  4,  4, dark);
        FillRect(tex, 38, 25,  4,  6, dark);

        // Bipod legs (two thin struts)
        FillRect(tex, 18, 16,  3, 14, dark);
        FillRect(tex, 26, 16,  3, 14, dark);
        FillRect(tex, 18, 15,  4,  2, black);  // bipod feet
        FillRect(tex, 26, 15,  4,  2, black);

        // Muzzle brake
        FillRect(tex, 60, 28,  4, 10, dark);
        FillRect(tex, 60, 30,  4,  1, black);
        FillRect(tex, 60, 34,  4,  1, black);
    }

    // -----------------------------------------------------------------------
    // Asset creation & assignment
    // -----------------------------------------------------------------------

    private static void GenerateAndAssign(
        string weaponItemFile,
        System.Action<Texture2D> drawFunc,
        string iconFileName)
    {
        // 1. Draw the icon into a Texture2D
        Texture2D tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        drawFunc(tex);
        tex.Apply();

        // 2. Build paths.
        //    Application.dataPath = "<project>/Assets" on all platforms.
        //    assetPath is the "Assets/..." path Unity uses for AssetDatabase calls.
        //    fullPath  is the absolute filesystem path for File.WriteAllBytes.
        string assetPath = OutputPath + "/" + iconFileName;
        // Strip leading "Assets/" from assetPath and join to dataPath to get absolute path.
        string relativePart = assetPath.Substring("Assets/".Length);   // "Textures/GunIcons/foo.png"
        string fullPath     = Path.Combine(Application.dataPath, relativePart)
                                  .Replace('\\', '/');

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // 3. Import the texture and configure it as a Sprite.
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = IconSize;
            importer.filterMode          = FilterMode.Point;  // crisp pixel art
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.SaveAndReimport();
        }

        // 4. Refresh and load the resulting Sprite.
        //    SaveAndReimport schedules a reimport; Refresh flushes it so
        //    LoadAssetAtPath can find the sprite sub-asset immediately.
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[GunIconCreator] Could not load sprite from '{assetPath}' after import. " +
                             "PNG written OK — try running the tool again after Unity finishes importing.");
            return;
        }

        // 5. Assign to the WeaponItem asset
        string itemPath = $"{WeaponsPath}/{weaponItemFile}";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        if (item == null)
        {
            Debug.LogWarning($"[GunIconCreator] WeaponItem not found at '{itemPath}'. " +
                             "Run 'Voidborne > Create Weapon Items' first.");
            return;
        }

        item.icon = sprite;
        EditorUtility.SetDirty(item);
        Debug.Log($"[GunIconCreator] Assigned icon '{iconFileName}' to '{item.displayName}'.");
    }

    // -----------------------------------------------------------------------
    // Pixel drawing helpers
    // -----------------------------------------------------------------------

    /// <summary>Fills the entire texture with a single color.</summary>
    private static void Fill(Texture2D tex, Color32 color)
    {
        Color32[] pixels = new Color32[IconSize * IconSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels32(pixels);
    }

    /// <summary>
    /// Fills a rectangle on the texture.
    /// x, y = bottom-left corner (Texture2D convention: y=0 is bottom).
    /// </summary>
    private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color32 color)
    {
        for (int py = y; py < y + h && py < IconSize; py++)
        {
            for (int px = x; px < x + w && px < IconSize; px++)
            {
                if (px >= 0 && py >= 0)
                    tex.SetPixel(px, py, color);
            }
        }
    }
}
