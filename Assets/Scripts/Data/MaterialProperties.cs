namespace Voidborne.Data
{
    /// <summary>
    /// Canonical material-property vocabulary (Volume 2.6).
    ///
    /// Every Core 60 item declares 1-4 of these in its <c>properties[]</c> field in
    /// <c>items_core.json</c>. Machines may declare zero (their behaviour is governed by
    /// <see cref="Voidborne.Automation.MachineProcessType"/> instead).
    ///
    /// Drives the forgiving/picky crafting match engine (see Volume 6.1). Forgiving machines
    /// fall through to property-based matching when no specific recipe matches; picky machines
    /// refuse property fallback; hybrid machines accept both, capping output modifier at 0.7x
    /// for property matches.
    ///
    /// Vocabulary is intentionally short and physically grounded - extending later is cheap
    /// because new tags simply become finer-grained slots in forgiving recipes.
    /// </summary>
    /// <remarks>
    /// Pure data, no Unity references - lives in <c>Voidborne.Data</c> so both runtime and
    /// editor assemblies can reference it. Coop note: read-only content data.
    /// </remarks>
    public enum MaterialProperties
    {
        // ----- Combustibles -----
        /// <summary>Wood, coal, dried fat, charcoal. Burns well.</summary>
        Combustible_Dry,
        /// <summary>Fresh meat, raw fish. Burns badly.</summary>
        Combustible_Wet,
        /// <summary>Oil, alcohol, rendered fat. Burns hot.</summary>
        Combustible_Liquid,
        /// <summary>Gunpowder, refined fuel. Explosive.</summary>
        Combustible_Volatile,

        // ----- Liquids -----
        /// <summary>Water, milk, blood, sap. Boils at low energy. Has thermal mass.</summary>
        Liquid_Aqueous,
        /// <summary>Crude oil, rendered fat, refined oil. Slippery, flammable.</summary>
        Liquid_Oil,
        /// <summary>Acids, solvents. Reactive.</summary>
        Liquid_Alchemical,

        // ----- Organic states -----
        /// <summary>Anything just harvested from a plant or animal.</summary>
        Organic_Fresh,
        /// <summary>Rotted, composted, fermented matter.</summary>
        Organic_Decayed,
        /// <summary>Drying-rack output.</summary>
        Organic_Dried,
        /// <summary>Sugar-containing matter, suitable for fermentation.</summary>
        Organic_Sweet,

        // ----- Solid bulk -----
        /// <summary>Ingots, raw ore.</summary>
        Solid_Metal,
        /// <summary>Rocks, gravel.</summary>
        Solid_Stone,
        /// <summary>Crushed/ground output.</summary>
        Solid_Powder,
        /// <summary>Plant fibers, hide strips.</summary>
        Solid_Fiber,

        // ----- Specialty -----
        /// <summary>Wires, conductive components.</summary>
        Conducts_Electric,
        /// <summary>Gems, structured minerals.</summary>
        Crystalline,
        /// <summary>Exotic, void-touched, spirit-bound.</summary>
        Magical
    }
}
