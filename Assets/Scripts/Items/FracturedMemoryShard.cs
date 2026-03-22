using UnityEngine;

/// <summary>
/// A Fractured Memory Shard dropped by DirectedSentinel enemies.
///
/// Contains a readable lore fragment from the former personality of the Kin before
/// they were processed. Each shard is unique — one sentence, one moment, partially
/// corrupted by the overwrite.
///
/// Right-click in inventory (or use while held) prints the lore to the Debug log
/// and triggers a subtitle line. Full subtitle UI hookup is deferred to Vol 11 (Polish).
/// </summary>
[CreateAssetMenu(fileName = "FracturedMemoryShard", menuName = "Voidborne/Items/Fractured Memory Shard")]
public class FracturedMemoryShard : ItemDefinition
{
    [Header("Lore Content")]
    [Tooltip("The actual memory fragment text. Keep it brief and unsettling.")]
    [TextArea(4, 10)]
    public string loreText = "[DATA CORRUPTED] — fragment unrecoverable.";

    [Tooltip("Attribution shown before the lore text (e.g. 'Kin-7 — Former Keeper of the Eastern Gate').")]
    public string memoryAuthor = "Unknown Kin";

    /// <summary>
    /// Called when the player reads (uses) this shard.
    /// Outputs the memory to the console and hooks into a future subtitle system.
    /// </summary>
    public void Read()
    {
        string header = $"── Memory Fragment: {memoryAuthor} ──";
        Debug.Log($"[MEMORY]\n{header}\n{loreText}\n{new string('─', header.Length)}");

        // TODO: route through SubtitleUI / DialogueUI when implemented (Vol 11)
    }
}
