using UnityEngine;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// ScriptableObject that holds all OreDefinitions for the game.
    /// Wire ore definition assets into the oreDefinitions array in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "OreRegistry", menuName = "Voidborne/Ore Registry")]
    public class OreRegistry : ScriptableObject
    {
        [Tooltip("All ore definitions registered in this world. Order matters: lower-index ores " +
                 "are evaluated first during generation, giving them placement priority.")]
        public OreDefinition[] oreDefinitions;

        /// <summary>
        /// Returns the OreDefinition with the given oreTypeId, or null if not found.
        /// Uses a linear search — this is only called once at startup or on lookup, not per-voxel.
        /// </summary>
        public OreDefinition GetOreById(byte id)
        {
            if (oreDefinitions == null) return null;

            for (int i = 0; i < oreDefinitions.Length; i++)
            {
                if (oreDefinitions[i] != null && oreDefinitions[i].oreTypeId == id)
                    return oreDefinitions[i];
            }

            return null;
        }
    }
}
