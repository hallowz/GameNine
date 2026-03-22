using UnityEngine;
using Voidborne.Crafting;

namespace Voidborne.Automation
{
    /// <summary>
    /// An ItemDefinition subclass representing a Schematic Card: a physical item that encodes
    /// a CraftingRecipe and can be inserted into the Assembler's recipe slot.
    ///
    /// Crafted at the Workbench from paper + copper components.
    /// Also found as loot in VORD facilities and Architect strongholds.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSchematicCard", menuName = "Voidborne/Automation/Schematic Card")]
    public class SchematicCard : ItemDefinition
    {
        [Header("Encoded Recipe")]
        [Tooltip("The CraftingRecipe this schematic card encodes. Must be a 3x3 workbench recipe.")]
        public CraftingRecipe encodedRecipe;
    }
}
