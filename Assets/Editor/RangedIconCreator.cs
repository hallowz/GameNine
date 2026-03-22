using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Generates distinct 64×64 pixel-art icons for each ranged weapon and assigns them
/// to the corresponding WeaponItem ScriptableObjects.
///
/// Run via: Voidborne > Create Ranged Icons
/// </summary>
public static class RangedIconCreator
{
    private const int    IconSize     = 64;
    private const string OutputPath   = "Assets/Textures/RangedIcons";
    private const string WeaponsPath  = "Assets/ScriptableObjects/Weapons";

    [MenuItem("Voidborne/Create Ranged Icons")]
    public static void CreateRangedIcons()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/Textures", "RangedIcons");

        GenerateAndAssign("WeaponItem_ShortBow.asset",      DrawShortBow,      "shortbow_icon.png");
        GenerateAndAssign("WeaponItem_LongBow.asset",       DrawLongBow,       "longbow_icon.png");
        GenerateAndAssign("WeaponItem_Crossbow.asset",      DrawCrossbow,      "crossbow_icon.png");
        GenerateAndAssign("WeaponItem_ThrowingSpear.asset", DrawThrowingSpear, "throwing_spear_icon.png");
        GenerateAndAssign("WeaponItem_ThrowingAxe.asset",   DrawThrowingAxe,   "throwing_axe_icon.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[RangedIconCreator] 5 ranged weapon icons created in " + OutputPath);
        EditorUtility.DisplayDialog("Ranged Icons Created",
            "5 ranged weapon icons generated and assigned to WeaponItems:\n\n" +
            "  Short Bow    — dark teal, D-shape bow with arrow\n" +
            "  Long Bow     — forest green, tall slender bow\n" +
            "  Crossbow     — dark maroon, horizontal prod + stock\n" +
            "  Throwing Spear — olive, diagonal shaft with silver tip\n" +
            "  Throwing Axe   — steel blue, crescent head + handle\n\n" +
            "Icons saved to: " + OutputPath,
            "OK");
    }

    // -----------------------------------------------------------------------
    // Per-weapon draw routines  (y=0 is bottom of texture)
    // -----------------------------------------------------------------------

    private static void DrawShortBow(Texture2D tex)
    {
        // Background: dark teal
        Fill(tex, new Color32(12, 58, 68, 255));

        Color32 wood   = new Color32(188, 142, 62, 255);  // limb colour
        Color32 str    = new Color32(222, 218, 196, 255);  // bowstring
        Color32 shaft  = new Color32(164, 128, 62, 255);   // arrow shaft
        Color32 tip    = new Color32(192, 194, 196, 255);  // arrowhead

        // ── Stave (D-shape, opens right) ─────────────────────────
        // Main vertical spine
        FillRect(tex, 10, 10, 5, 44, wood);
        // Upper limb curves rightward
        FillRect(tex, 15, 48, 5, 4, wood);
        FillRect(tex, 19, 52, 4, 4, wood);
        FillRect(tex, 23, 54, 3, 3, wood);
        // Lower limb curves rightward
        FillRect(tex, 15, 12, 5, 4, wood);
        FillRect(tex, 19, 8,  4, 4, wood);
        FillRect(tex, 23, 6,  3, 3, wood);

        // ── Bowstring ─────────────────────────────────────────────
        FillRect(tex, 25, 6, 2, 52, str);

        // ── Arrow (nocked, pointing right) ────────────────────────
        FillRect(tex, 25, 31, 30, 2, shaft);      // shaft
        FillRect(tex, 54, 29,  4,  6, tip);       // arrowhead (wider)
        FillRect(tex, 57, 31,  4,  2, tip);       // arrowhead tip
        FillRect(tex, 23, 29,  4,  4, str);       // fletching
    }

    private static void DrawLongBow(Texture2D tex)
    {
        // Background: dark forest green
        Fill(tex, new Color32(10, 46, 14, 255));

        Color32 wood  = new Color32(162, 122, 52, 255);
        Color32 str   = new Color32(210, 208, 182, 255);
        Color32 shaft = new Color32(148, 116, 52, 255);
        Color32 tip   = new Color32(186, 190, 192, 255);

        // ── Stave — taller and slimmer than shortbow ───────────────
        FillRect(tex, 10, 4, 4, 56, wood);
        // Upper limb
        FillRect(tex, 14, 54, 5, 4, wood);
        FillRect(tex, 18, 58, 5, 3, wood);
        FillRect(tex, 22, 60, 4, 2, wood);
        // Lower limb
        FillRect(tex, 14, 6,  5, 4, wood);
        FillRect(tex, 18, 3,  5, 3, wood);
        FillRect(tex, 22, 2,  4, 2, wood);

        // ── String ────────────────────────────────────────────────
        FillRect(tex, 25, 2, 2, 60, str);

        // ── Arrow ─────────────────────────────────────────────────
        FillRect(tex, 25, 31, 32, 2, shaft);
        FillRect(tex, 56, 29,  4,  6, tip);
        FillRect(tex, 59, 31,  3,  2, tip);
        FillRect(tex, 23, 29,  4,  4, str);
    }

    private static void DrawCrossbow(Texture2D tex)
    {
        // Background: dark maroon
        Fill(tex, new Color32(72, 14, 14, 255));

        Color32 stock = new Color32(152, 104, 46, 255);   // wooden stock
        Color32 metal = new Color32(196, 198, 202, 255);  // prod / limbs
        Color32 bolt  = new Color32(214, 210, 188, 255);  // bolt / string
        Color32 dark  = new Color32(80,  60,  30,  255);  // detail shadow

        // ── Stock (horizontal body, left-heavy) ───────────────────
        FillRect(tex, 6,  26, 46, 12, stock);   // main stock body
        FillRect(tex, 4,  24,  6, 16, stock);   // butt plate
        // Narrow wrist / grip area
        FillRect(tex, 20, 20, 10, 18, stock);
        // Trigger guard
        FillRect(tex, 22, 14,  6, 8, dark);
        FillRect(tex, 20, 14,  2, 4, dark);

        // ── Prod (vertical bow sitting on top of tiller) ──────────
        // Tiller (the slot the prod sits in)
        FillRect(tex, 30, 36,  6, 10, dark);
        // Left limb
        FillRect(tex,  8, 40, 26,  5, metal);
        FillRect(tex,  6, 38,  6,  9, metal);   // left tip
        // Right limb
        FillRect(tex, 36, 40, 22,  5, metal);
        FillRect(tex, 54, 38,  6,  9, metal);   // right tip

        // ── String (horizontal) ───────────────────────────────────
        FillRect(tex, 6, 43, 54, 2, bolt);

        // ── Bolt (sitting in groove, pointing right) ──────────────
        FillRect(tex, 30, 31, 24, 2, bolt);
        FillRect(tex, 52, 29,  5, 6, metal);   // bolt head
        FillRect(tex, 28, 29,  4, 4, bolt);    // bolt fletching
    }

    private static void DrawThrowingSpear(Texture2D tex)
    {
        // Background: dark olive
        Fill(tex, new Color32(36, 50, 10, 255));

        Color32 shaft = new Color32(168, 124, 52, 255);
        Color32 tip   = new Color32(198, 202, 206, 255);
        Color32 butt  = new Color32(210, 210, 200, 255);
        Color32 wrap  = new Color32(130,  90,  35, 255);  // grip wrap

        // ── Diagonal shaft (bottom-left to top-right, slope ~1:1) ─
        // Draw as a series of 4×5 rects stepping diagonally
        int[] xs = { 2, 6, 10, 14, 18, 22, 26, 30, 34, 38, 42 };
        int[] ys = { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44 };
        for (int i = 0; i < xs.Length; i++)
            FillRect(tex, xs[i], ys[i], 7, 5, shaft);

        // ── Grip wrap (two bands near the lower-left third) ───────
        FillRect(tex,  8, 10, 7, 3, wrap);
        FillRect(tex, 14, 16, 7, 3, wrap);

        // ── Butt cap (bottom-left end) ────────────────────────────
        FillRect(tex, 2, 4, 5, 5, butt);

        // ── Tip (spearhead — diamond shape at top-right) ──────────
        FillRect(tex, 44, 44, 8, 5, tip);
        FillRect(tex, 48, 49, 6, 5, tip);
        FillRect(tex, 50, 54, 5, 4, tip);
        FillRect(tex, 52, 58, 4, 3, tip);
        FillRect(tex, 54, 60, 3, 2, tip);
        // Tip highlight (bright edge)
        FillRect(tex, 56, 60, 2, 2, new Color32(230, 235, 240, 255));
    }

    private static void DrawThrowingAxe(Texture2D tex)
    {
        // Background: steel blue-gray
        Fill(tex, new Color32(20, 30, 54, 255));

        Color32 handle = new Color32(156, 112, 50, 255);
        Color32 head   = new Color32(185, 190, 198, 255);
        Color32 edge   = new Color32(230, 234, 240, 255);  // cutting edge highlight
        Color32 dark   = new Color32(110, 115, 122, 255);  // head shadow

        // ── Handle (diagonal, bottom-right to center-left) ────────
        FillRect(tex, 36,  4, 6, 30, handle);   // vertical handle
        FillRect(tex, 34,  4, 8,  5, handle);   // butt cap
        // Grip rings
        FillRect(tex, 35, 10, 7, 2, new Color32(120, 84, 36, 255));
        FillRect(tex, 35, 16, 7, 2, new Color32(120, 84, 36, 255));

        // ── Axe head (mounted at top of handle) ───────────────────
        // Eye socket (where handle enters head)
        FillRect(tex, 32, 32, 14, 10, head);

        // Blade body — sweeps left and upward (crescent)
        FillRect(tex, 12, 38, 24, 14, head);
        FillRect(tex, 10, 36, 16, 18, head);
        FillRect(tex,  8, 34, 10, 20, head);
        FillRect(tex,  6, 36,  6, 16, head);

        // Blade tip top arc
        FillRect(tex, 14, 52, 22,  6, head);
        FillRect(tex, 10, 50, 10,  4, head);

        // Blade tip bottom arc
        FillRect(tex, 10, 34, 12,  4, head);
        FillRect(tex,  8, 36,  6,  4, head);

        // Poll (butt of head, right side)
        FillRect(tex, 44, 33, 10, 10, dark);
        FillRect(tex, 46, 31,  8,  4, dark);

        // ── Cutting edge (bright highlight on left blade arc) ──────
        FillRect(tex,  4, 36,  3, 16, edge);
        FillRect(tex,  6, 32,  4,  4, edge);
        FillRect(tex,  6, 52,  4,  4, edge);
    }

    // -----------------------------------------------------------------------
    // Asset creation & assignment (mirrors GunIconCreator pattern)
    // -----------------------------------------------------------------------

    private static void GenerateAndAssign(
        string weaponItemFile,
        System.Action<Texture2D> drawFunc,
        string iconFileName)
    {
        Texture2D tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        drawFunc(tex);
        tex.Apply();

        string assetPath    = OutputPath + "/" + iconFileName;
        string relativePart = assetPath.Substring("Assets/".Length);
        string fullPath     = Path.Combine(Application.dataPath, relativePart).Replace('\\', '/');

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // First import — creates the AssetImporter entry in Unity's database
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        // Configure and force-synchronous reimport so the sprite sub-asset exists immediately
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = IconSize;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
        }
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        // Find the Sprite sub-asset (LoadAssetAtPath<Sprite> can miss it on first import)
        Sprite sprite = null;
        foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (a is Sprite s) { sprite = s; break; }
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[RangedIconCreator] Could not load sprite from '{assetPath}' after import.");
            return;
        }

        string itemPath = $"{WeaponsPath}/{weaponItemFile}";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        if (item == null)
        {
            Debug.LogWarning($"[RangedIconCreator] WeaponItem not found at '{itemPath}'. " +
                             "Run 'Voidborne > Create Ranged Weapon Assets' first.");
            return;
        }

        item.icon = sprite;
        EditorUtility.SetDirty(item);
        Debug.Log($"[RangedIconCreator] Assigned '{iconFileName}' to '{item.displayName}'.");
    }

    // -----------------------------------------------------------------------
    // Pixel helpers
    // -----------------------------------------------------------------------

    private static void Fill(Texture2D tex, Color32 color)
    {
        Color32[] pixels = new Color32[IconSize * IconSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels32(pixels);
    }

    private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color32 color)
    {
        for (int py = y; py < y + h && py < IconSize; py++)
            for (int px = x; px < x + w && px < IconSize; px++)
                if (px >= 0 && py >= 0)
                    tex.SetPixel(px, py, color);
    }
}
