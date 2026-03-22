using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates simple procedural 32x32 Sprite icons for ItemDefinitions when no
/// art asset is assigned. Sprites are cached so they are only created once.
/// </summary>
public static class ItemIconGenerator
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Returns the icon for the given ItemDefinition.
    /// Uses ItemDefinition.icon if assigned; otherwise generates a procedural one.
    /// </summary>
    public static Sprite GetIcon(ItemDefinition itemDef)
    {
        if (itemDef == null) return null;
        if (itemDef.icon != null) return itemDef.icon;

        string key = itemDef.itemId;
        if (_cache.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D tex = GenerateTexture(itemDef);
        Sprite sprite = Texture2DToSprite(tex);
        _cache[key] = sprite;
        return sprite;
    }

    /// <summary>Returns the color associated with an ItemType (used for tooltip borders etc.).</summary>
    public static Color GetTypeColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.Resource:   return new Color(0.2f, 0.8f, 0.2f);
            case ItemType.Tool:       return new Color(0.6f, 0.6f, 0.6f);
            case ItemType.Weapon:     return new Color(0.9f, 0.2f, 0.2f);
            case ItemType.Armor:      return new Color(0.2f, 0.4f, 0.9f);
            case ItemType.Consumable: return new Color(0.9f, 0.85f, 0.1f);
            case ItemType.Block:      return new Color(0.76f, 0.60f, 0.42f);
            case ItemType.Machine:    return new Color(0.0f, 0.85f, 0.85f);
            case ItemType.Backpack:    return new Color(0.6f, 0.2f, 0.8f);
            case ItemType.VehiclePart: return new Color(0.85f, 0.55f, 0.15f);
            default:                   return Color.white;
        }
    }

    // ---------------------------------------------------------------
    //  Per-item texture generation
    // ---------------------------------------------------------------

    private static Texture2D GenerateTexture(ItemDefinition itemDef)
    {
        // Vehicle parts: generate icon by part type rather than individual itemId
        if (itemDef is Voidborne.Vehicles.VehiclePartItem vpi)
            return DrawVehiclePartByType(vpi.partType);

        switch (itemDef.itemId)
        {
            case "wood":         return DrawWood();
            case "stone":        return DrawStone();
            case "iron_ore":     return DrawIronOre();
            case "iron_ingot":   return DrawIronIngot();
            case "coal":         return DrawCoal();
            case "copper_ore":   return DrawCopperOre();
            case "copper_ingot": return DrawCopperIngot();
            case "stick":        return DrawStick();
            case "wood_planks":  return DrawWoodPlanks();
            case "torch":        return DrawTorch();
            case "wood_pickaxe":   return DrawPickaxe(new Color(0.72f, 0.56f, 0.32f));
            case "stone_pickaxe":  return DrawPickaxe(new Color(0.55f, 0.55f, 0.55f));
            case "iron_pickaxe":   return DrawPickaxe(new Color(0.65f, 0.65f, 0.70f));
            case "wood_axe":       return DrawAxe(new Color(0.72f, 0.56f, 0.32f));
            case "stone_axe":      return DrawAxe(new Color(0.55f, 0.55f, 0.55f));
            case "iron_axe":       return DrawAxe(new Color(0.65f, 0.65f, 0.70f));
            case "sand":           return DrawSand();
            case "glass":          return DrawGlass();
            case "raw_meat":       return DrawRawMeat();
            case "cooked_meat":    return DrawCookedMeat();
            case "furnace":        return DrawFurnace();
            case "workbench":      return DrawWorkbench();

            // Firearms
            case "revolver":       return DrawRevolver();
            case "rattler_smg":    return DrawSMG();
            case "ironbark_ar":    return DrawAR();
            case "pump_shotgun":   return DrawShotgun();
            case "bolt_sniper":    return DrawSniper();

            // Melee weapons
            case "wooden_club":    return DrawWoodenClub();
            case "iron_dagger":    return DrawIronDagger();
            case "iron_longsword": return DrawIronLongsword();
            case "titanium_spear": return DrawTitaniumSpear();
            case "void_warhammer": return DrawVoidWarhammer();

            // Electricity items
            case "burn_generator":   return DrawBurnGenerator();
            case "thermal_tap":      return DrawThermalTap();
            case "wind_rotor":       return DrawWindRotor();
            case "battery_bank":     return DrawBatteryBank();
            case "junction_box":     return DrawJunctionBox();
            case "wire":             return DrawWire();
            case "power_cable":      return DrawPowerCable();
            case "powered_light":    return DrawPoweredLight();
            case "powered_door":     return DrawPoweredDoor();
            case "power_probe":      return DrawPowerProbe();

            // Backpacks
            case "satchel":         return DrawSatchel();
            case "travel_pack":     return DrawTravelPack();
            case "expedition_rig":  return DrawExpeditionRig();
            case "void_pocket":     return DrawVoidPocket();

            // Automation items (Vol 8.1)
            case "conveyor_belt":       return DrawConveyorBelt();
            case "fast_conveyor_belt":  return DrawFastConveyorBelt();
            case "slope_belt":          return DrawSlopeBelt();
            case "split_belt":          return DrawSplitBelt();
            case "pneumatic_tube":      return DrawPneumaticTube();
            case "hopper":              return DrawHopper();
            case "filter_hopper":       return DrawFilterHopper();
            case "belt_sorter":         return DrawBeltSorter();
            case "overflow_valve":      return DrawOverflowValve();

            // Building pieces
            case "build_wood_foundation":      return DrawBuildFoundation();
            case "build_wood_trifoundation":   return DrawBuildTriFoundation();
            case "build_wood_wall":            return DrawBuildWall();
            case "build_wood_doorway":         return DrawBuildDoor();
            case "build_wood_window":          return DrawBuildWindow();
            case "build_wood_floor":           return DrawBuildFloor();
            case "build_wood_trifloor":        return DrawBuildTriFloor();
            case "build_wood_stairs":          return DrawBuildStairs();
            case "build_wood_pillar":          return DrawBuildPillar();
            case "build_wood_halfwall":        return DrawBuildHalfWall();

            // Automation machines (Vol 8.2 & 8.3)
            case "AutoMinerItem":          return DrawAutoMiner();
            case "DronePortItem":          return DrawDronePort();
            case "ElectricFurnaceItem":    return DrawElectricFurnace();
            case "GrinderItem":            return DrawGrinder();
            case "PressItem":              return DrawPress();
            case "AssemblerItem":          return DrawAssembler();
            case "CircuitEtcherItem":      return DrawCircuitEtcher();
            case "ComponentPressItem":     return DrawComponentPress();
            case "BatteryFabricatorItem":  return DrawBatteryFabricator();
            case "SchematicCardItem":      return DrawSchematicCard();

            // Advanced generators (Vol 8.4)
            case "SolarPanelItem":         return DrawSolarPanel();
            case "VoidFilamentItem":       return DrawVoidFilament();

            // Storage drives (Vol 8.5)
            case "Drive_RawMaterials":     return DrawDrive(new Color(0.55f, 0.35f, 0.10f));
            case "Drive_Components":       return DrawDrive(new Color(0.10f, 0.55f, 0.80f));
            case "Drive_Fuel":             return DrawDrive(new Color(0.80f, 0.35f, 0.05f));
            case "Drive_Misc":             return DrawDrive(new Color(0.45f, 0.45f, 0.50f));
            case "DriveRepairKit":         return DrawDriveRepairKit();

            // Enemy drops
            case "directiveshard":              return DrawDirectiveShard();
            case "reinforcedplating":           return DrawReinforcedPlating();
            case "cortexcomponents":            return DrawCortexComponents();
            case "energycell":                  return DrawEnergyCell();
            case "weaponcomponents":            return DrawWeaponComponents();
            case "chitin":                      return DrawChitin();
            case "fracturedmemoryshard_generic":return DrawFracturedMemoryShard();
            case "schematicfragment_generic":   return DrawSchematicFragment();

            // Vehicles & Vehicle Workbench (Vol 9)
            case "vehicle_workbench":    return DrawVehicleWorkbench();
            case "pushcart_default":     return DrawPushcart();
            case "buggy_default":        return DrawBuggy();
            case "cycle_default":        return DrawCycle();
            case "hauler_default":       return DrawHauler();
            case "drillrig_default":     return DrawDrillRig();
            case "gyrocopter_default":   return DrawGyrocopter();

            // Flare gun
            case "flare_gun":            return DrawFlareGun();

            // DevBackpack
            case "dev_backpack":         return DrawDevBackpack();

            // Wire tiers (Vol 10.4)
            case "copper_wire":          return DrawWireTier(new Color(0.85f, 0.55f, 0.20f), 1);
            case "insulated_wire":       return DrawWireTier(new Color(0.30f, 0.30f, 0.35f), 2);
            case "heavy_duty_cable":     return DrawWireTier(new Color(0.50f, 0.50f, 0.55f), 3);

            // Storage tiers (Vol 10.4)
            case "wood_chest":           return DrawChest(new Color(0.72f, 0.56f, 0.32f));
            case "iron_chest":           return DrawChest(new Color(0.65f, 0.65f, 0.70f));
            case "compression_chest":    return DrawCompressionChest();

            // Storage network items (Vol 10.4)
            case "storage_drive_item":   return DrawDrive(new Color(0.30f, 0.70f, 0.30f));
            case "drive_rack_item":      return DrawDriveRack();
            case "terminal_item":        return DrawTerminal();
            case "network_cable_item":   return DrawNetworkCable();

            default:               return DrawFallback(itemDef.itemType);
        }
    }

    // ---------------------------------------------------------------
    //  Individual icon drawers
    // ---------------------------------------------------------------

    // Wood — warm brown rectangle with darker rings
    private static Texture2D DrawWood()
    {
        var tex = NewTex();
        Color bg = new Color(0.55f, 0.34f, 0.14f);
        Color ring = new Color(0.38f, 0.22f, 0.08f);
        Color light = new Color(0.65f, 0.42f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, bg);
        // Outer ring (ellipse at ~radius 13)
        DrawEllipseRing(tex, 16, 16, 13, 10, 1, ring);
        // Middle ring
        DrawEllipseRing(tex, 16, 16, 8, 6, 1, ring);
        // Centre dot
        FillCircle(tex, 16, 16, 3, ring);
        // Highlight
        FillCircle(tex, 12, 12, 2, light);
        tex.Apply();
        return tex;
    }

    // Stone — grey circle with crack lines
    private static Texture2D DrawStone()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color grey = new Color(0.55f, 0.55f, 0.55f);
        Color dark = new Color(0.35f, 0.35f, 0.35f);
        Color light = new Color(0.70f, 0.70f, 0.70f);
        FillCircle(tex, 16, 16, 14, grey);
        // cracks
        DrawLine(tex, 16, 16, 22, 8, dark);
        DrawLine(tex, 16, 16, 10, 6, dark);
        DrawLine(tex, 16, 16, 8, 22, dark);
        DrawLine(tex, 16, 16, 24, 24, dark);
        // highlight
        FillCircle(tex, 12, 20, 3, light);
        tex.Apply();
        return tex;
    }

    // Iron Ore — dark grey background with orange-red flecks
    private static Texture2D DrawIronOre()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color bg = new Color(0.35f, 0.35f, 0.36f);
        Color fleck = new Color(0.75f, 0.35f, 0.15f);
        Color dark = new Color(0.22f, 0.22f, 0.24f);
        FillCircle(tex, 16, 16, 14, bg);
        // Vein-like dark areas
        FillCircle(tex, 12, 12, 5, dark);
        FillCircle(tex, 20, 19, 4, dark);
        // Orange flecks
        FillCircle(tex, 14, 10, 2, fleck);
        FillCircle(tex, 20, 15, 2, fleck);
        FillCircle(tex, 11, 20, 2, fleck);
        FillCircle(tex, 22, 22, 1, fleck);
        tex.Apply();
        return tex;
    }

    // Iron Ingot — metallic silver rectangle with shine stripe
    private static Texture2D DrawIronIngot()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color silver = new Color(0.65f, 0.65f, 0.70f);
        Color dark = new Color(0.40f, 0.40f, 0.44f);
        Color shine = new Color(0.90f, 0.90f, 0.95f);
        // Ingot body (trapezoid feel via rectangles)
        FillRect(tex, 4, 9, 24, 14, dark);   // shadow base
        FillRect(tex, 4, 8, 24, 14, silver); // main face
        // Top bevel
        FillRect(tex, 5, 8, 22, 2, shine);
        // Shine stripe diagonal-ish
        for (int y = 9; y < 22; y++)
        {
            int x = 7 + (y - 9) / 2;
            if (x < 28) SetPixelSafe(tex, x, y, shine);
        }
        tex.Apply();
        return tex;
    }

    // Coal — near-black irregular blob with blue sheen
    private static Texture2D DrawCoal()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color coal = new Color(0.12f, 0.12f, 0.14f);
        Color sheen = new Color(0.20f, 0.22f, 0.35f);
        Color dark = new Color(0.06f, 0.06f, 0.08f);
        // Irregular blob via overlapping circles
        FillCircle(tex, 16, 16, 12, coal);
        FillCircle(tex, 11, 14, 7, coal);
        FillCircle(tex, 20, 18, 6, coal);
        FillCircle(tex, 14, 20, 5, coal);
        // Dark crevice
        FillCircle(tex, 18, 13, 3, dark);
        // Blue sheen
        FillCircle(tex, 13, 19, 3, sheen);
        FillCircle(tex, 20, 12, 2, sheen);
        tex.Apply();
        return tex;
    }

    // Copper Ore — brown-grey rock with teal/green flecks
    private static Texture2D DrawCopperOre()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color bg = new Color(0.42f, 0.38f, 0.34f);
        Color dark = new Color(0.28f, 0.24f, 0.22f);
        Color teal = new Color(0.20f, 0.70f, 0.55f);
        FillCircle(tex, 16, 16, 14, bg);
        FillCircle(tex, 13, 13, 5, dark);
        FillCircle(tex, 20, 18, 4, dark);
        // Teal/green flecks
        FillCircle(tex, 15, 11, 2, teal);
        FillCircle(tex, 21, 14, 2, teal);
        FillCircle(tex, 12, 21, 2, teal);
        FillCircle(tex, 22, 23, 1, teal);
        tex.Apply();
        return tex;
    }

    // Copper Ingot — orange-copper rectangle with shine
    private static Texture2D DrawCopperIngot()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color copper = new Color(0.78f, 0.44f, 0.16f);
        Color dark = new Color(0.50f, 0.28f, 0.10f);
        Color shine = new Color(0.95f, 0.70f, 0.40f);
        FillRect(tex, 4, 9, 24, 14, dark);
        FillRect(tex, 4, 8, 24, 14, copper);
        FillRect(tex, 5, 8, 22, 2, shine);
        for (int y = 9; y < 22; y++)
        {
            int x = 7 + (y - 9) / 2;
            if (x < 28) SetPixelSafe(tex, x, y, shine);
        }
        tex.Apply();
        return tex;
    }

    // Stick — thin tan vertical bar
    private static Texture2D DrawStick()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color tan = new Color(0.72f, 0.56f, 0.32f);
        Color dark = new Color(0.52f, 0.38f, 0.18f);
        Color light = new Color(0.85f, 0.72f, 0.50f);
        // Main stick body — 4px wide centered
        FillRect(tex, 13, 2, 4, 28, dark);
        FillRect(tex, 14, 2, 3, 28, tan);
        // Highlight
        FillRect(tex, 14, 2, 1, 28, light);
        tex.Apply();
        return tex;
    }

    // Wood Planks — light tan with horizontal plank lines
    private static Texture2D DrawWoodPlanks()
    {
        var tex = NewTex();
        Color plank = new Color(0.76f, 0.60f, 0.38f);
        Color line = new Color(0.52f, 0.38f, 0.20f);
        Color light = new Color(0.88f, 0.74f, 0.54f);
        FillRect(tex, 0, 0, 32, 32, plank);
        // Two horizontal plank dividers
        FillRect(tex, 0, 10, 32, 2, line);
        FillRect(tex, 0, 20, 32, 2, line);
        // Vertical grain on each plank row
        for (int x = 0; x < 32; x += 6)
            DrawLine(tex, x, 0, x + 2, 32, line);
        // Highlight strip on each plank
        FillRect(tex, 0, 1, 32, 1, light);
        FillRect(tex, 0, 12, 32, 1, light);
        FillRect(tex, 0, 22, 32, 1, light);
        tex.Apply();
        return tex;
    }

    // Torch — brown stick base with flame on top
    private static Texture2D DrawTorch()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color stick = new Color(0.52f, 0.35f, 0.18f);
        Color orange = new Color(1.0f, 0.55f, 0.05f);
        Color yellow = new Color(1.0f, 0.90f, 0.20f);
        Color red = new Color(0.9f, 0.20f, 0.05f);
        // Stick
        FillRect(tex, 13, 14, 4, 16, stick);
        // Flame — tiered circles narrowing upward
        FillCircle(tex, 15, 13, 5, orange);
        FillCircle(tex, 15, 10, 4, orange);
        FillCircle(tex, 15, 8, 3, yellow);
        FillCircle(tex, 16, 11, 2, red);
        FillCircle(tex, 15, 6, 2, yellow);
        // Tip flicker
        SetPixelSafe(tex, 15, 4, yellow);
        SetPixelSafe(tex, 14, 5, orange);
        SetPixelSafe(tex, 16, 5, orange);
        tex.Apply();
        return tex;
    }

    // Pickaxe — handle + angled head in the given head color
    private static Texture2D DrawPickaxe(Color headColor)
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color handle = new Color(0.62f, 0.44f, 0.22f);
        Color handleLight = new Color(0.78f, 0.60f, 0.38f);
        Color headDark = new Color(headColor.r * 0.6f, headColor.g * 0.6f, headColor.b * 0.6f);
        Color headShine = new Color(Mathf.Min(headColor.r * 1.3f, 1f), Mathf.Min(headColor.g * 1.3f, 1f), Mathf.Min(headColor.b * 1.3f, 1f));
        // Handle (diagonal, bottom-left to mid)
        DrawLine(tex, 6, 4, 20, 26, handle);
        DrawLine(tex, 7, 4, 21, 26, handle);
        DrawLine(tex, 8, 4, 22, 26, handleLight);
        // Pickaxe head (horizontal bar across the top)
        FillRect(tex, 6, 20, 20, 4, headDark);
        FillRect(tex, 6, 21, 20, 3, headColor);
        FillRect(tex, 6, 21, 20, 1, headShine);
        // Left pick tip
        FillRect(tex, 4, 19, 4, 6, headColor);
        FillRect(tex, 4, 19, 1, 3, headShine);
        // Right pick tip
        FillRect(tex, 24, 19, 4, 6, headColor);
        FillRect(tex, 27, 19, 1, 3, headShine);
        tex.Apply();
        return tex;
    }

    // Axe — diagonal handle with wedge-shaped head
    private static Texture2D DrawAxe(Color headColor)
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color handle = new Color(0.62f, 0.44f, 0.22f);
        Color handleLight = new Color(0.78f, 0.60f, 0.38f);
        Color headDark = new Color(headColor.r * 0.6f, headColor.g * 0.6f, headColor.b * 0.6f);
        Color headShine = new Color(Mathf.Min(headColor.r * 1.3f, 1f), Mathf.Min(headColor.g * 1.3f, 1f), Mathf.Min(headColor.b * 1.3f, 1f));
        // Handle (diagonal, bottom-left to mid)
        DrawLine(tex, 6, 4, 20, 26, handle);
        DrawLine(tex, 7, 4, 21, 26, handle);
        DrawLine(tex, 8, 4, 22, 26, handleLight);
        // Axe head — wedge shape on the right side of the handle top
        FillRect(tex, 18, 20, 8, 8, headColor);
        FillRect(tex, 18, 20, 8, 1, headShine);
        FillRect(tex, 18, 20, 1, 8, headShine);
        // Blade edge (curved look via stair-step)
        FillRect(tex, 26, 18, 2, 12, headColor);
        FillRect(tex, 27, 17, 2, 14, headDark);
        // Blade shine
        FillRect(tex, 27, 19, 1, 8, headShine);
        // Back of head
        FillRect(tex, 16, 22, 3, 4, headDark);
        tex.Apply();
        return tex;
    }

    // Sand — warm tan field with scattered grain pixels
    private static Texture2D DrawSand()
    {
        var tex = NewTex();
        Color bg     = new Color(0.90f, 0.80f, 0.45f);
        Color dark   = new Color(0.72f, 0.62f, 0.28f);
        Color bright = new Color(0.98f, 0.92f, 0.65f);
        FillRect(tex, 0, 0, 32, 32, bg);
        // Scattered grain pixels — fixed pattern to look consistent
        int[] gx = { 3, 7,12,18,24,28, 5,15,22, 9, 2,19,27,11,16,25, 6,14,20,29, 8,13,21,26 };
        int[] gy = { 5, 3, 8, 4, 6, 2,14,12,15,18,24,22,20,26,28,25,30,29,27,23, 9,17,10,19 };
        for (int i = 0; i < gx.Length; i++)
            SetPixelSafe(tex, gx[i], gy[i], i % 2 == 0 ? dark : bright);
        tex.Apply();
        return tex;
    }

    // Glass — pale blue pane with white edge highlight and diagonal shine
    private static Texture2D DrawGlass()
    {
        var tex = NewTex();
        Color pane  = new Color(0.65f, 0.85f, 0.95f, 0.85f);
        Color edge  = new Color(0.92f, 0.96f, 1.00f);
        Color shine = new Color(1.00f, 1.00f, 1.00f, 0.70f);
        FillRect(tex, 2, 2, 28, 28, pane);
        // Edges
        FillRect(tex, 2,  2, 28, 2, edge);
        FillRect(tex, 2, 28, 28, 2, edge);
        FillRect(tex,  2, 2, 2, 28, edge);
        FillRect(tex, 28, 2, 2, 28, edge);
        // Diagonal shine streaks
        DrawLine(tex,  4, 28, 28,  4, shine);
        DrawLine(tex,  4, 20, 20,  4, shine);
        tex.Apply();
        return tex;
    }

    // Raw Meat — pink/red slab with pale fat streaks
    private static Texture2D DrawRawMeat()
    {
        var tex = NewTex();
        Color meat = new Color(0.85f, 0.38f, 0.38f);
        Color fat  = new Color(0.95f, 0.82f, 0.80f);
        Color dark = new Color(0.60f, 0.18f, 0.18f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 8, 22, 16, meat);
        FillRect(tex, 7, 6, 18, 20, meat);
        // Fat marbling
        FillRect(tex, 8,  10, 6, 3, fat);
        FillRect(tex, 18, 16, 6, 3, fat);
        FillRect(tex, 12, 20, 8, 2, fat);
        // Dark shadow base
        FillRect(tex, 7, 24, 18, 2, dark);
        tex.Apply();
        return tex;
    }

    // Cooked Meat — brown slab with black grill marks
    private static Texture2D DrawCookedMeat()
    {
        var tex = NewTex();
        Color meat  = new Color(0.55f, 0.28f, 0.10f);
        Color light = new Color(0.72f, 0.44f, 0.18f);
        Color dark  = new Color(0.35f, 0.16f, 0.05f);
        Color grill = new Color(0.12f, 0.08f, 0.06f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 8, 22, 16, meat);
        FillRect(tex, 7, 6, 18, 20, meat);
        // Highlight top
        FillRect(tex, 8, 7, 14, 2, light);
        // Grill marks
        FillRect(tex, 6, 12, 20, 2, grill);
        FillRect(tex, 6, 18, 20, 2, grill);
        // Shadow base
        FillRect(tex, 7, 24, 18, 2, dark);
        tex.Apply();
        return tex;
    }

    // Furnace — dark stone box with orange fire glow in a door opening
    private static Texture2D DrawFurnace()
    {
        var tex = NewTex();
        Color stone = new Color(0.40f, 0.40f, 0.42f);
        Color dark  = new Color(0.24f, 0.24f, 0.26f);
        Color door  = new Color(0.10f, 0.08f, 0.08f);
        Color glow  = new Color(1.00f, 0.52f, 0.08f);
        Color core  = new Color(1.00f, 0.92f, 0.40f);
        FillRect(tex, 2, 2, 28, 28, stone);
        // Shading
        FillRect(tex, 2, 28, 28, 2, dark);
        FillRect(tex, 28, 2, 2, 28, dark);
        // Door opening
        FillRect(tex, 8, 7, 16, 14, door);
        // Fire glow inside the door
        FillCircle(tex, 16, 14, 6, glow);
        FillCircle(tex, 16, 12, 3, core);
        tex.Apply();
        return tex;
    }

    // Workbench — brown box with lighter top and scratch marks
    private static Texture2D DrawWorkbench()
    {
        var tex = NewTex();
        Color side    = new Color(0.52f, 0.32f, 0.12f);
        Color top     = new Color(0.70f, 0.50f, 0.22f);
        Color dark    = new Color(0.34f, 0.18f, 0.06f);
        Color scratch = new Color(0.42f, 0.26f, 0.08f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Body
        FillRect(tex, 2, 4, 28, 22, side);
        // Top face
        FillRect(tex, 2, 22, 28, 7, top);
        // Dark edges
        FillRect(tex, 2, 4, 28, 2, dark);
        FillRect(tex, 2, 4, 2, 24, dark);
        FillRect(tex, 28, 4, 2, 24, dark);
        // Scratch marks on top surface
        DrawLine(tex,  6, 24, 14, 28, scratch);
        DrawLine(tex, 16, 23, 26, 27, scratch);
        DrawLine(tex,  8, 22, 12, 28, scratch);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Gun icons — distinct silhouettes on unique colored backgrounds
    // ---------------------------------------------------------------

    // Revolver — dark navy, compact cylinder-frame pistol
    private static Texture2D DrawRevolver()
    {
        var tex = NewTex();
        Color bg   = new Color(0.07f, 0.14f, 0.38f);
        Color gun  = new Color(0.86f, 0.86f, 0.86f);
        Color dark = new Color(0.55f, 0.55f, 0.55f);
        Color blk  = new Color(0.15f, 0.15f, 0.15f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex, 18, 15,  9,  4, gun);   // barrel
        FillRect(tex,  9, 13, 11,  7, gun);   // frame / cylinder
        FillRect(tex, 10,  7,  5,  7, dark);  // grip
        FillRect(tex, 11,  6,  4,  2, dark);  // grip base
        FillRect(tex, 18, 19,  2,  3, dark);  // hammer spur
        FillRect(tex, 25, 15,  2,  3, blk);   // muzzle
        FillRect(tex, 25, 18,  1,  1, gun);   // front sight
        FillRect(tex, 10, 17,  7,  1, blk);   // cylinder detail
        FillRect(tex, 10, 15,  7,  1, blk);
        tex.Apply();
        return tex;
    }

    // SMG — burnt orange, compact boxy receiver with vertical magazine
    private static Texture2D DrawSMG()
    {
        var tex = NewTex();
        Color bg   = new Color(0.50f, 0.22f, 0.02f);
        Color gun  = new Color(0.88f, 0.84f, 0.76f);
        Color dark = new Color(0.55f, 0.51f, 0.43f);
        Color blk  = new Color(0.12f, 0.10f, 0.06f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex,  6, 13, 18,  6, gun);   // receiver
        FillRect(tex, 24, 14,  6,  4, gun);   // short barrel
        FillRect(tex, 28, 14,  2,  5, dark);  // muzzle device
        FillRect(tex,  3, 14,  4,  4, dark);  // folded stock
        FillRect(tex,  9,  7,  5,  7, dark);  // grip
        FillRect(tex, 14,  3,  5, 11, dark);  // vertical magazine
        FillRect(tex, 14, 13,  1,  2, blk);   // mag catch
        FillRect(tex, 18, 19,  2,  2, blk);   // charging handle
        FillRect(tex,  8, 19, 10,  1, dark);  // top rail
        FillRect(tex, 10, 20,  1,  2, gun);   // front sight
        tex.Apply();
        return tex;
    }

    // AR — forest green, medium rifle with curved mag and carry handle
    private static Texture2D DrawAR()
    {
        var tex = NewTex();
        Color bg   = new Color(0.06f, 0.22f, 0.07f);
        Color gun  = new Color(0.78f, 0.84f, 0.76f);
        Color dark = new Color(0.47f, 0.53f, 0.45f);
        Color wood = new Color(0.62f, 0.44f, 0.20f);
        Color blk  = new Color(0.08f, 0.12f, 0.08f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex, 21, 15,  9,  3, gun);   // barrel
        FillRect(tex,  8, 13, 15,  5, gun);   // upper receiver
        FillRect(tex,  8, 10, 14,  4, dark);  // lower receiver
        FillRect(tex,  2, 12,  7,  4, dark);  // stock
        FillRect(tex,  2, 11,  4,  2, dark);  // stock cheekrest
        FillRect(tex,  9,  5,  5,  6, dark);  // grip
        FillRect(tex, 12,  2,  5,  9, dark);  // curved magazine
        FillRect(tex, 12,  9,  1,  2, blk);   // mag catch
        FillRect(tex, 11, 18,  8,  2, dark);  // carry handle
        FillRect(tex, 12, 20,  1,  2, gun);   // rear sight
        FillRect(tex, 17, 20,  1,  2, gun);   // front sight
        FillRect(tex, 20, 13,  2,  2, dark);  // gas block
        tex.Apply();
        return tex;
    }

    // Shotgun — deep red, wide dual-tube with wooden stock and pump
    private static Texture2D DrawShotgun()
    {
        var tex = NewTex();
        Color bg   = new Color(0.40f, 0.05f, 0.05f);
        Color gun  = new Color(0.86f, 0.82f, 0.72f);
        Color wood = new Color(0.55f, 0.33f, 0.16f);
        Color dark = new Color(0.55f, 0.51f, 0.43f);
        Color blk  = new Color(0.12f, 0.08f, 0.04f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex,  7, 17, 22,  4, gun);   // upper barrel tube
        FillRect(tex,  9, 13, 19,  4, gun);   // lower barrel / mag tube
        FillRect(tex,  4, 12,  8,  9, dark);  // receiver block
        FillRect(tex,  2, 11,  6,  6, wood);  // wooden stock
        FillRect(tex,  2, 10,  4,  2, wood);  // cheekrest
        FillRect(tex,  6,  7,  5,  6, wood);  // grip
        FillRect(tex, 15, 11,  7,  3, wood);  // pump / foregrip
        FillRect(tex, 27, 17,  2,  3, blk);   // upper muzzle
        FillRect(tex, 27, 13,  2,  3, blk);   // lower muzzle
        FillRect(tex, 10, 17,  4,  2, blk);   // ejection port
        FillRect(tex, 25, 21,  1,  1, gun);   // bead sight
        tex.Apply();
        return tex;
    }

    // Sniper — dark purple, very long barrel with scope and bipod
    private static Texture2D DrawSniper()
    {
        var tex = NewTex();
        Color bg    = new Color(0.19f, 0.06f, 0.30f);
        Color gun   = new Color(0.80f, 0.80f, 0.84f);
        Color dark  = new Color(0.51f, 0.51f, 0.55f);
        Color scope = new Color(0.20f, 0.35f, 0.63f);  // blue scope lens
        Color blk   = new Color(0.08f, 0.06f, 0.12f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex,  4, 15, 26,  3, gun);   // long barrel
        FillRect(tex,  6, 13, 13,  6, gun);   // receiver
        FillRect(tex,  2, 12,  6,  6, dark);  // stock
        FillRect(tex,  2, 11,  4,  2, dark);  // cheekrest
        FillRect(tex,  7,  7,  5,  7, dark);  // grip
        FillRect(tex, 11, 19, 11,  3, dark);  // scope body
        FillRect(tex, 10, 18, 13,  1, blk);   // scope mount
        FillRect(tex, 11, 20,  2,  2, scope); // scope front lens
        FillRect(tex, 19, 19,  2,  3, scope); // scope eyepiece
        FillRect(tex, 15, 22,  2,  2, dark);  // elevation turret
        FillRect(tex, 17, 13,  2,  2, dark);  // bolt handle
        FillRect(tex, 18, 11,  2,  4, dark);
        FillRect(tex,  9,  8,  2,  7, dark);  // bipod leg 1
        FillRect(tex, 13,  8,  2,  7, dark);  // bipod leg 2
        FillRect(tex,  9,  8,  2,  1, blk);   // bipod foot 1
        FillRect(tex, 13,  8,  2,  1, blk);   // bipod foot 2
        tex.Apply();
        return tex;
    }

    // Flare Gun — dark red-brown background, stubby barrel with bright orange flare tip
    private static Texture2D DrawFlareGun()
    {
        var tex = NewTex();
        Color bg    = new Color(0.28f, 0.10f, 0.06f);
        Color metal = new Color(0.55f, 0.55f, 0.58f);
        Color dark  = new Color(0.30f, 0.28f, 0.26f);
        Color grip  = new Color(0.38f, 0.24f, 0.12f);
        Color blk   = new Color(0.10f, 0.10f, 0.10f);
        Color flare = new Color(1.0f, 0.40f, 0.08f);
        Color tip   = new Color(1.0f, 0.65f, 0.15f);
        FillRect(tex, 0, 0, 32, 32, bg);
        FillRect(tex, 14, 14, 14, 5, metal);  // wide barrel
        FillRect(tex, 26, 14, 2, 5, blk);     // muzzle opening
        FillRect(tex, 27, 15, 1, 3, flare);   // loaded flare visible
        FillRect(tex,  8, 13, 10, 7, dark);   // receiver body
        FillRect(tex,  8,  6,  5, 8, grip);   // grip
        FillRect(tex,  9,  5,  4, 2, grip);   // grip base
        FillRect(tex, 14, 12,  2, 2, blk);    // trigger
        FillRect(tex, 10, 20,  3, 3, dark);   // hammer
        FillRect(tex, 28, 15,  2, 3, tip);    // flare tip glow
        // Highlight on barrel top
        FillRect(tex, 16, 18, 10, 1, new Color(0.70f, 0.70f, 0.73f));
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Melee weapon icons
    // ---------------------------------------------------------------

    // Wooden Club — warm brown background, thick rounded handle tapering to a heavy end
    private static Texture2D DrawWoodenClub()
    {
        var tex = NewTex();
        Color bg     = new Color(0.30f, 0.18f, 0.06f);   // dark wood background
        Color wood   = new Color(0.62f, 0.40f, 0.18f);   // handle
        Color head   = new Color(0.52f, 0.32f, 0.12f);   // club head (darker)
        Color shine  = new Color(0.80f, 0.60f, 0.34f);   // highlight
        Color dark   = new Color(0.32f, 0.18f, 0.06f);   // shadow
        FillRect(tex, 0, 0, 32, 32, bg);
        // Handle — thin, bottom-left to mid
        FillRect(tex, 10, 3,  4, 18, wood);
        FillRect(tex, 11, 3,  2, 18, shine);
        FillRect(tex, 13, 3,  1, 18, dark);
        // Club head — wide oval mass in top-right
        FillCircle(tex, 18, 22, 9, head);
        FillCircle(tex, 17, 23, 8, wood);
        FillCircle(tex, 15, 24, 5, shine);
        // Wood ring detail on head
        DrawEllipseRing(tex, 18, 22, 7, 6, 1, dark);
        tex.Apply();
        return tex;
    }

    // Iron Dagger — cool teal background, thin silver blade with crossguard
    private static Texture2D DrawIronDagger()
    {
        var tex = NewTex();
        Color bg    = new Color(0.04f, 0.24f, 0.28f);   // teal background
        Color blade = new Color(0.80f, 0.82f, 0.86f);   // silver blade
        Color shine = new Color(0.96f, 0.96f, 1.00f);   // edge shine
        Color dark  = new Color(0.50f, 0.52f, 0.56f);   // shadow side
        Color grip  = new Color(0.28f, 0.20f, 0.10f);   // leather grip
        Color guard = new Color(0.65f, 0.66f, 0.68f);   // crossguard
        FillRect(tex, 0, 0, 32, 32, bg);
        // Blade — narrow, diagonal (bottom-left to top-right)
        DrawLine(tex,  9,  4, 22, 17, blade);
        DrawLine(tex, 10,  4, 23, 17, blade);
        DrawLine(tex, 11,  4, 24, 17, dark);
        DrawLine(tex,  9,  5, 22, 18, shine);
        // Tip
        SetPixelSafe(tex, 9, 4, shine);
        // Crossguard
        FillRect(tex, 18, 16, 6, 3, guard);
        FillRect(tex, 19, 15, 4, 1, shine);
        FillRect(tex, 18, 19, 6, 1, dark);
        // Grip — wrapped leather
        FillRect(tex, 20, 19, 4, 8, grip);
        for (int i = 0; i < 4; i++) SetPixelSafe(tex, 20 + (i % 2 == 0 ? 0 : 3), 20 + i * 2, dark);
        // Pommel
        FillCircle(tex, 22, 28, 2, guard);
        tex.Apply();
        return tex;
    }

    // Iron Longsword — dark slate background, full cross silhouette with blade + crossguard
    private static Texture2D DrawIronLongsword()
    {
        var tex = NewTex();
        Color bg    = new Color(0.12f, 0.14f, 0.18f);   // dark slate
        Color blade = new Color(0.78f, 0.80f, 0.84f);   // silver blade
        Color shine = new Color(0.96f, 0.96f, 1.00f);   // bright edge
        Color dark  = new Color(0.48f, 0.50f, 0.54f);   // shadow
        Color grip  = new Color(0.35f, 0.24f, 0.12f);   // grip
        Color guard = new Color(0.62f, 0.62f, 0.66f);   // crossguard
        FillRect(tex, 0, 0, 32, 32, bg);
        // Blade — centered vertical, tapers to tip at top
        FillRect(tex, 14,  3, 4, 20, dark);
        FillRect(tex, 14,  3, 3, 20, blade);
        FillRect(tex, 14,  3, 1, 20, shine);
        // Taper tip
        SetPixelSafe(tex, 15,  2, blade);
        SetPixelSafe(tex, 15,  1, shine);
        // Crossguard — horizontal bar
        FillRect(tex,  7, 21, 18, 3, guard);
        FillRect(tex,  7, 21, 18, 1, shine);
        FillRect(tex,  7, 23, 18, 1, dark);
        // Grip
        FillRect(tex, 14, 24, 4, 5, grip);
        FillRect(tex, 15, 24, 1, 5, new Color(0.52f, 0.36f, 0.18f));
        // Pommel
        FillRect(tex, 13, 29, 6, 2, guard);
        FillRect(tex, 14, 31, 4, 1, shine);
        tex.Apply();
        return tex;
    }

    // Titanium Spear — cool blue-silver background, long thin pole with diamond head
    private static Texture2D DrawTitaniumSpear()
    {
        var tex = NewTex();
        Color bg    = new Color(0.10f, 0.16f, 0.28f);   // deep blue
        Color shaft = new Color(0.58f, 0.64f, 0.72f);   // titanium shaft
        Color head  = new Color(0.82f, 0.86f, 0.92f);   // bright spearhead
        Color shine = new Color(0.95f, 0.97f, 1.00f);   // specular
        Color dark  = new Color(0.36f, 0.40f, 0.48f);   // shadow
        Color wrap  = new Color(0.42f, 0.28f, 0.12f);   // grip wrap
        FillRect(tex, 0, 0, 32, 32, bg);
        // Long shaft — diagonal from bottom-right to top-left
        DrawLine(tex,  7,  4, 25, 28, shaft);
        DrawLine(tex,  8,  4, 26, 28, shaft);
        DrawLine(tex,  9,  4, 27, 28, dark);
        DrawLine(tex,  7,  5, 25, 29, shine);
        // Grip wrap near the bottom
        FillRect(tex, 21, 23, 5, 3, wrap);
        // Spearhead — diamond shape at the top
        FillCircle(tex,  9,  6, 4, head);
        FillRect(tex,   7,  3, 4, 7, head);
        SetPixelSafe(tex,  9,  2, head);
        SetPixelSafe(tex,  9,  1, shine);
        DrawLine(tex,  5,  6, 12,  6, shine);
        DrawLine(tex,  7,  3,  7, 10, dark);
        tex.Apply();
        return tex;
    }

    // Void Warhammer — deep purple/black background, massive square hammerhead with void glow
    private static Texture2D DrawVoidWarhammer()
    {
        var tex = NewTex();
        Color bg    = new Color(0.08f, 0.03f, 0.18f);   // void purple
        Color metal = new Color(0.30f, 0.24f, 0.40f);   // dark void metal
        Color glow  = new Color(0.65f, 0.30f, 1.00f);   // purple glow
        Color core  = new Color(0.90f, 0.70f, 1.00f);   // bright core
        Color shaft = new Color(0.42f, 0.36f, 0.52f);   // handle
        Color shine = new Color(0.55f, 0.45f, 0.70f);   // highlight
        Color dark  = new Color(0.15f, 0.10f, 0.25f);   // deep shadow
        FillRect(tex, 0, 0, 32, 32, bg);
        // Handle
        FillRect(tex, 14, 18, 4, 12, shaft);
        FillRect(tex, 15, 18, 1, 12, shine);
        FillRect(tex, 17, 18, 1, 12, dark);
        // Massive hammerhead
        FillRect(tex,  5,  5, 22, 15, metal);
        FillRect(tex,  5,  5, 22,  2, shine);
        FillRect(tex,  5, 18, 22,  2, dark);
        FillRect(tex,  5,  5,  2, 15, shine);
        FillRect(tex, 25,  5,  2, 15, dark);
        // Void glow runes on the face
        FillCircle(tex, 16, 12, 5, glow);
        FillCircle(tex, 16, 12, 3, core);
        // Corner accents
        FillRect(tex,  5,  5, 4, 4, glow);
        FillRect(tex, 23,  5, 4, 4, glow);
        FillRect(tex,  5, 16, 4, 4, glow);
        FillRect(tex, 23, 16, 4, 4, glow);
        SetPixelSafe(tex,  7,  7, core);
        SetPixelSafe(tex, 24,  7, core);
        SetPixelSafe(tex,  7, 17, core);
        SetPixelSafe(tex, 24, 17, core);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Building piece icons — all warm wood palette
    // ---------------------------------------------------------------

    private static readonly Color WoodBg    = new Color(0.22f, 0.13f, 0.05f);
    private static readonly Color WoodMain  = new Color(0.62f, 0.40f, 0.18f);
    private static readonly Color WoodLight = new Color(0.82f, 0.62f, 0.34f);
    private static readonly Color WoodDark  = new Color(0.38f, 0.22f, 0.08f);
    private static readonly Color WoodEdge  = new Color(0.50f, 0.30f, 0.12f);

    // Foundation — flat platform viewed from slight angle
    private static Texture2D DrawBuildFoundation()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Top face (light)
        FillRect(tex, 4, 18, 24, 8, WoodLight);
        // Left side face (dark)
        FillRect(tex, 4, 10, 6, 8, WoodDark);
        // Right side face (mid)
        FillRect(tex, 10, 14, 18, 4, WoodMain);
        // Front face
        FillRect(tex, 4, 10, 24, 8, WoodMain);
        // Top face on top
        FillRect(tex, 4, 18, 24, 6, WoodLight);
        // Wood grain lines on top
        DrawLine(tex,  6, 20, 26, 20, WoodEdge);
        DrawLine(tex,  6, 22, 26, 22, WoodEdge);
        // Border
        FillRect(tex, 4, 10, 24, 1, WoodLight);
        FillRect(tex, 4, 10, 1, 14, WoodLight);
        tex.Apply();
        return tex;
    }

    // Wall — vertical plank with horizontal lines
    private static Texture2D DrawBuildWall()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Wall face
        FillRect(tex, 6, 2, 20, 28, WoodMain);
        // Horizontal plank dividers
        FillRect(tex, 6,  9, 20, 2, WoodDark);
        FillRect(tex, 6, 17, 20, 2, WoodDark);
        FillRect(tex, 6, 25, 20, 2, WoodDark);
        // Highlight side
        FillRect(tex, 6,  2, 2, 28, WoodLight);
        FillRect(tex, 6,  2, 20, 1, WoodLight);
        // Vertical grain per plank row
        DrawLine(tex, 14,  2, 14, 28, WoodEdge);
        DrawLine(tex, 20,  2, 20, 28, WoodEdge);
        tex.Apply();
        return tex;
    }

    // Floor — horizontal plank top-down
    private static Texture2D DrawBuildFloor()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        FillRect(tex, 3, 3, 26, 26, WoodMain);
        // Plank lines (vertical when viewed from above)
        FillRect(tex, 10, 3, 2, 26, WoodDark);
        FillRect(tex, 20, 3, 2, 26, WoodDark);
        // Grain
        DrawLine(tex, 3,  8, 29,  8, WoodEdge);
        DrawLine(tex, 3, 18, 29, 18, WoodEdge);
        // Highlight edge
        FillRect(tex, 3, 3, 26, 1, WoodLight);
        FillRect(tex, 3, 3, 1, 26, WoodLight);
        tex.Apply();
        return tex;
    }

    // Ramp — diagonal wedge
    private static Texture2D DrawBuildRamp()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Ramp face — trapezoid drawn as filled triangle-ish shape
        for (int y = 4; y < 28; y++)
        {
            int x0 = 4 + (y - 4) * 24 / 24;
            int x1 = 28 - (y - 4) * 8 / 24;
            if (x1 > x0) FillRect(tex, x0, y, x1 - x0, 1, WoodMain);
        }
        // Highlight top edge (the slope surface)
        DrawLine(tex, 4, 4, 28, 28, WoodLight);
        DrawLine(tex, 5, 4, 29, 28, WoodLight);
        // Bottom face
        FillRect(tex, 4, 26, 24, 2, WoodDark);
        // Wood grain diagonals
        DrawLine(tex, 8, 6, 26, 24, WoodEdge);
        DrawLine(tex, 14, 6, 28, 20, WoodEdge);
        tex.Apply();
        return tex;
    }

    // Roof — triangle peak shape
    private static Texture2D DrawBuildRoof()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Left slope
        for (int y = 4; y < 22; y++)
        {
            int x0 = 2 + (y - 4) * 14 / 18;
            FillRect(tex, x0, y, 16 - x0, 1, WoodMain);
        }
        // Right slope (mirrored)
        for (int y = 4; y < 22; y++)
        {
            int x0 = 16;
            int x1 = 30 - (y - 4) * 14 / 18;
            if (x1 > x0) FillRect(tex, x0, y, x1 - x0, 1, WoodMain);
        }
        // Ridge at top
        FillRect(tex, 14, 4, 4, 2, WoodLight);
        // Eave at bottom
        FillRect(tex, 2, 22, 28, 3, WoodDark);
        // Left edge highlight
        DrawLine(tex, 2, 22, 16, 4, WoodLight);
        DrawLine(tex, 30, 22, 16, 4, WoodLight);
        tex.Apply();
        return tex;
    }

    // Door — tall rectangle with handle and frame
    private static Texture2D DrawBuildDoor()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Door frame
        FillRect(tex, 6, 2, 20, 28, WoodDark);
        // Door panel
        FillRect(tex, 8, 3, 16, 26, WoodMain);
        // Panel inset (top half)
        FillRect(tex, 10, 5, 12, 10, WoodDark);
        FillRect(tex, 11, 6, 10,  8, WoodEdge);
        // Panel inset (bottom half)
        FillRect(tex, 10, 17, 12, 9, WoodDark);
        FillRect(tex, 11, 18, 10, 7, WoodEdge);
        // Door handle
        FillRect(tex, 20, 15, 2, 4, WoodLight);
        FillCircle(tex, 21, 14, 2, WoodLight);
        // Highlight left edge
        FillRect(tex, 8, 3, 1, 26, WoodLight);
        FillRect(tex, 8, 3, 16, 1, WoodLight);
        tex.Apply();
        return tex;
    }

    // Window — square with 4-pane cross
    private static Texture2D DrawBuildWindow()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Frame
        FillRect(tex, 4, 4, 24, 24, WoodDark);
        // Glass panes
        Color glass = new Color(0.55f, 0.78f, 0.90f, 0.85f);
        FillRect(tex,  6,  6, 9, 9, glass);   // top-left pane
        FillRect(tex, 17,  6, 9, 9, glass);   // top-right pane
        FillRect(tex,  6, 17, 9, 9, glass);   // bottom-left pane
        FillRect(tex, 17, 17, 9, 9, glass);   // bottom-right pane
        // Cross mullion
        FillRect(tex, 14,  4, 4, 24, WoodMain);
        FillRect(tex,  4, 14, 24, 4, WoodMain);
        // Shine on panes
        Color shine = new Color(1f, 1f, 1f, 0.5f);
        SetPixelSafe(tex,  7, 14, shine);
        SetPixelSafe(tex, 18, 14, shine);
        SetPixelSafe(tex,  7, 25, shine);
        SetPixelSafe(tex, 18, 25, shine);
        // Outer frame highlight
        FillRect(tex, 4, 4, 24, 1, WoodLight);
        FillRect(tex, 4, 4, 1, 24, WoodLight);
        tex.Apply();
        return tex;
    }

    // Ladder — two rails with rungs
    private static Texture2D DrawBuildLadder()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Left rail
        FillRect(tex, 6, 2, 4, 28, WoodMain);
        FillRect(tex, 7, 2, 1, 28, WoodLight);
        // Right rail
        FillRect(tex, 22, 2, 4, 28, WoodMain);
        FillRect(tex, 23, 2, 1, 28, WoodLight);
        // Rungs (5 evenly spaced)
        for (int i = 0; i < 5; i++)
        {
            int y = 4 + i * 5;
            FillRect(tex, 10, y, 12, 3, WoodMain);
            FillRect(tex, 10, y, 12, 1, WoodLight);
        }
        tex.Apply();
        return tex;
    }

    // Triangle Foundation — triangular platform viewed from slight angle
    private static Texture2D DrawBuildTriFoundation()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Triangle top face (drawn as filled triangle)
        for (int y = 10; y < 26; y++)
        {
            int halfW = (y - 10) * 12 / 16;
            FillRect(tex, 16 - halfW, y, halfW * 2, 1, WoodLight);
        }
        // Front edge
        FillRect(tex, 4, 10, 24, 2, WoodMain);
        // Side edges (diagonal lines)
        DrawLine(tex, 4, 10, 16, 26, WoodEdge);
        DrawLine(tex, 28, 10, 16, 26, WoodEdge);
        // Wood grain
        DrawLine(tex, 12, 14, 20, 14, WoodEdge);
        DrawLine(tex, 10, 18, 22, 18, WoodEdge);
        tex.Apply();
        return tex;
    }

    // Triangle Floor — thin triangular slab top-down
    private static Texture2D DrawBuildTriFloor()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Triangle viewed from above
        for (int y = 4; y < 28; y++)
        {
            int halfW = (y - 4) * 13 / 24;
            FillRect(tex, 16 - halfW, y, halfW * 2, 1, WoodMain);
        }
        // Plank divider lines
        DrawLine(tex, 10, 12, 22, 12, WoodDark);
        DrawLine(tex, 8, 20, 24, 20, WoodDark);
        // Edge highlights
        DrawLine(tex, 3, 28, 16, 4, WoodLight);
        DrawLine(tex, 29, 28, 16, 4, WoodLight);
        tex.Apply();
        return tex;
    }

    // Stairs — stepped profile viewed from side
    private static Texture2D DrawBuildStairs()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // 5 steps ascending left to right
        for (int i = 0; i < 5; i++)
        {
            int x = 4 + i * 5;
            int y = 4 + i * 5;
            int w = 5;
            int h = 28 - y;
            // Vertical riser
            FillRect(tex, x, 4, w, h, WoodMain);
            // Step top (lighter)
            FillRect(tex, x, y, w, 2, WoodLight);
        }
        // Side edge highlight
        FillRect(tex, 4, 4, 1, 24, WoodLight);
        // Bottom
        FillRect(tex, 4, 4, 25, 2, WoodDark);
        tex.Apply();
        return tex;
    }

    // Pillar — tall thin column
    private static Texture2D DrawBuildPillar()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Column body
        FillRect(tex, 12, 2, 8, 28, WoodMain);
        // Left highlight
        FillRect(tex, 12, 2, 2, 28, WoodLight);
        // Right shadow
        FillRect(tex, 18, 2, 2, 28, WoodDark);
        // Top cap
        FillRect(tex, 10, 28, 12, 2, WoodEdge);
        FillRect(tex, 10, 28, 12, 1, WoodLight);
        // Bottom cap
        FillRect(tex, 10, 2, 12, 2, WoodEdge);
        FillRect(tex, 10, 2, 12, 1, WoodDark);
        // Vertical grain
        DrawLine(tex, 15, 4, 15, 26, WoodEdge);
        tex.Apply();
        return tex;
    }

    // Half Wall — short wall panel (half height)
    private static Texture2D DrawBuildHalfWall()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, WoodBg);
        // Short wall face (bottom half of the icon space)
        FillRect(tex, 6, 2, 20, 14, WoodMain);
        // Horizontal plank divider
        FillRect(tex, 6, 9, 20, 2, WoodDark);
        // Highlight side
        FillRect(tex, 6, 2, 2, 14, WoodLight);
        FillRect(tex, 6, 2, 20, 1, WoodLight);
        // Vertical grain
        DrawLine(tex, 14, 2, 14, 14, WoodEdge);
        DrawLine(tex, 20, 2, 20, 14, WoodEdge);
        // Ground line to show it's short
        FillRect(tex, 2, 2, 28, 1, WoodDark);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Machine icons (Vol 8.2 & 8.3)
    // ---------------------------------------------------------------

    // Auto-Miner — dark grey box body, drill bit below, cyan status dot
    private static Texture2D DrawAutoMiner()
    {
        var tex = NewTex();
        Color body  = new Color(0.28f, 0.28f, 0.32f);
        Color dark  = new Color(0.16f, 0.16f, 0.18f);
        Color drill = new Color(0.50f, 0.50f, 0.54f);
        Color tip   = new Color(0.75f, 0.75f, 0.78f);
        Color dot   = new Color(0.00f, 0.85f, 0.85f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 8, 8, 16, 14, dark);
        FillRect(tex, 9, 9, 14, 12, body);
        for (int i = 0; i < 3; i++) FillRect(tex, 11, 11 + i * 3, 8, 2, dark);
        FillRect(tex, 14, 22, 4, 8, drill);
        FillRect(tex, 15, 28, 2, 4, tip);
        FillCircle(tex, 22, 10, 2, dot);
        tex.Apply();
        return tex;
    }

    // Drone Port — flat platform with launch arm and orange beacon
    private static Texture2D DrawDronePort()
    {
        var tex = NewTex();
        Color plat  = new Color(0.30f, 0.30f, 0.35f);
        Color arm   = new Color(0.50f, 0.50f, 0.55f);
        Color beacon= new Color(1.00f, 0.60f, 0.00f);
        Color drone = new Color(0.65f, 0.65f, 0.70f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 22, 24, 6, plat);
        FillRect(tex, 14, 8, 4, 14, arm);
        FillCircle(tex, 16, 7, 4, beacon);
        FillRect(tex, 8, 18, 6, 3, drone);
        FillRect(tex, 18, 18, 6, 3, drone);
        tex.Apply();
        return tex;
    }

    // Electric Furnace — metallic box, electric bolt instead of flame, blue glow
    private static Texture2D DrawElectricFurnace()
    {
        var tex = NewTex();
        Color metal = new Color(0.30f, 0.32f, 0.38f);
        Color dark  = new Color(0.16f, 0.17f, 0.20f);
        Color door  = new Color(0.08f, 0.12f, 0.20f);
        Color glow  = new Color(0.10f, 0.55f, 1.00f);
        Color bolt  = new Color(0.70f, 0.95f, 1.00f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 4, 24, 24, dark);
        FillRect(tex, 5, 5, 22, 22, metal);
        FillRect(tex, 10, 8, 12, 14, door);
        FillCircle(tex, 16, 15, 5, glow);
        DrawLine(tex, 19, 10, 14, 16, bolt); DrawLine(tex, 20, 10, 15, 16, bolt);
        DrawLine(tex, 14, 16, 18, 16, bolt);
        DrawLine(tex, 14, 16, 12, 22, bolt); DrawLine(tex, 15, 16, 13, 22, bolt);
        tex.Apply();
        return tex;
    }

    // Grinder — circular grinding wheel on a stand
    private static Texture2D DrawGrinder()
    {
        var tex = NewTex();
        Color stand = new Color(0.35f, 0.32f, 0.28f);
        Color wheel = new Color(0.55f, 0.52f, 0.48f);
        Color rim   = new Color(0.30f, 0.28f, 0.24f);
        Color spark = new Color(1.00f, 0.85f, 0.10f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 12, 22, 8, 8, stand);
        FillRect(tex, 10, 20, 12, 4, stand);
        FillCircle(tex, 16, 14, 10, rim);
        FillCircle(tex, 16, 14,  8, wheel);
        FillCircle(tex, 16, 14,  3, rim);
        for (int i = 0; i < 4; i++) { float a = i * 0.785f; int sx = 16 + Mathf.RoundToInt(Mathf.Cos(a)*5); int sy = 14 + Mathf.RoundToInt(Mathf.Sin(a)*5); SetPixelSafe(tex, sx, sy, spark); }
        tex.Apply();
        return tex;
    }

    // Press — heavy press head above a flat platform, with downward arrows
    private static Texture2D DrawPress()
    {
        var tex = NewTex();
        Color body  = new Color(0.35f, 0.30f, 0.25f);
        Color head  = new Color(0.50f, 0.45f, 0.40f);
        Color plat  = new Color(0.42f, 0.38f, 0.32f);
        Color arrow = new Color(0.80f, 0.75f, 0.60f);
        Color dark  = new Color(0.20f, 0.18f, 0.14f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 6, 2, 20, 10, head);
        FillRect(tex, 6, 2, 20, 2, new Color(0.62f, 0.58f, 0.52f));
        FillRect(tex, 14, 12, 4, 10, body);
        FillRect(tex, 4, 22, 24, 8, dark);
        FillRect(tex, 4, 23, 24, 6, plat);
        DrawLine(tex, 11, 13, 11, 20, arrow); DrawLine(tex, 10, 17, 11, 20, arrow); DrawLine(tex, 12, 17, 11, 20, arrow);
        DrawLine(tex, 21, 13, 21, 20, arrow); DrawLine(tex, 20, 17, 21, 20, arrow); DrawLine(tex, 22, 17, 21, 20, arrow);
        tex.Apply();
        return tex;
    }

    // Assembler — tall box with 3×3 grid of input ports and an output slot
    private static Texture2D DrawAssembler()
    {
        var tex = NewTex();
        Color body = new Color(0.18f, 0.22f, 0.30f);
        Color port = new Color(0.10f, 0.55f, 0.80f);
        Color dark = new Color(0.10f, 0.12f, 0.18f);
        Color out_ = new Color(0.00f, 0.85f, 0.50f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 3, 24, 26, dark);
        FillRect(tex, 5, 4, 22, 24, body);
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                FillRect(tex, 7 + col * 6, 6 + row * 6, 4, 4, port);
        FillRect(tex, 10, 24, 12, 4, out_);
        tex.Apply();
        return tex;
    }

    // Circuit Etcher — flat tray with copper trace lines and a tool arm
    private static Texture2D DrawCircuitEtcher()
    {
        var tex = NewTex();
        Color tray  = new Color(0.12f, 0.18f, 0.12f);
        Color trace = new Color(0.78f, 0.44f, 0.16f);
        Color arm   = new Color(0.55f, 0.55f, 0.58f);
        Color tip   = new Color(0.10f, 0.80f, 0.80f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 16, 24, 14, tray);
        DrawLine(tex, 7, 20, 14, 20, trace); DrawLine(tex, 14, 20, 14, 26, trace);
        DrawLine(tex, 14, 26, 22, 26, trace); DrawLine(tex, 22, 26, 22, 22, trace);
        DrawLine(tex, 22, 22, 18, 22, trace);
        FillRect(tex, 14, 4, 4, 14, arm);
        FillCircle(tex, 16, 4, 3, tip);
        tex.Apply();
        return tex;
    }

    // Component Press — slim press with two blue output bins
    private static Texture2D DrawComponentPress()
    {
        var tex = NewTex();
        Color body = new Color(0.22f, 0.28f, 0.38f);
        Color head = new Color(0.32f, 0.38f, 0.50f);
        Color bin  = new Color(0.10f, 0.45f, 0.70f);
        Color dark = new Color(0.12f, 0.15f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 8, 2, 16, 8, head);
        FillRect(tex, 10, 10, 12, 10, body);
        FillRect(tex, 4, 24, 10, 6, dark); FillRect(tex, 5, 25, 8, 4, bin);
        FillRect(tex, 18, 24, 10, 6, dark); FillRect(tex, 19, 25, 8, 4, bin);
        tex.Apply();
        return tex;
    }

    // Battery Fabricator — blocky factory with two input slots and one battery output
    private static Texture2D DrawBatteryFabricator()
    {
        var tex = NewTex();
        Color body  = new Color(0.14f, 0.20f, 0.14f);
        Color rim   = new Color(0.06f, 0.10f, 0.06f);
        Color inSlot= new Color(0.65f, 0.45f, 0.10f);
        Color out_  = new Color(0.05f, 0.85f, 0.30f);
        Color nub   = new Color(0.20f, 0.55f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 6, 22, 20, rim);
        FillRect(tex, 6, 7, 20, 18, body);
        FillRect(tex, 8, 10, 6, 8, inSlot);
        FillRect(tex, 18, 10, 6, 8, inSlot);
        FillRect(tex, 12, 22, 8, 6, out_);
        FillRect(tex, 14, 21, 4, 2, nub);
        tex.Apply();
        return tex;
    }

    // Schematic Card — card shape with circuit pattern
    private static Texture2D DrawSchematicCard()
    {
        var tex = NewTex();
        Color card  = new Color(0.14f, 0.16f, 0.22f);
        Color edge  = new Color(0.08f, 0.09f, 0.14f);
        Color trace = new Color(0.78f, 0.44f, 0.16f);
        Color dot   = new Color(1.00f, 0.70f, 0.30f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 4, 22, 28, edge);
        FillRect(tex, 6, 5, 20, 26, card);
        DrawLine(tex, 8, 10, 16, 10, trace); DrawLine(tex, 16, 10, 16, 18, trace);
        DrawLine(tex, 16, 18, 24, 18, trace); DrawLine(tex, 8, 18, 8, 26, trace);
        DrawLine(tex, 8, 26, 16, 26, trace);
        FillCircle(tex,  8, 10, 2, dot); FillCircle(tex, 16, 10, 2, dot);
        FillCircle(tex, 24, 18, 2, dot); FillCircle(tex,  8, 26, 2, dot);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Advanced generator icons (Vol 8.4)
    // ---------------------------------------------------------------

    // Solar Panel — blue panel grid with sun rays
    private static Texture2D DrawSolarPanel()
    {
        var tex = NewTex();
        Color panel = new Color(0.10f, 0.18f, 0.55f);
        Color cell  = new Color(0.06f, 0.11f, 0.38f);
        Color frame = new Color(0.60f, 0.62f, 0.65f);
        Color sun   = new Color(1.00f, 0.90f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 3, 10, 26, 18, frame);
        FillRect(tex, 4, 11, 24, 16, panel);
        for (int col = 0; col < 3; col++)
            for (int row = 0; row < 2; row++)
                FillRect(tex, 6 + col * 7, 13 + row * 6, 5, 4, cell);
        FillCircle(tex, 26, 6, 4, sun);
        DrawLine(tex, 26, 0, 26, 2, sun); DrawLine(tex, 30, 2, 28, 4, sun);
        DrawLine(tex, 32, 6, 30, 6, sun); DrawLine(tex, 30, 10, 28, 8, sun);
        tex.Apply();
        return tex;
    }

    // Void Filament Generator — tall dark column with purple void tendrils
    private static Texture2D DrawVoidFilament()
    {
        var tex = NewTex();
        Color body  = new Color(0.08f, 0.05f, 0.14f);
        Color dark  = new Color(0.04f, 0.02f, 0.08f);
        Color void_ = new Color(0.55f, 0.20f, 1.00f);
        Color core  = new Color(0.80f, 0.60f, 1.00f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 10, 4, 12, 24, dark);
        FillRect(tex, 11, 5, 10, 22, body);
        FillCircle(tex, 16, 16, 6, void_);
        FillCircle(tex, 16, 16, 3, core);
        DrawLine(tex, 16, 10,  8,  2, void_); DrawLine(tex, 16, 10, 24,  2, void_);
        DrawLine(tex, 16, 22,  8, 30, void_); DrawLine(tex, 16, 22, 24, 30, void_);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Storage drive icons (Vol 8.5)
    // ---------------------------------------------------------------

    // Drive — chip card shape with coloured label stripe
    private static Texture2D DrawDrive(Color labelColor)
    {
        var tex = NewTex();
        Color body  = new Color(0.14f, 0.14f, 0.16f);
        Color chip  = new Color(0.30f, 0.30f, 0.34f);
        Color gold  = new Color(0.75f, 0.65f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 4, 22, 26, body);
        FillRect(tex, 5, 4, 22, 8, labelColor);
        FillRect(tex, 8, 8, 16, 14, chip);
        for (int i = 0; i < 5; i++) { FillRect(tex, 3, 8 + i * 3, 2, 2, gold); FillRect(tex, 27, 8 + i * 3, 2, 2, gold); }
        tex.Apply();
        return tex;
    }

    // Drive Repair Kit — screwdriver and chip
    private static Texture2D DrawDriveRepairKit()
    {
        var tex = NewTex();
        Color handle= new Color(0.72f, 0.30f, 0.10f);
        Color shaft = new Color(0.60f, 0.60f, 0.64f);
        Color tip   = new Color(0.80f, 0.80f, 0.84f);
        Color chip  = new Color(0.14f, 0.14f, 0.16f);
        Color gold  = new Color(0.75f, 0.65f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 12, 4, 6, 14, handle);
        FillRect(tex, 14, 18, 4, 10, shaft);
        FillRect(tex, 13, 28, 6, 2, tip);
        FillRect(tex, 4, 8, 8, 10, chip);
        for (int i = 0; i < 3; i++) FillRect(tex, 3, 10 + i * 3, 1, 2, gold);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Enemy drop icons
    // ---------------------------------------------------------------

    // Directive Shard — angular blue-white crystal
    private static Texture2D DrawDirectiveShard()
    {
        var tex = NewTex();
        Color bg    = Color.clear;
        Color shard = new Color(0.25f, 0.55f, 0.90f);
        Color light = new Color(0.65f, 0.85f, 1.00f);
        Color dark  = new Color(0.10f, 0.25f, 0.55f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        DrawLine(tex, 16,  2, 22, 14, dark);  DrawLine(tex, 16, 2, 10, 14, dark);
        DrawLine(tex, 10, 14, 16, 30, shard); DrawLine(tex, 22, 14, 16, 30, shard);
        DrawLine(tex, 10, 14, 22, 14, shard);
        FillRect(tex, 14, 10, 4, 16, shard);
        DrawLine(tex, 16, 2, 16, 14, light);
        tex.Apply();
        return tex;
    }

    // Reinforced Plating — dark metal plate with bolts
    private static Texture2D DrawReinforcedPlating()
    {
        var tex = NewTex();
        Color metal = new Color(0.30f, 0.32f, 0.38f);
        Color dark  = new Color(0.16f, 0.17f, 0.20f);
        Color bolt  = new Color(0.60f, 0.60f, 0.65f);
        Color shine = new Color(0.55f, 0.57f, 0.65f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 8, 24, 16, dark);
        FillRect(tex, 5, 9, 22, 14, metal);
        FillRect(tex, 5, 9, 22, 1, shine);
        FillCircle(tex,  8, 12, 2, bolt); FillCircle(tex,  8, 12, 1, dark);
        FillCircle(tex, 24, 12, 2, bolt); FillCircle(tex, 24, 12, 1, dark);
        FillCircle(tex,  8, 20, 2, bolt); FillCircle(tex,  8, 20, 1, dark);
        FillCircle(tex, 24, 20, 2, bolt); FillCircle(tex, 24, 20, 1, dark);
        tex.Apply();
        return tex;
    }

    // Cortex Components — circuit board fragment (teal green)
    private static Texture2D DrawCortexComponents()
    {
        var tex = NewTex();
        Color board = new Color(0.08f, 0.22f, 0.14f);
        Color trace = new Color(0.78f, 0.44f, 0.16f);
        Color chip  = new Color(0.12f, 0.12f, 0.14f);
        Color dot   = new Color(0.00f, 0.90f, 0.55f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 6, 24, 20, board);
        FillRect(tex, 10, 10, 12, 8, chip);
        DrawLine(tex, 4, 14, 10, 14, trace); DrawLine(tex, 22, 14, 28, 14, trace);
        DrawLine(tex, 14, 6, 14, 10, trace); DrawLine(tex, 18, 6, 18, 10, trace);
        DrawLine(tex, 14, 18, 14, 26, trace); DrawLine(tex, 18, 18, 18, 26, trace);
        FillCircle(tex, 4, 14, 2, dot); FillCircle(tex, 28, 14, 2, dot);
        tex.Apply();
        return tex;
    }

    // Energy Cell — glowing capsule, yellow-white core
    private static Texture2D DrawEnergyCell()
    {
        var tex = NewTex();
        Color shell = new Color(0.15f, 0.18f, 0.25f);
        Color glow  = new Color(0.90f, 0.85f, 0.20f);
        Color core  = new Color(1.00f, 1.00f, 0.70f);
        Color band  = new Color(0.40f, 0.40f, 0.48f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillCircle(tex, 16, 6, 5, shell);
        FillRect(tex, 11, 6, 10, 20, shell);
        FillCircle(tex, 16, 26, 5, shell);
        FillCircle(tex, 16, 6, 3, glow);
        FillRect(tex, 13, 6, 6, 20, glow);
        FillCircle(tex, 16, 26, 3, glow);
        FillRect(tex, 14, 10, 4, 12, core);
        FillRect(tex, 11, 12, 10, 2, band);
        FillRect(tex, 11, 18, 10, 2, band);
        tex.Apply();
        return tex;
    }

    // Weapon Components — gear + spring cluster
    private static Texture2D DrawWeaponComponents()
    {
        var tex = NewTex();
        Color metal = new Color(0.52f, 0.50f, 0.48f);
        Color dark  = new Color(0.28f, 0.26f, 0.24f);
        Color spring= new Color(0.70f, 0.68f, 0.62f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillCircle(tex, 12, 14, 8, dark);
        FillCircle(tex, 12, 14, 6, metal);
        FillCircle(tex, 12, 14, 3, dark);
        for (int i = 0; i < 8; i++) { float a = i * 0.785f; int sx = 12 + Mathf.RoundToInt(Mathf.Cos(a)*7); int sy = 14 + Mathf.RoundToInt(Mathf.Sin(a)*7); FillRect(tex, sx-1, sy-1, 2, 2, metal); }
        for (int y = 8; y < 26; y += 3) FillRect(tex, 22, y, 3, 2, spring);
        tex.Apply();
        return tex;
    }

    // Chitin — segmented exoskeleton plate, olive brown
    private static Texture2D DrawChitin()
    {
        var tex = NewTex();
        Color shell = new Color(0.45f, 0.38f, 0.15f);
        Color dark  = new Color(0.25f, 0.20f, 0.08f);
        Color ridge = new Color(0.60f, 0.52f, 0.22f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillCircle(tex, 16, 16, 13, dark);
        FillCircle(tex, 16, 16, 11, shell);
        for (int y = 8; y < 24; y += 4) FillRect(tex, 6, y, 20, 2, ridge);
        FillCircle(tex, 16, 16, 5, dark);
        FillCircle(tex, 16, 16, 3, shell);
        tex.Apply();
        return tex;
    }

    // Fractured Memory Shard — jagged purple crystal
    private static Texture2D DrawFracturedMemoryShard()
    {
        var tex = NewTex();
        Color crystal= new Color(0.55f, 0.20f, 0.85f);
        Color light  = new Color(0.80f, 0.60f, 1.00f);
        Color dark   = new Color(0.25f, 0.08f, 0.40f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        DrawLine(tex, 16, 2, 24, 16, dark);   DrawLine(tex, 16, 2,  8, 20, crystal);
        DrawLine(tex, 24, 16, 20, 30, dark);  DrawLine(tex, 8, 20, 16, 30, crystal);
        DrawLine(tex, 8, 20, 24, 16, crystal);
        FillRect(tex, 14, 10, 4, 16, crystal);
        DrawLine(tex, 16, 2, 16, 16, light);
        DrawLine(tex, 12, 8, 20, 8, dark);
        tex.Apply();
        return tex;
    }

    // Schematic Fragment — torn paper with blueprint lines
    private static Texture2D DrawSchematicFragment()
    {
        var tex = NewTex();
        Color paper = new Color(0.75f, 0.82f, 0.90f);
        Color line  = new Color(0.20f, 0.40f, 0.70f);
        Color edge  = new Color(0.55f, 0.62f, 0.72f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Torn paper shape
        FillRect(tex, 5, 4, 20, 24, edge);
        FillRect(tex, 6, 5, 18, 22, paper);
        // Tear edge (top-right)
        for (int i = 0; i < 5; i++) SetPixelSafe(tex, 22 + i, 5 + i, paper);
        // Blueprint lines
        DrawLine(tex, 8, 10, 22, 10, line);
        DrawLine(tex, 8, 16, 22, 16, line);
        DrawLine(tex, 8, 22, 20, 22, line);
        DrawLine(tex, 12, 10, 12, 22, line);
        DrawLine(tex, 18, 10, 18, 22, line);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Vehicle part icons (per AttachmentType)
    // ---------------------------------------------------------------

    private static Texture2D DrawVehiclePartByType(Voidborne.Vehicles.AttachmentType partType)
    {
        switch (partType)
        {
            case Voidborne.Vehicles.AttachmentType.Wheel:            return DrawPartWheel();
            case Voidborne.Vehicles.AttachmentType.Hood:             return DrawPartHood();
            case Voidborne.Vehicles.AttachmentType.Bumper:           return DrawPartBumper();
            case Voidborne.Vehicles.AttachmentType.CockpitShell:     return DrawPartCockpit();
            case Voidborne.Vehicles.AttachmentType.CockpitDoorLeft:
            case Voidborne.Vehicles.AttachmentType.CockpitDoorRight: return DrawPartDoor();
            case Voidborne.Vehicles.AttachmentType.Engine:           return DrawPartEngine();
            case Voidborne.Vehicles.AttachmentType.TrunkDoor:        return DrawPartTrunkDoor();
            case Voidborne.Vehicles.AttachmentType.Suspension:       return DrawPartSuspension();
            default:                                                  return DrawFallback(ItemType.VehiclePart);
        }
    }

    // Wheel: dark circle (tire) + lighter hub center
    private static Texture2D DrawPartWheel()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color tire = new Color(0.15f, 0.15f, 0.15f);
        Color hub  = new Color(0.55f, 0.55f, 0.58f);
        Color tread = new Color(0.22f, 0.22f, 0.22f);
        FillCircle(tex, 16, 16, 14, tire);
        // Tread ring
        DrawEllipseRing(tex, 16, 16, 12, 12, 1, tread);
        // Hub
        FillCircle(tex, 16, 16, 5, hub);
        // Hub highlight
        FillCircle(tex, 14, 18, 2, new Color(0.70f, 0.70f, 0.72f));
        tex.Apply();
        return tex;
    }

    // Hood: flat trapezoid, metallic gray with panel line
    private static Texture2D DrawPartHood()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color body  = new Color(0.45f, 0.45f, 0.48f);
        Color dark  = new Color(0.30f, 0.30f, 0.32f);
        Color shine = new Color(0.65f, 0.65f, 0.68f);
        // Main panel
        FillRect(tex, 3, 8, 26, 16, body);
        // Panel line down center
        DrawLine(tex, 16, 8, 16, 24, dark);
        // Top bevel
        FillRect(tex, 4, 22, 24, 2, shine);
        // Edge shadow
        FillRect(tex, 3, 8, 26, 1, dark);
        tex.Apply();
        return tex;
    }

    // Bumper: horizontal bar
    private static Texture2D DrawPartBumper()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color bar   = new Color(0.30f, 0.30f, 0.32f);
        Color shine = new Color(0.50f, 0.50f, 0.52f);
        // Main bar
        FillRect(tex, 2, 12, 28, 8, bar);
        // Highlight strip
        FillRect(tex, 3, 17, 26, 2, shine);
        // End caps
        FillRect(tex, 2, 12, 2, 8, shine);
        FillRect(tex, 28, 12, 2, 8, shine);
        tex.Apply();
        return tex;
    }

    // Cockpit: dome/arch shape, tinted glass
    private static Texture2D DrawPartCockpit()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color frame = new Color(0.35f, 0.35f, 0.38f);
        Color glass = new Color(0.40f, 0.60f, 0.70f);
        // Base platform
        FillRect(tex, 4, 4, 24, 4, frame);
        // Side pillars
        FillRect(tex, 4, 4, 3, 20, frame);
        FillRect(tex, 25, 4, 3, 20, frame);
        // Top arch
        FillRect(tex, 4, 22, 24, 3, frame);
        // Glass fill
        FillRect(tex, 7, 8, 18, 14, glass);
        // Glass highlight
        FillRect(tex, 9, 16, 4, 4, new Color(0.55f, 0.75f, 0.85f));
        tex.Apply();
        return tex;
    }

    // Door: rectangle with window cutout
    private static Texture2D DrawPartDoor()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color panel  = new Color(0.40f, 0.40f, 0.43f);
        Color window = new Color(0.40f, 0.60f, 0.70f);
        Color handle = new Color(0.60f, 0.60f, 0.62f);
        // Door panel
        FillRect(tex, 5, 3, 22, 26, panel);
        // Window cutout (upper portion)
        FillRect(tex, 8, 16, 16, 10, window);
        // Handle
        FillRect(tex, 20, 10, 4, 2, handle);
        tex.Apply();
        return tex;
    }

    // Engine: rectangle body + cylinder exhaust pipe
    private static Texture2D DrawPartEngine()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color iron  = new Color(0.35f, 0.35f, 0.38f);
        Color dark  = new Color(0.22f, 0.22f, 0.24f);
        Color pipe  = new Color(0.50f, 0.50f, 0.52f);
        // Engine block
        FillRect(tex, 6, 6, 18, 16, iron);
        // Cylinder head ridges
        FillRect(tex, 6, 18, 18, 2, dark);
        FillRect(tex, 6, 14, 18, 2, dark);
        FillRect(tex, 6, 10, 18, 2, dark);
        // Exhaust pipe
        FillRect(tex, 24, 10, 4, 3, pipe);
        FillRect(tex, 27, 8, 2, 7, pipe);
        // Highlight
        FillRect(tex, 8, 20, 6, 1, new Color(0.50f, 0.50f, 0.52f));
        tex.Apply();
        return tex;
    }

    // Trunk door: small rectangle with handle line
    private static Texture2D DrawPartTrunkDoor()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color panel  = new Color(0.40f, 0.40f, 0.43f);
        Color edge   = new Color(0.28f, 0.28f, 0.30f);
        Color handle = new Color(0.60f, 0.60f, 0.62f);
        // Trunk panel
        FillRect(tex, 4, 8, 24, 16, panel);
        // Edge lines
        FillRect(tex, 4, 8, 24, 1, edge);
        FillRect(tex, 4, 23, 24, 1, edge);
        // Handle bar
        FillRect(tex, 10, 15, 12, 2, handle);
        tex.Apply();
        return tex;
    }

    // Suspension: coil spring with shock absorber
    private static Texture2D DrawPartSuspension()
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        Color spring = new Color(0.50f, 0.50f, 0.55f);
        Color springLight = new Color(0.70f, 0.70f, 0.75f);
        Color shock = new Color(0.35f, 0.35f, 0.38f);
        Color shockShine = new Color(0.55f, 0.55f, 0.58f);
        Color mount = new Color(0.60f, 0.60f, 0.62f);
        // Shock absorber body (center cylinder)
        FillRect(tex, 14, 6, 4, 20, shock);
        FillRect(tex, 14, 6, 1, 20, shockShine);
        // Coil spring (zigzag pattern around the shock)
        for (int i = 0; i < 6; i++)
        {
            int y = 8 + i * 3;
            // Left coil
            DrawLine(tex, 8, y, 14, y + 1, spring);
            DrawLine(tex, 8, y, 14, y + 1, springLight);
            // Right coil
            DrawLine(tex, 18, y + 1, 24, y, spring);
            DrawLine(tex, 18, y + 1, 24, y, springLight);
            // Horizontal coil lines
            FillRect(tex, 8, y, 16, 1, spring);
        }
        // Top mount plate
        FillRect(tex, 10, 26, 12, 3, mount);
        FillRect(tex, 10, 28, 12, 1, springLight);
        // Bottom mount plate
        FillRect(tex, 10, 4, 12, 3, mount);
        FillRect(tex, 10, 4, 12, 1, springLight);
        // Piston rod (thin line through center)
        FillRect(tex, 15, 4, 2, 24, shockShine);
        tex.Apply();
        return tex;
    }

    // Fallback — solid color square with type initial
    private static Texture2D DrawFallback(ItemType type)
    {
        var tex = NewTex();
        Color c = GetTypeColor(type);
        Color dark = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f);
        Color light = new Color(Mathf.Min(c.r * 1.4f, 1f), Mathf.Min(c.g * 1.4f, 1f), Mathf.Min(c.b * 1.4f, 1f));
        FillRect(tex, 0, 0, 32, 32, c);
        // Border
        FillRect(tex, 0, 0, 32, 2, dark);
        FillRect(tex, 0, 30, 32, 2, dark);
        FillRect(tex, 0, 0, 2, 32, dark);
        FillRect(tex, 30, 0, 2, 32, dark);
        // Highlight corner
        FillRect(tex, 2, 2, 6, 2, light);
        FillRect(tex, 2, 2, 2, 6, light);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Drawing primitives
    // ---------------------------------------------------------------

    private static Texture2D NewTex()
    {
        return new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static Sprite Texture2DToSprite(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
    {
        for (int px = x; px < x + w; px++)
            for (int py = y; py < y + h; py++)
                SetPixelSafe(tex, px, py, c);
    }

    private static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color c)
    {
        int r2 = radius * radius;
        for (int px = cx - radius; px <= cx + radius; px++)
            for (int py = cy - radius; py <= cy + radius; py++)
                if ((px - cx) * (px - cx) + (py - cy) * (py - cy) <= r2)
                    SetPixelSafe(tex, px, py, c);
    }

    private static void DrawEllipseRing(Texture2D tex, int cx, int cy, int rx, int ry, int thickness, Color c)
    {
        for (int angle = 0; angle < 360; angle++)
        {
            float rad = angle * Mathf.Deg2Rad;
            for (int t = 0; t <= thickness; t++)
            {
                int px = cx + Mathf.RoundToInt(Mathf.Cos(rad) * (rx - t));
                int py = cy + Mathf.RoundToInt(Mathf.Sin(rad) * (ry - t));
                SetPixelSafe(tex, px, py, c);
            }
        }
    }

    private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int cx = x0, cy = y0;
        while (true)
        {
            SetPixelSafe(tex, cx, cy, c);
            if (cx == x1 && cy == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; cx += sx; }
            if (e2 < dx)  { err += dx; cy += sy; }
        }
    }

    private static void SetPixelSafe(Texture2D tex, int x, int y, Color c)
    {
        if (x >= 0 && x < 32 && y >= 0 && y < 32)
            tex.SetPixel(x, y, c);
    }

    // ---------------------------------------------------------------
    //  Electricity icons
    // ---------------------------------------------------------------

    // Burn Generator — dark metal box with orange flame on top
    private static Texture2D DrawBurnGenerator()
    {
        var tex = NewTex();
        Color metal  = new Color(0.30f, 0.25f, 0.20f);
        Color dark   = new Color(0.18f, 0.14f, 0.10f);
        Color flame1 = new Color(1.00f, 0.55f, 0.05f);
        Color flame2 = new Color(1.00f, 0.85f, 0.10f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Body
        FillRect(tex, 6, 4, 20, 18, dark);
        FillRect(tex, 7, 5, 18, 16, metal);
        // Vent slits
        for (int i = 0; i < 3; i++)
            FillRect(tex, 10, 7 + i * 4, 12, 2, dark);
        // Flame
        FillCircle(tex, 16, 26, 5, flame1);
        FillCircle(tex, 16, 28, 3, flame2);
        FillCircle(tex, 13, 25, 3, flame1);
        FillCircle(tex, 19, 25, 3, flame1);
        tex.Apply();
        return tex;
    }

    // Thermal Tap — red-orange disc with heat lines
    private static Texture2D DrawThermalTap()
    {
        var tex = NewTex();
        Color rock  = new Color(0.35f, 0.18f, 0.10f);
        Color heat1 = new Color(0.90f, 0.25f, 0.00f);
        Color heat2 = new Color(1.00f, 0.60f, 0.10f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillCircle(tex, 16, 14, 12, rock);
        FillCircle(tex, 16, 14,  9, heat1);
        FillCircle(tex, 16, 14,  5, heat2);
        // Heat shimmer lines rising from top
        for (int x = 12; x <= 20; x += 2)
        {
            DrawLine(tex, x, 3, x + 1, 0, heat1);
            DrawLine(tex, x, 5, x - 1, 1, heat2);
        }
        tex.Apply();
        return tex;
    }

    // Wind Rotor — grey pole with three white blades
    private static Texture2D DrawWindRotor()
    {
        var tex = NewTex();
        Color pole  = new Color(0.55f, 0.55f, 0.58f);
        Color blade = new Color(0.90f, 0.90f, 0.92f);
        Color hub   = new Color(0.70f, 0.70f, 0.72f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Pole
        FillRect(tex, 14, 0, 4, 20, pole);
        // Hub
        FillCircle(tex, 16, 18, 3, hub);
        // Blades (three at 120° apart)
        DrawLine(tex, 16, 18,  5, 28, blade); DrawLine(tex, 15, 18,  4, 28, blade);
        DrawLine(tex, 16, 18, 27, 28, blade); DrawLine(tex, 17, 18, 28, 28, blade);
        DrawLine(tex, 16, 18, 16,  5, blade); DrawLine(tex, 15, 18, 15,  5, blade);
        tex.Apply();
        return tex;
    }

    // Battery Bank — green-tinted rectangle with charge bars
    private static Texture2D DrawBatteryBank()
    {
        var tex = NewTex();
        Color body  = new Color(0.12f, 0.18f, 0.14f);
        Color rim   = new Color(0.05f, 0.10f, 0.07f);
        Color bar   = new Color(0.05f, 0.85f, 0.30f);
        Color empty = new Color(0.10f, 0.20f, 0.12f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 6, 24, 20, rim);
        FillRect(tex, 5, 7, 22, 18, body);
        // Terminal nubs
        FillRect(tex, 12, 3, 8, 4, rim);
        FillRect(tex, 13, 4, 6, 3, body);
        // Charge bars (4 bars, 3 filled)
        for (int i = 0; i < 4; i++)
        {
            Color c = i < 3 ? bar : empty;
            FillRect(tex, 7 + i * 5, 10, 3, 12, c);
        }
        tex.Apply();
        return tex;
    }

    // Junction Box — yellow box with bolt symbol
    private static Texture2D DrawJunctionBox()
    {
        var tex = NewTex();
        Color box  = new Color(0.70f, 0.60f, 0.08f);
        Color dark = new Color(0.35f, 0.28f, 0.04f);
        Color bolt = new Color(1.00f, 0.95f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 5, 5, 22, 22, dark);
        FillRect(tex, 6, 6, 20, 20, box);
        // Lightning bolt: two diagonal strokes
        DrawLine(tex, 19, 8,  14, 17, bolt);
        DrawLine(tex, 20, 8,  15, 17, bolt);
        DrawLine(tex, 14, 17, 18, 17, bolt);
        DrawLine(tex, 14, 17,  9, 26, bolt);
        DrawLine(tex, 15, 17, 10, 26, bolt);
        tex.Apply();
        return tex;
    }

    // Wire — yellow coiled line on dark background
    private static Texture2D DrawWire()
    {
        var tex = NewTex();
        Color bg   = new Color(0.12f, 0.12f, 0.12f);
        Color wire = new Color(0.95f, 0.80f, 0.10f);
        Color ins  = new Color(0.20f, 0.10f, 0.05f); // insulation
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillCircle(tex, 16, 16, 13, bg);
        // Coil: concentric arcs
        DrawEllipseRing(tex, 16, 16, 10, 10, 2, ins);
        DrawEllipseRing(tex, 16, 16, 10, 10, 1, wire);
        DrawEllipseRing(tex, 16, 16,  6,  6, 1, wire);
        DrawEllipseRing(tex, 16, 16,  2,  2, 1, wire);
        // Lead ends
        DrawLine(tex, 6,  16,  2, 16, wire);
        DrawLine(tex, 26, 16, 30, 16, wire);
        tex.Apply();
        return tex;
    }

    // Power Cable — thick orange-jacketed cable segment
    private static Texture2D DrawPowerCable()
    {
        var tex = NewTex();
        Color jacket  = new Color(0.80f, 0.35f, 0.05f);
        Color core    = new Color(0.95f, 0.80f, 0.10f);
        Color dark    = new Color(0.30f, 0.12f, 0.02f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Cable body (thick horizontal)
        FillRect(tex, 2, 12, 28, 8, dark);
        FillRect(tex, 2, 13, 28, 6, jacket);
        FillRect(tex, 2, 15, 28, 2, core);
        // End caps
        FillCircle(tex,  3, 16, 4, dark);
        FillCircle(tex,  3, 16, 3, jacket);
        FillCircle(tex, 29, 16, 4, dark);
        FillCircle(tex, 29, 16, 3, jacket);
        tex.Apply();
        return tex;
    }

    // Powered Light — glowing bulb shape
    private static Texture2D DrawPoweredLight()
    {
        var tex = NewTex();
        Color glow   = new Color(1.00f, 0.95f, 0.60f);
        Color bright = new Color(1.00f, 1.00f, 0.80f);
        Color metal  = new Color(0.50f, 0.50f, 0.52f);
        Color rim    = new Color(0.30f, 0.30f, 0.32f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Glow aura
        FillCircle(tex, 16, 18, 13, new Color(1f, 0.9f, 0.4f, 0.3f));
        // Bulb
        FillCircle(tex, 16, 18, 10, glow);
        FillCircle(tex, 16, 18,  6, bright);
        // Base/cap
        FillRect(tex, 12,  6, 8, 6, rim);
        FillRect(tex, 13,  7, 6, 4, metal);
        // Filament hint
        DrawLine(tex, 14, 14, 18, 14, rim);
        DrawLine(tex, 16, 12, 16, 16, rim);
        tex.Apply();
        return tex;
    }

    // Powered Door — door frame with blue indicator light
    private static Texture2D DrawPoweredDoor()
    {
        var tex = NewTex();
        Color frame  = new Color(0.25f, 0.25f, 0.28f);
        Color door   = new Color(0.12f, 0.30f, 0.50f);
        Color light  = new Color(0.10f, 0.70f, 1.00f);
        Color dark   = new Color(0.10f, 0.10f, 0.12f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Outer frame
        FillRect(tex, 5, 2, 22, 28, dark);
        FillRect(tex, 6, 3, 20, 26, frame);
        // Door panel
        FillRect(tex, 8, 3, 16, 26, door);
        // Door handle
        FillCircle(tex, 20, 16, 2, frame);
        // Status light top-right
        FillCircle(tex, 23,  6, 3, light);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Backpack icons
    // ---------------------------------------------------------------

    // Satchel — simple brown bag with a strap, small buckle
    private static Texture2D DrawSatchel()
    {
        var tex  = NewTex();
        Color bg     = new Color(0.08f, 0.05f, 0.03f);
        Color body   = new Color(0.55f, 0.32f, 0.10f);   // rough hide brown
        Color shadow = new Color(0.32f, 0.18f, 0.05f);
        Color flap   = new Color(0.62f, 0.38f, 0.14f);
        Color strap  = new Color(0.40f, 0.22f, 0.06f);
        Color buckle = new Color(0.75f, 0.65f, 0.30f);   // dull brass
        FillRect(tex, 0, 0, 32, 32, bg);
        // Strap across top
        FillRect(tex,  5,  3, 22, 3, strap);
        FillRect(tex,  5,  4, 22, 1, new Color(0.50f, 0.30f, 0.10f));
        // Bag body
        FillRect(tex,  5,  6, 22, 20, body);
        FillRect(tex,  5,  6, 22,  1, flap);
        FillRect(tex,  5, 25, 22,  1, shadow);
        FillRect(tex,  5,  6,  1, 20, shadow);
        FillRect(tex, 26,  6,  1, 20, shadow);
        // Flap overhang
        FillRect(tex,  5,  6, 22,  7, flap);
        FillRect(tex,  5, 12, 22,  1, shadow);
        // Centre buckle
        FillRect(tex, 14, 10,  4,  3, buckle);
        FillRect(tex, 15, 11,  2,  1, bg);
        tex.Apply();
        return tex;
    }

    // Travel Pack — larger bag, 3 panels, iron buckles
    private static Texture2D DrawTravelPack()
    {
        var tex  = NewTex();
        Color bg      = new Color(0.05f, 0.06f, 0.10f);
        Color fabric  = new Color(0.30f, 0.38f, 0.48f);  // reinforced blue-grey
        Color dark    = new Color(0.18f, 0.22f, 0.30f);
        Color seam    = new Color(0.22f, 0.28f, 0.38f);
        Color buckle  = new Color(0.62f, 0.62f, 0.66f);  // iron
        Color shine   = new Color(0.78f, 0.78f, 0.82f);
        Color pocket  = new Color(0.26f, 0.32f, 0.42f);
        FillRect(tex, 0, 0, 32, 32, bg);
        // Main body
        FillRect(tex,  4,  4, 24, 24, fabric);
        FillRect(tex,  4,  4, 24,  1, shine);
        FillRect(tex,  4, 27, 24,  1, dark);
        FillRect(tex,  4,  4,  1, 24, dark);
        FillRect(tex, 27,  4,  1, 24, dark);
        // Horizontal seam dividing top panel
        FillRect(tex,  4, 14, 24,  1, seam);
        // Front pocket (lower panel)
        FillRect(tex,  7, 16, 18, 10, pocket);
        FillRect(tex,  7, 16, 18,  1, shine);
        FillRect(tex,  7, 25, 18,  1, dark);
        // Two iron buckles on pocket
        FillRect(tex, 10, 20,  4,  3, buckle);
        FillRect(tex, 11, 21,  2,  1, bg);
        FillRect(tex, 18, 20,  4,  3, buckle);
        FillRect(tex, 19, 21,  2,  1, bg);
        // Top strap
        FillRect(tex,  4,  2, 24,  2, dark);
        FillRect(tex,  4,  2, 24,  1, buckle);
        tex.Apply();
        return tex;
    }

    // Expedition Rig — structured frame pack with titanium accents
    private static Texture2D DrawExpeditionRig()
    {
        var tex  = NewTex();
        Color bg      = new Color(0.06f, 0.06f, 0.06f);
        Color leather = new Color(0.42f, 0.28f, 0.14f);  // treated leather
        Color frame   = new Color(0.68f, 0.72f, 0.78f);  // titanium frame
        Color dark    = new Color(0.22f, 0.14f, 0.06f);
        Color light   = new Color(0.58f, 0.40f, 0.22f);
        Color metal   = new Color(0.80f, 0.84f, 0.88f);
        FillRect(tex, 0, 0, 32, 32, bg);
        // Frame bars
        FillRect(tex,  3,  2,  3, 28, frame);   // left bar
        FillRect(tex, 26,  2,  3, 28, frame);   // right bar
        FillRect(tex,  3,  2, 26,  3, frame);   // top bar
        FillRect(tex,  3, 27, 26,  3, frame);   // bottom bar
        // Leather fill between frame
        FillRect(tex,  6,  5, 20, 22, leather);
        FillRect(tex,  6,  5, 20,  1, light);
        FillRect(tex,  6, 26, 20,  1, dark);
        // Horizontal panel dividers
        FillRect(tex,  6, 13, 20,  1, dark);
        FillRect(tex,  6, 19, 20,  1, dark);
        // Buckle on frame intersection (corners)
        FillCircle(tex,  4,  3, 2, metal);
        FillCircle(tex, 27,  3, 2, metal);
        FillCircle(tex,  4, 28, 2, metal);
        FillCircle(tex, 27, 28, 2, metal);
        tex.Apply();
        return tex;
    }

    // Void Pocket — dark purple/black, void crystal glow, dimensional shimmer
    private static Texture2D DrawVoidPocket()
    {
        var tex  = NewTex();
        Color bg     = new Color(0.04f, 0.02f, 0.08f);
        Color body   = new Color(0.14f, 0.08f, 0.24f);   // void fabric
        Color glow   = new Color(0.55f, 0.20f, 1.00f);   // void purple
        Color bright = new Color(0.80f, 0.60f, 1.00f);   // crystal highlight
        Color edge   = new Color(0.30f, 0.12f, 0.55f);
        Color dim    = new Color(0.08f, 0.04f, 0.16f);
        FillRect(tex, 0, 0, 32, 32, bg);
        // Bag body
        FillRect(tex,  4,  4, 24, 24, body);
        FillRect(tex,  4,  4, 24,  1, edge);
        FillRect(tex,  4, 27, 24,  1, dim);
        FillRect(tex,  4,  4,  1, 24, dim);
        FillRect(tex, 27,  4,  1, 24, edge);
        // Dimensional rifts (shimmer lines)
        for (int i = 0; i < 4; i++)
        {
            int x = 6 + i * 5;
            DrawLine(tex, x, 6, x + 2, 26, new Color(glow.r, glow.g, glow.b, 0.6f));
        }
        // Centre void crystal
        FillCircle(tex, 16, 16, 6, glow);
        FillCircle(tex, 16, 16, 3, bright);
        SetPixelSafe(tex, 16, 16, Color.white);
        // Glow halo
        DrawEllipseRing(tex, 16, 16, 9, 9, 1, new Color(glow.r, glow.g, glow.b, 0.4f));
        tex.Apply();
        return tex;
    }

    // Power Probe — handheld scanner with display
    private static Texture2D DrawPowerProbe()
    {
        var tex = NewTex();
        Color body    = new Color(0.20f, 0.22f, 0.28f);
        Color screen  = new Color(0.05f, 0.55f, 0.80f);
        Color scan    = new Color(0.00f, 1.00f, 0.80f);
        Color grip    = new Color(0.12f, 0.12f, 0.14f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Handle/grip
        FillRect(tex, 13, 0, 6, 8, grip);
        // Body
        FillRect(tex, 7, 7, 18, 16, body);
        // Screen
        FillRect(tex, 9, 9, 14, 10, screen);
        // Scan lines on screen
        for (int y = 10; y <= 16; y += 2)
            DrawLine(tex, 10, y, 22, y, new Color(0.5f, 0.9f, 1f, 0.5f));
        // Antenna/tip with glow
        DrawLine(tex, 16, 23, 16, 31, scan);
        DrawLine(tex, 15, 23, 17, 23, scan);
        FillCircle(tex, 16, 31, 2, scan);
        tex.Apply();
        return tex;
    }

    // ── Automation icons (Vol 8.1) ─────────────────────────────────────────

    // Conveyor Belt — side view: brown belt platform with steel rollers and a direction arrow
    private static Texture2D DrawConveyorBelt()
    {
        var tex = NewTex();
        Color bg      = Color.clear;
        Color belt    = new Color(0.35f, 0.22f, 0.08f);   // dark rubber
        Color surface = new Color(0.48f, 0.32f, 0.14f);   // lighter belt surface
        Color seam    = new Color(0.25f, 0.15f, 0.05f);   // seam/strip
        Color roller  = new Color(0.58f, 0.58f, 0.62f);   // steel roller
        Color shaft   = new Color(0.35f, 0.35f, 0.38f);   // roller shaft
        Color arrow   = new Color(0.85f, 0.80f, 0.55f);   // direction arrow
        Color rail    = new Color(0.50f, 0.50f, 0.54f);   // side rail
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Belt body
        FillRect(tex,  2, 11, 28, 10, belt);
        FillRect(tex,  2, 12, 28,  8, surface);
        // Seam strips across belt
        for (int x = 4; x < 30; x += 5)
            FillRect(tex, x, 11, 2, 10, seam);
        // Side rails (top/bottom edges)
        FillRect(tex,  2, 11, 28, 1, rail);
        FillRect(tex,  2, 20, 28, 1, rail);
        // Left roller
        FillCircle(tex,  6, 16, 5, roller);
        FillCircle(tex,  6, 16, 2, shaft);
        // Right roller
        FillCircle(tex, 26, 16, 5, roller);
        FillCircle(tex, 26, 16, 2, shaft);
        // Direction arrow (pointing right)
        DrawLine(tex, 10, 16, 21, 16, arrow);
        DrawLine(tex, 17, 13, 21, 16, arrow);
        DrawLine(tex, 17, 19, 21, 16, arrow);
        tex.Apply();
        return tex;
    }

    // Fast Conveyor Belt — same as belt but with blue accent and double arrows
    private static Texture2D DrawFastConveyorBelt()
    {
        var tex    = NewTex();
        Color belt = new Color(0.12f, 0.22f, 0.42f);   // dark blue-grey rubber
        Color surf = new Color(0.18f, 0.35f, 0.65f);   // blue belt
        Color seam = new Color(0.08f, 0.14f, 0.28f);
        Color roller = new Color(0.58f, 0.58f, 0.62f);
        Color shaft  = new Color(0.35f, 0.35f, 0.38f);
        Color arrow  = new Color(0.30f, 0.90f, 1.00f); // cyan arrows = fast
        Color rail   = new Color(0.25f, 0.45f, 0.75f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex,  2, 11, 28, 10, belt);
        FillRect(tex,  2, 12, 28,  8, surf);
        for (int x = 4; x < 30; x += 4)
            FillRect(tex, x, 11, 1, 10, seam);  // narrower seams = faster visual
        FillRect(tex,  2, 11, 28, 1, rail);
        FillRect(tex,  2, 20, 28, 1, rail);
        FillCircle(tex,  6, 16, 5, roller); FillCircle(tex,  6, 16, 2, shaft);
        FillCircle(tex, 26, 16, 5, roller); FillCircle(tex, 26, 16, 2, shaft);
        // Double arrows (speed visual)
        DrawLine(tex, 9, 16, 16, 16, arrow); DrawLine(tex, 13, 13, 16, 16, arrow); DrawLine(tex, 13, 19, 16, 16, arrow);
        DrawLine(tex, 16, 16, 23, 16, arrow); DrawLine(tex, 19, 13, 23, 16, arrow); DrawLine(tex, 19, 19, 23, 16, arrow);
        tex.Apply();
        return tex;
    }

    // Slope Belt — angled belt rising left-to-right
    private static Texture2D DrawSlopeBelt()
    {
        var tex = NewTex();
        Color belt   = new Color(0.40f, 0.28f, 0.10f);
        Color surf   = new Color(0.55f, 0.38f, 0.16f);
        Color roller = new Color(0.58f, 0.58f, 0.62f);
        Color shaft  = new Color(0.35f, 0.35f, 0.38f);
        Color arrow  = new Color(0.85f, 0.80f, 0.55f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Angled belt via diagonal line blocks
        for (int i = 0; i < 28; i++)
        {
            int bx = 2 + i;
            int by = 6 + (i * 14 / 28);  // rises from y=6 to y=20
            FillRect(tex, bx, by, 1, 4, belt);
            FillRect(tex, bx, by + 1, 1, 2, surf);
        }
        // Rollers at diagonal ends
        FillCircle(tex,  5, 7, 4, roller);  FillCircle(tex,  5, 7, 2, shaft);
        FillCircle(tex, 27, 20, 4, roller); FillCircle(tex, 27, 20, 2, shaft);
        // Diagonal arrow
        DrawLine(tex, 8, 9, 24, 19, arrow);
        DrawLine(tex, 20, 15, 24, 19, arrow);
        DrawLine(tex, 22, 22, 24, 19, arrow);
        tex.Apply();
        return tex;
    }

    // Split Belt — single input bottom, two outputs top (Y-shape)
    private static Texture2D DrawSplitBelt()
    {
        var tex = NewTex();
        Color belt  = new Color(0.20f, 0.42f, 0.20f);  // green = split/routing
        Color surf  = new Color(0.28f, 0.58f, 0.28f);
        Color roller= new Color(0.58f, 0.58f, 0.62f);
        Color shaft = new Color(0.35f, 0.35f, 0.38f);
        Color arrow = new Color(0.70f, 1.00f, 0.70f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Stem (centre input)
        FillRect(tex, 13,  2, 6, 16, belt);
        FillRect(tex, 14,  3, 4, 14, surf);
        // Left fork
        FillRect(tex,  3, 16, 13, 6, belt);
        FillRect(tex,  4, 17, 11, 4, surf);
        // Right fork
        FillRect(tex, 16, 16, 13, 6, belt);
        FillRect(tex, 17, 17, 11, 4, surf);
        // Rollers
        FillCircle(tex, 16,  3, 4, roller); FillCircle(tex, 16,  3, 2, shaft);  // input
        FillCircle(tex,  4, 19, 3, roller); FillCircle(tex,  4, 19, 1, shaft);  // left output
        FillCircle(tex, 28, 19, 3, roller); FillCircle(tex, 28, 19, 1, shaft);  // right output
        // Fork arrows
        DrawLine(tex, 16, 6, 16, 14, arrow);
        DrawLine(tex, 16, 14, 6, 20, arrow);  DrawLine(tex, 8, 17, 6, 20, arrow);
        DrawLine(tex, 16, 14, 26, 20, arrow); DrawLine(tex, 24, 17, 26, 20, arrow);
        tex.Apply();
        return tex;
    }

    // Pneumatic Tube — circular tube cross-section with cyan inner glow
    private static Texture2D DrawPneumaticTube()
    {
        var tex = NewTex();
        Color outer = new Color(0.14f, 0.16f, 0.22f);  // dark shell
        Color metal = new Color(0.30f, 0.32f, 0.38f);  // tube body
        Color inner = new Color(0.05f, 0.55f, 0.80f);  // inner glow (dark)
        Color glow  = new Color(0.20f, 0.80f, 1.00f);  // bright core
        Color pulse = new Color(0.60f, 0.95f, 1.00f);  // item pulse
        Color band  = new Color(0.22f, 0.60f, 0.85f);  // connector rings
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Outer tube
        FillCircle(tex, 16, 16, 14, outer);
        // Metal shell ring
        FillCircle(tex, 16, 16, 12, metal);
        // Inner tube (hollow)
        FillCircle(tex, 16, 16,  9, outer);
        // Interior glow
        FillCircle(tex, 16, 16,  7, inner);
        FillCircle(tex, 16, 16,  4, glow);
        FillCircle(tex, 16, 16,  2, pulse);
        // Connector bands (rings at ~radius 12)
        DrawEllipseRing(tex, 16, 16, 12, 12, 1, band);
        DrawEllipseRing(tex, 16, 16, 10, 10, 1, band);
        // Centre sparkle
        SetPixelSafe(tex, 16, 16, Color.white);
        SetPixelSafe(tex, 15, 16, Color.white);
        SetPixelSafe(tex, 17, 16, Color.white);
        SetPixelSafe(tex, 16, 15, Color.white);
        SetPixelSafe(tex, 16, 17, Color.white);
        tex.Apply();
        return tex;
    }

    // Hopper — funnel/triangle pointing down
    private static Texture2D DrawHopper()
    {
        var tex = NewTex();
        Color body  = new Color(0.50f, 0.38f, 0.20f);  // tan/copper
        Color shade = new Color(0.30f, 0.22f, 0.10f);
        Color shine = new Color(0.70f, 0.56f, 0.32f);
        Color spout = new Color(0.42f, 0.32f, 0.16f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Funnel body (wide top, narrows down)
        // Top lip
        FillRect(tex, 3, 22, 26, 4, body);
        FillRect(tex, 3, 25, 26, 1, shine);
        // Slanted sides — simulate via narrowing strips
        for (int row = 0; row < 12; row++)
        {
            int margin = (row * 7) / 12;
            int w = 26 - margin * 2;
            if (w <= 0) break;
            Color c = (row % 2 == 0) ? body : shade;
            FillRect(tex, 3 + margin, 21 - row, w, 1, c);
        }
        // Spout at bottom
        FillRect(tex, 12, 8, 8, 5, spout);
        FillRect(tex, 13, 8, 6, 4, shade);
        // Inner dark (funnel hollow at top)
        FillRect(tex, 5, 23, 22, 2, shade);
        // Shine on top rim
        FillRect(tex, 3, 25, 26, 1, shine);
        tex.Apply();
        return tex;
    }

    // Filter Hopper — same funnel with yellow filter grid
    private static Texture2D DrawFilterHopper()
    {
        var tex = NewTex();
        Color body   = new Color(0.50f, 0.38f, 0.20f);
        Color shade  = new Color(0.30f, 0.22f, 0.10f);
        Color shine  = new Color(0.70f, 0.56f, 0.32f);
        Color spout  = new Color(0.42f, 0.32f, 0.16f);
        Color filter = new Color(0.90f, 0.75f, 0.10f);  // yellow filter
        Color fgrid  = new Color(0.70f, 0.55f, 0.05f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Same funnel shape as plain hopper
        FillRect(tex, 3, 22, 26, 4, body);
        FillRect(tex, 3, 25, 26, 1, shine);
        for (int row = 0; row < 12; row++)
        {
            int margin = (row * 7) / 12;
            int w = 26 - margin * 2;
            if (w <= 0) break;
            Color c = (row % 2 == 0) ? body : shade;
            FillRect(tex, 3 + margin, 21 - row, w, 1, c);
        }
        FillRect(tex, 12, 8, 8, 5, spout);
        FillRect(tex, 13, 8, 6, 4, shade);
        FillRect(tex, 5, 23, 22, 2, shade);
        FillRect(tex, 3, 25, 26, 1, shine);
        // Yellow filter grid overlay in funnel opening
        FillRect(tex, 5, 22, 22, 3, filter);
        for (int x = 6; x < 27; x += 4)
            FillRect(tex, x, 22, 2, 3, fgrid);
        FillRect(tex, 5, 23, 22, 1, fgrid);
        tex.Apply();
        return tex;
    }

    // Belt Sorter — T/Y junction with coloured output arrows
    private static Texture2D DrawBeltSorter()
    {
        var tex = NewTex();
        Color plate  = new Color(0.22f, 0.22f, 0.26f);  // dark steel plate
        Color edge   = new Color(0.38f, 0.38f, 0.42f);
        Color arm    = new Color(0.85f, 0.80f, 0.10f);  // yellow diverter arm
        Color arrL   = new Color(0.30f, 0.75f, 1.00f);  // left output arrow (blue)
        Color arrR   = new Color(1.00f, 0.40f, 0.20f);  // right output arrow (orange)
        Color arrIn  = new Color(0.75f, 0.75f, 0.75f);  // input arrow (grey)
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Central plate
        FillRect(tex,  8,  8, 16, 16, plate);
        FillRect(tex,  8,  8, 16,  1, edge);
        FillRect(tex,  8, 23, 16,  1, edge);
        FillRect(tex,  8,  8,  1, 16, edge);
        FillRect(tex, 23,  8,  1, 16, edge);
        // Diverter arm (angled bar)
        DrawLine(tex, 10, 10, 22, 22, arm);
        DrawLine(tex, 11, 10, 23, 22, arm);
        // Input arrow (coming from bottom)
        DrawLine(tex, 16,  2, 16,  9, arrIn);
        DrawLine(tex, 13,  5, 16,  2, arrIn);
        DrawLine(tex, 19,  5, 16,  2, arrIn);
        // Left output arrow
        DrawLine(tex,  9, 16,  2, 16, arrL);
        DrawLine(tex,  5, 13,  2, 16, arrL);
        DrawLine(tex,  5, 19,  2, 16, arrL);
        // Right output arrow
        DrawLine(tex, 23, 16, 30, 16, arrR);
        DrawLine(tex, 27, 13, 30, 16, arrR);
        DrawLine(tex, 27, 19, 30, 16, arrR);
        tex.Apply();
        return tex;
    }

    // Overflow Valve — circle with valve wheel (crosshair + rim) and green/red indicator
    private static Texture2D DrawOverflowValve()
    {
        var tex = NewTex();
        Color body   = new Color(0.28f, 0.32f, 0.28f);  // dark industrial
        Color ring   = new Color(0.45f, 0.50f, 0.45f);  // valve ring
        Color spoke  = new Color(0.55f, 0.60f, 0.55f);  // spokes
        Color hub    = new Color(0.68f, 0.72f, 0.68f);  // centre hub
        Color open   = new Color(0.20f, 0.90f, 0.30f);  // green = open indicator
        Color arrow  = new Color(0.80f, 0.85f, 0.80f);  // flow arrows
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Valve body (circular housing)
        FillCircle(tex, 16, 14, 13, body);
        // Valve rim ring
        DrawEllipseRing(tex, 16, 14, 12, 12, 2, ring);
        // Valve spokes (hand wheel)
        DrawLine(tex, 16, 14, 16, 4, spoke);   // top spoke
        DrawLine(tex, 16, 14, 16, 24, spoke);  // bottom spoke
        DrawLine(tex, 16, 14, 6, 14, spoke);   // left spoke
        DrawLine(tex, 16, 14, 26, 14, spoke);  // right spoke
        DrawLine(tex, 16, 14, 9, 7, spoke);    // diagonal TL
        DrawLine(tex, 16, 14, 23, 21, spoke);  // diagonal BR
        DrawLine(tex, 16, 14, 23, 7, spoke);   // diagonal TR
        DrawLine(tex, 16, 14, 9, 21, spoke);   // diagonal BL
        // Centre hub
        FillCircle(tex, 16, 14, 3, hub);
        // Status indicator dot (green = open)
        FillCircle(tex, 16, 29, 3, open);
        // Small flow arrows below valve
        DrawLine(tex, 10, 27, 14, 27, arrow); DrawLine(tex, 12, 25, 14, 27, arrow); DrawLine(tex, 12, 29, 14, 27, arrow);
        DrawLine(tex, 18, 27, 22, 27, arrow); DrawLine(tex, 20, 25, 22, 27, arrow); DrawLine(tex, 20, 29, 22, 27, arrow);
        tex.Apply();
        return tex;
    }

    // ── Vehicle Workbench ────────────────────────────────────────────
    private static Texture2D DrawVehicleWorkbench()
    {
        var tex = NewTex();
        Color table = new Color(0.45f, 0.30f, 0.15f);
        Color top   = new Color(0.55f, 0.38f, 0.20f);
        Color tool  = new Color(0.60f, 0.62f, 0.65f);
        Color wrench = new Color(0.70f, 0.72f, 0.75f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Table body
        FillRect(tex, 2, 8, 28, 14, table);
        // Table top surface
        FillRect(tex, 1, 20, 30, 4, top);
        // Legs
        FillRect(tex, 4, 4, 3, 5, table);
        FillRect(tex, 25, 4, 3, 5, table);
        // Wrench on top
        DrawLine(tex, 8, 25, 14, 28, wrench);
        FillCircle(tex, 7, 25, 2, tool);
        // Gear on top
        FillCircle(tex, 22, 26, 3, tool);
        FillCircle(tex, 22, 26, 1, table);
        tex.Apply();
        return tex;
    }

    // ── Pushcart ─────────────────────────────────────────────────────
    private static Texture2D DrawPushcart()
    {
        var tex = NewTex();
        Color frame = new Color(0.50f, 0.35f, 0.18f);
        Color bed   = new Color(0.60f, 0.42f, 0.22f);
        Color wheel = new Color(0.35f, 0.35f, 0.35f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Cargo bed
        FillRect(tex, 4, 12, 24, 8, bed);
        // Side rails
        FillRect(tex, 3, 10, 2, 12, frame);
        FillRect(tex, 27, 10, 2, 12, frame);
        // Handle
        DrawLine(tex, 15, 22, 15, 28, frame);
        DrawLine(tex, 16, 22, 16, 28, frame);
        DrawLine(tex, 12, 28, 20, 28, frame);
        // Wheels
        FillCircle(tex, 8, 8, 3, wheel);
        FillCircle(tex, 24, 8, 3, wheel);
        tex.Apply();
        return tex;
    }

    // ── Buggy ────────────────────────────────────────────────────────
    private static Texture2D DrawBuggy()
    {
        var tex = NewTex();
        Color body  = new Color(0.30f, 0.55f, 0.30f);
        Color roof  = new Color(0.25f, 0.45f, 0.25f);
        Color wheel = new Color(0.25f, 0.25f, 0.25f);
        Color hub   = new Color(0.50f, 0.50f, 0.50f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Body
        FillRect(tex, 4, 10, 24, 8, body);
        // Roof
        FillRect(tex, 8, 18, 16, 5, roof);
        // Windshield
        FillRect(tex, 20, 18, 4, 5, new Color(0.5f, 0.7f, 0.9f, 0.6f));
        // Wheels
        FillCircle(tex, 8, 7, 4, wheel); FillCircle(tex, 8, 7, 1, hub);
        FillCircle(tex, 24, 7, 4, wheel); FillCircle(tex, 24, 7, 1, hub);
        tex.Apply();
        return tex;
    }

    // ── Cycle ────────────────────────────────────────────────────────
    private static Texture2D DrawCycle()
    {
        var tex = NewTex();
        Color frame = new Color(0.60f, 0.20f, 0.20f);
        Color wheel = new Color(0.25f, 0.25f, 0.25f);
        Color hub   = new Color(0.50f, 0.50f, 0.50f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Frame triangle
        DrawLine(tex, 8, 8, 16, 22, frame);
        DrawLine(tex, 16, 22, 24, 8, frame);
        DrawLine(tex, 8, 8, 24, 8, frame);
        // Seat
        FillRect(tex, 13, 22, 6, 2, frame);
        // Handlebar
        DrawLine(tex, 22, 16, 26, 20, hub);
        // Wheels
        FillCircle(tex, 8, 8, 4, wheel); FillCircle(tex, 8, 8, 1, hub);
        FillCircle(tex, 24, 8, 4, wheel); FillCircle(tex, 24, 8, 1, hub);
        tex.Apply();
        return tex;
    }

    // ── Hauler ───────────────────────────────────────────────────────
    private static Texture2D DrawHauler()
    {
        var tex = NewTex();
        Color body  = new Color(0.40f, 0.42f, 0.45f);
        Color cab   = new Color(0.35f, 0.37f, 0.40f);
        Color wheel = new Color(0.22f, 0.22f, 0.22f);
        Color hub   = new Color(0.45f, 0.45f, 0.45f);
        Color glass = new Color(0.4f, 0.6f, 0.8f, 0.6f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Cargo body
        FillRect(tex, 2, 10, 20, 12, body);
        // Cab
        FillRect(tex, 22, 10, 8, 14, cab);
        // Windshield
        FillRect(tex, 26, 18, 4, 6, glass);
        // 6 wheels (3 axles)
        FillCircle(tex, 6, 7, 3, wheel); FillCircle(tex, 6, 7, 1, hub);
        FillCircle(tex, 16, 7, 3, wheel); FillCircle(tex, 16, 7, 1, hub);
        FillCircle(tex, 26, 7, 3, wheel); FillCircle(tex, 26, 7, 1, hub);
        tex.Apply();
        return tex;
    }

    // ── DrillRig ─────────────────────────────────────────────────────
    private static Texture2D DrawDrillRig()
    {
        var tex = NewTex();
        Color body  = new Color(0.55f, 0.45f, 0.15f);
        Color drill = new Color(0.65f, 0.65f, 0.70f);
        Color tip   = new Color(0.80f, 0.80f, 0.85f);
        Color wheel = new Color(0.25f, 0.25f, 0.25f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Body
        FillRect(tex, 4, 10, 18, 8, body);
        // Drill arm
        FillRect(tex, 22, 12, 8, 4, drill);
        // Drill tip
        DrawLine(tex, 30, 14, 32, 14, tip);
        DrawLine(tex, 30, 13, 32, 14, tip);
        DrawLine(tex, 30, 15, 32, 14, tip);
        // Wheels
        FillCircle(tex, 8, 7, 3, wheel);
        FillCircle(tex, 20, 7, 3, wheel);
        tex.Apply();
        return tex;
    }

    // ── Gyrocopter ───────────────────────────────────────────────────
    private static Texture2D DrawGyrocopter()
    {
        var tex = NewTex();
        Color body  = new Color(0.30f, 0.40f, 0.55f);
        Color rotor = new Color(0.55f, 0.58f, 0.60f);
        Color glass = new Color(0.4f, 0.6f, 0.8f, 0.6f);
        Color tail  = new Color(0.25f, 0.35f, 0.50f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Fuselage
        FillRect(tex, 8, 10, 16, 6, body);
        // Tail boom
        FillRect(tex, 2, 12, 7, 2, tail);
        // Main rotor blade
        DrawLine(tex, 4, 20, 28, 20, rotor);
        DrawLine(tex, 4, 21, 28, 21, rotor);
        // Rotor mast
        DrawLine(tex, 16, 16, 16, 20, rotor);
        // Windshield
        FillRect(tex, 22, 10, 3, 6, glass);
        // Tail rotor
        DrawLine(tex, 3, 16, 3, 10, rotor);
        // Skids
        DrawLine(tex, 8, 8, 24, 8, new Color(0.40f, 0.40f, 0.40f));
        tex.Apply();
        return tex;
    }

    // ── DevBackpack ──────────────────────────────────────────────────
    private static Texture2D DrawDevBackpack()
    {
        var tex = NewTex();
        Color bag   = new Color(0.15f, 0.10f, 0.25f);
        Color strap = new Color(0.60f, 0.20f, 0.80f);
        Color star  = new Color(1.0f, 0.85f, 0.0f);
        Color inf   = new Color(0.90f, 0.90f, 1.0f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Bag body
        FillRect(tex, 6, 4, 20, 22, bag);
        // Flap
        FillRect(tex, 6, 22, 20, 4, new Color(0.20f, 0.15f, 0.30f));
        // Straps
        FillRect(tex, 4, 8, 2, 16, strap);
        FillRect(tex, 26, 8, 2, 16, strap);
        // Infinity symbol (simple approximation)
        FillCircle(tex, 12, 15, 3, inf); FillCircle(tex, 12, 15, 1, bag);
        FillCircle(tex, 20, 15, 3, inf); FillCircle(tex, 20, 15, 1, bag);
        // Star on top flap
        FillCircle(tex, 16, 24, 2, star);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Wire tier icons (Vol 10.4)
    // ---------------------------------------------------------------

    private static Texture2D DrawWireTier(Color wireColor, int tier)
    {
        var tex = NewTex();
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        float w = tier * 1.5f + 1f; // thicker for higher tiers
        int hw = Mathf.CeilToInt(w);
        // Coiled wire shape
        FillRect(tex, 4, 14 - hw, 24, hw * 2, wireColor);
        DrawLine(tex, 4, 14, 12, 6, wireColor);
        DrawLine(tex, 12, 6, 20, 22, wireColor);
        DrawLine(tex, 20, 22, 28, 14, wireColor);
        // Connector dots at ends
        FillCircle(tex, 4, 14, 2, Color.white);
        FillCircle(tex, 28, 14, 2, Color.white);
        // Tier indicator dots
        for (int i = 0; i < tier; i++)
            FillCircle(tex, 12 + i * 4, 28, 1, wireColor);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------
    //  Storage icons (Vol 10.4)
    // ---------------------------------------------------------------

    private static Texture2D DrawChest(Color chestColor)
    {
        var tex = NewTex();
        Color dark = chestColor * 0.6f; dark.a = 1f;
        Color latch = new Color(0.75f, 0.65f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Main body
        FillRect(tex, 4, 4, 24, 18, chestColor);
        // Lid
        FillRect(tex, 4, 18, 24, 8, dark);
        // Latch
        FillRect(tex, 14, 16, 4, 6, latch);
        // Corner reinforcements
        FillRect(tex, 4, 4, 2, 22, dark);
        FillRect(tex, 26, 4, 2, 22, dark);
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawCompressionChest()
    {
        var tex = NewTex();
        Color body  = new Color(0.25f, 0.25f, 0.30f);
        Color panel = new Color(0.15f, 0.40f, 0.80f);
        Color glow  = new Color(0.30f, 0.60f, 1.00f);
        Color edge  = new Color(0.35f, 0.35f, 0.40f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 4, 24, 24, body);
        FillRect(tex, 4, 4, 24, 2, edge);
        FillRect(tex, 4, 26, 24, 2, edge);
        // Blue panel (powered indicator)
        FillRect(tex, 8, 10, 16, 10, panel);
        // Glow lines
        FillRect(tex, 10, 12, 12, 1, glow);
        FillRect(tex, 10, 15, 12, 1, glow);
        FillRect(tex, 10, 18, 12, 1, glow);
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawDriveRack()
    {
        var tex = NewTex();
        Color frame = new Color(0.20f, 0.20f, 0.22f);
        Color slot  = new Color(0.12f, 0.12f, 0.14f);
        Color led   = new Color(0.10f, 0.80f, 0.20f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        FillRect(tex, 4, 2, 24, 28, frame);
        // 4 drive slots
        for (int i = 0; i < 4; i++)
        {
            int y = 4 + i * 7;
            FillRect(tex, 6, y, 20, 5, slot);
            FillCircle(tex, 24, y + 2, 1, led);
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawTerminal()
    {
        var tex = NewTex();
        Color body   = new Color(0.18f, 0.18f, 0.22f);
        Color screen = new Color(0.05f, 0.15f, 0.05f);
        Color text   = new Color(0.20f, 0.80f, 0.20f);
        Color base_  = new Color(0.14f, 0.14f, 0.16f);
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        // Base stand
        FillRect(tex, 8, 2, 16, 4, base_);
        // Monitor body
        FillRect(tex, 4, 6, 24, 22, body);
        // Screen
        FillRect(tex, 6, 8, 20, 18, screen);
        // Text lines
        for (int i = 0; i < 4; i++)
            FillRect(tex, 8, 10 + i * 4, 16, 1, text);
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawNetworkCable()
    {
        var tex = NewTex();
        Color cable = new Color(0.10f, 0.70f, 0.70f); // cyan
        FillRect(tex, 0, 0, 32, 32, Color.clear);
        DrawLine(tex, 4, 14, 14, 6, cable);
        DrawLine(tex, 14, 6, 18, 26, cable);
        DrawLine(tex, 18, 26, 28, 14, cable);
        FillCircle(tex, 4, 14, 2, Color.white);
        FillCircle(tex, 28, 14, 2, Color.white);
        tex.Apply();
        return tex;
    }
}
