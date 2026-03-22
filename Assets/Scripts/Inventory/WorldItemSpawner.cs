using UnityEngine;

/// <summary>
/// Static utility for spawning WorldItem instances in the scene.
/// </summary>
public static class WorldItemSpawner
{
    /// <summary>
    /// Creates a WorldItem in the scene at the given position.
    /// The item is placed slightly above the position so it spawns above ground.
    /// </summary>
    /// <param name="stack">The ItemStack to drop.</param>
    /// <param name="position">The world position to spawn at.</param>
    /// <returns>The spawned WorldItem component.</returns>
    public static WorldItem SpawnItem(ItemStack stack, Vector3 position,
                                      string backpackInstanceId = null)
    {
        if (stack.IsEmpty)
            return null;

        string itemId = stack.item != null ? stack.item.itemId : "unknown";
        GameObject go = new GameObject("WorldItem_" + itemId);
        go.transform.position = position + Vector3.up * 0.3f;

        WorldItem worldItem = go.AddComponent<WorldItem>();
        worldItem.itemStack          = stack;
        worldItem.backpackInstanceId = backpackInstanceId;

        return worldItem;
    }
}
