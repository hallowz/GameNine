using UnityEngine;

namespace Voidborne.Fauna
{
    /// <summary>
    /// Volume 2.4 fauna definition — content data for a single non-Vord
    /// wildlife entry (NPCs.json <c>cat == "wildlife"</c>). Generated en masse
    /// by <c>Assets/Editor/Data/FaunaSoGenerator.cs</c>.
    /// </summary>
    /// <remarks>
    /// IDs are derived from the JSON <c>name</c> via
    /// <see cref="Voidborne.Editor.Data.ParsingHelpers.SlugifyName(string)"/>
    /// since the source JSON does not carry an explicit ID.
    ///
    /// Coop note: stateless content data. Runtime AI state lives on
    /// MonoBehaviours / NetworkBehaviours spawned in Volume 14.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewFauna", menuName = "Voidborne/Fauna/Fauna Definition")]
    public class FaunaDefinition : ScriptableObject
    {
        // ----- Identity -----
        [Header("Identity")]
        [Tooltip("Slugified ID derived from the JSON name (e.g. 'mist_hunter').")]
        public string id;

        public string displayName;

        [TextArea(2, 6)]
        public string description;

        // ----- Behaviour -----
        [Header("Behaviour")]
        [Tooltip("Raw behaviour bullet points from the source JSON. Drives AI tuning in V14.")]
        public string[] behaviors;

        [Tooltip("Habitat hint derived from the source section (e.g. 'Lowlands Fauna'). May be empty.")]
        public string habitat;

        // ----- Drops -----
        [Header("Drops")]
        [Tooltip("Drop bullets from the source JSON. Free-form strings — V14 / V20 will resolve them to item IDs.")]
        public string[] dropItemIds;

        // ----- Flags -----
        [Header("Flags")]
        public bool isTameable;
        public bool isPassive;
        public bool isAggressive;

        // ----- Stats -----
        [Header("Stats (defaults — balance in V14)")]
        [Min(1f)] public float baseHealth = 50f;
        [Min(0f)] public float baseSpeed = 3f;

        // ----- Visuals (Volume 3 owns this) -----
        [Header("Visuals (assigned by Volume 3 pipeline)")]
        public GameObject prefab;
    }
}
