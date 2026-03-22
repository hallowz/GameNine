using System;
using System.Collections.Generic;
using UnityEngine;
using Voidborne.World.Biomes;

namespace Voidborne.Enemies
{
    public enum EnemyCategory
    {
        Optimized,      // Fully processed Kin — no personality, pure function
        Directed,       // Partially overwritten Kin — retain personality fragments, can speak
        WildCreature    // Non-Kin fauna
    }

    /// <summary>
    /// Drop entry in the enemy loot table.
    /// </summary>
    [Serializable]
    public class LootEntry
    {
        public ItemDefinition item;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
        [Range(0f, 1f)] public float dropChance = 0.5f;
    }

    /// <summary>
    /// ScriptableObject defining an enemy type's stats, AI behaviour, and loot.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Voidborne/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName = "Unknown Kin";
        public EnemyCategory category = EnemyCategory.Optimized;

        [Header("Stats")]
        [Min(1f)]  public float maxHealth       = 100f;
        [Min(0f)]  public float moveSpeed        = 4f;
        [Min(0f)]  public float attackDamage     = 15f;
        [Min(0f)]  public float attackRange      = 1.8f;
        [Min(0f)]  public float detectionRange   = 18f;
        /// <summary>Flat damage reduction per hit (0 = no armour).</summary>
        [Min(0f)]  public float armor            = 0f;

        [Header("Combat Timing")]
        [Min(0.1f)] public float attackCooldown  = 1.5f;
        [Min(0f)]   public float attackWindup    = 0.3f;

        [Header("Loot")]
        public List<LootEntry> lootTable = new();

        [Header("Spawning — Biome & Depth")]
        [Tooltip("Leave empty to allow spawning in all biomes.")]
        public List<BiomeDefinition> biomeRestrictions = new();
        public float minDepthY = -9999f;
        public float maxDepthY =  9999f;
        [Tooltip("How many can exist in the world at once (per spawner).")]
        [Min(1)] public int populationCap = 20;
        [Tooltip("Min/max group size when spawning an Optimized cluster (ignored for Directed/Wild).")]
        [Min(1)] public int groupSizeMin = 1;
        [Min(1)] public int groupSizeMax = 1;

        [Header("Directed — Narrative")]
        [Tooltip("Only meaningful for Directed category. Enables Directive Pen partial-reset interaction.")]
        public bool canBeRestored = false;
        [Tooltip("Lines the Directed enemy speaks when the player approaches unarmed. One line chosen at random.")]
        [TextArea(2, 4)]
        public string[] converseLines = new string[0];

        [Header("Behavior")]
        [Tooltip("Utility AI weights. Leave at zero to use the category default.")]
        public BehaviorWeights behaviorWeights;

        [Header("Visuals")]
        [Tooltip("Prefab instantiated in the scene for this enemy. Must have an EnemyEntity component.")]
        public GameObject prefab;

        private void OnValidate()
        {
            // Auto-fill behavior weights from category default if all zero
            if (behaviorWeights.aggression == 0f && behaviorWeights.tactical == 0f
                && behaviorWeights.cowardice == 0f && behaviorWeights.speedMult == 0f)
            {
                behaviorWeights = BehaviorWeights.ForCategory(category);
            }
        }
    }
}
