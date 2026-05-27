using UnityEngine;

namespace Voidborne.Enemies.V2
{
    /// <summary>
    /// Vord enemy archetype family — derived by the V2.4 generator from the
    /// NPC's <c>section</c> string in npcs.json (e.g. "Brood Family",
    /// "Warden Family", "Hunter Family"). Drives spawn rules and AI selection
    /// in Volume 15.
    /// </summary>
    public enum EnemyArchetype
    {
        Brood,
        Warden,
        Hunter,
        Channeler,
        Aberrant,
        Fodder,
        Finale,
        Other
    }

    /// <summary>
    /// Volume 2.4 enemy definition — content data for hostile NPCs (boss /
    /// finale / fodder). Generated en masse by
    /// <c>Assets/Editor/Data/EnemySoGenerator.cs</c>.
    /// </summary>
    /// <remarks>
    /// Lives in the <c>Voidborne.Enemies.V2</c> namespace to avoid colliding
    /// with the pre-existing <c>Voidborne.Enemies.EnemyDefinition</c> from
    /// the legacy enemy system (kept in service by EnemySpawner/EnemyBrain
    /// until Volume 15 replaces it). Volume 15 will consolidate these.
    ///
    /// Coop note: stateless content data. Per-instance state lives on the
    /// EnemyEntity / NetworkBehaviour spawned in Volume 15.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Voidborne/Enemies/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        // ----- Identity -----
        [Header("Identity")]
        [Tooltip("Slugified ID derived from the JSON name (e.g. 'mist_hunter').")]
        public string id;

        public string displayName;

        [TextArea(2, 6)]
        public string description;

        // ----- Tier / Family -----
        [Header("Classification")]
        [Tooltip("Boss tier 1-3 (parsed from desc / section). 0 for fodder / finale or when no tier was given.")]
        public int tier;

        [Tooltip("Family / archetype derived from the JSON 'section' field.")]
        public EnemyArchetype family = EnemyArchetype.Other;

        // ----- Behaviour -----
        [Header("Behaviour")]
        public string[] behaviors;
        public string[] abilities;

        // ----- Drops -----
        [Header("Drops")]
        [Tooltip("Drop bullets from JSON. Free-form strings — V15 / V20 will resolve them to item IDs.")]
        public string[] dropItemIds;

        // ----- Boss/section metadata -----
        [Header("Source metadata")]
        [Tooltip("Section string from the source JSON (e.g. 'Brood Family'). Empty for fodder/finale where the section is generic.")]
        public string boss_section;

        public bool isBoss;
        public bool isFinale;

        // ----- Stats -----
        [Header("Stats (defaults — balance in V15)")]
        [Min(1f)] public float baseHealth = 30f;
        [Min(0f)] public float baseSpeed = 4f;

        // ----- Visuals (Volume 3 owns this) -----
        [Header("Visuals (assigned by Volume 3 pipeline)")]
        public GameObject prefab;
    }
}
