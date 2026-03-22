using UnityEngine;
using Voidborne.Crafting;

/// <summary>
/// A Schematic Fragment dropped by DirectedCrafter enemies.
///
/// When the player examines this at a Terminal (right-click the Terminal while holding
/// the fragment, or future inventory-examine interaction), the linked CraftingRecipe is
/// added to the player's known recipes list.
///
/// Schematic fragments are consumed on use (single-use item).
/// </summary>
[CreateAssetMenu(fileName = "SchematicFragment", menuName = "Voidborne/Items/Schematic Fragment")]
public class SchematicFragment : ItemDefinition
{
    [Header("Recipe Unlock")]
    [Tooltip("The CraftingRecipe that becomes available once this fragment is examined at a Terminal.")]
    public CraftingRecipe unlocksRecipe;

    [Tooltip("Description of what the schematic contains — shown as flavour text.")]
    [TextArea(2, 4)]
    public string schematicDescription = "Partial manufacturing directive. Examine at a Terminal to decode.";

    /// <summary>
    /// Called by the Terminal interaction when the player examines this fragment.
    /// Returns the recipe to be unlocked, or null if not configured.
    /// </summary>
    public CraftingRecipe Examine()
    {
        if (unlocksRecipe == null)
        {
            Debug.LogWarning($"[SchematicFragment] '{displayName}' has no recipe assigned.");
            return null;
        }

        Debug.Log($"[SchematicFragment] Decoded: {unlocksRecipe.name} is now available to craft.");
        // TODO: notify CraftingManager to add to known recipes list (Terminal Vol 8+)
        return unlocksRecipe;
    }
}
