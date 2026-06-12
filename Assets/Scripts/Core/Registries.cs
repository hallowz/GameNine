using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.Fauna;
using Voidborne.NPCs;

namespace Voidborne.Core
{
    /// <summary>
    /// Volume 2.4 static facade exposing every generated content registry
    /// through one entry point. Use this from gameplay code to avoid having
    /// to remember each registry's individual singleton.
    /// </summary>
    /// <remarks>
    /// All singletons are loaded lazily from <c>Assets/Resources/</c> via
    /// each registry's own <c>Instance</c> property. Run the V2 generators
    /// (Items, Recipes, Machines, Fauna, Enemies, NPCs) before invoking the
    /// facade — otherwise the underlying <c>Resources.Load</c> calls return
    /// null and the registry logs an error.
    ///
    /// Coop note: read-only registries; safe to share between server and
    /// client.
    /// </remarks>
    public static class Registries
    {
        /// <summary>The Volume 2.2 ItemDatabase.</summary>
        public static ItemDatabase Items => ItemDatabase.GetOrLoad();

        /// <summary>The Volume 2.3 RecipeRegistry.</summary>
        public static RecipeRegistry Recipes => RecipeRegistry.Instance;

        /// <summary>The Volume 2.4 MachineRegistry.</summary>
        public static MachineRegistry Machines => MachineRegistry.Instance;

        /// <summary>The Volume 2.4 FaunaRegistry.</summary>
        public static FaunaRegistry Fauna => FaunaRegistry.Instance;

        /// <summary>The Volume 2.4 EnemyRegistry (V2 enemies — distinct from the legacy <c>Voidborne.Enemies</c> system retired in V15).</summary>
        public static Voidborne.Enemies.V2.EnemyRegistry Enemies => Voidborne.Enemies.V2.EnemyRegistry.Instance;

        /// <summary>The Volume 2.4 NpcRegistry.</summary>
        public static NpcRegistry Npcs => NpcRegistry.Instance;
    }
}
