namespace Voidborne.Automation
{
    /// <summary>
    /// Canonical machine process-type vocabulary (Volume 2.7).
    ///
    /// Stored on <see cref="MachineDefinition.processType"/>. Drives the forgiving/picky/hybrid
    /// matching logic in <c>CraftingMatchEngine</c> (Volume 6.1):
    /// <list type="bullet">
    /// <item><description><b>Forgiving</b> machines accept any physically defensible input with
    ///   realistic efficiency curves - synergy lives here. They try specific <c>inputs[]</c>
    ///   recipes first; if none match, fall through to <c>inputProperties[]</c> matching.</description></item>
    /// <item><description><b>Picky</b> machines refuse property fallback - progression lives here.
    ///   Refinery refuses anything that isn't crude oil; Auto-Turret refuses anything that isn't a
    ///   built turret recipe.</description></item>
    /// <item><description><b>Hybrid</b> machines (workbench, carpenter's bench) match specific
    ///   recipes first AND allow property-based 'improvised' recipes at reduced quality (0.7x cap).</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Machines without a clear process type default to <see cref="Picky_Specialty"/>.
    /// Pure data, no Unity refs. Coop note: stateless content vocabulary.
    /// </remarks>
    public enum MachineProcessType
    {
        // ----- Forgiving (synergy lives here) -----

        /// <summary>Campfire, Furnace, Forge. Accepts any <c>Combustible_*</c> as fuel; transforms target item via heat.</summary>
        Forgiving_Thermal_DryBurn,

        /// <summary>Steam Boiler. Accepts any <c>Liquid_Aqueous</c> as working medium + any <c>Combustible_*</c> as heat source.</summary>
        Forgiving_Thermal_Boil,

        /// <summary>Composter, Fermenter. Accepts any <c>Organic_*</c>; produces decay/fermentation product based on input subtype.</summary>
        Forgiving_Organic_Decay,

        /// <summary>Drying Rack. Accepts any <c>Organic_Fresh</c> or <c>Liquid_Aqueous</c>-containing item; removes moisture.</summary>
        Forgiving_Organic_Dry,

        /// <summary>Crusher, Grinder. Accepts any <c>Solid_*</c>; outputs <c>Solid_Powder</c> variant.</summary>
        Forgiving_Mechanical_Crush,

        /// <summary>Centrifuge. Accepts any mixture; outputs components by density.</summary>
        Forgiving_Mechanical_Separate,

        /// <summary>Press. Accepts compatible inputs; outputs sheets/extrusions.</summary>
        Forgiving_Pressure,

        // ----- Picky (progression lives here) -----

        /// <summary>Chemistry Set, Alchemy Bench. Only specific reagent combinations work.</summary>
        Picky_Chemical,

        /// <summary>Refinery. Only crude oil -> refined products. No substitutes.</summary>
        Picky_Refinement,

        /// <summary>Assembler, Etcher. Component-specific recipes. No property fallback.</summary>
        Picky_Assembly,

        /// <summary>Default catch-all picky type. Hand-authored recipes only. Used for Taxidermy
        /// Bench, Apiary, Cheese Press, Storage Chest, Conveyor Belt, Inserter, Auto Turret,
        /// Steam Generator, Hand Crank Generator, etc.</summary>
        Picky_Specialty,

        // ----- Hybrid -----

        /// <summary>Workbench, Carpenter's Bench. Specific recipes + improvised property matching at 0.7x quality cap.</summary>
        Hybrid_Crafting
    }
}
