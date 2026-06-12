using UnityEngine;

namespace Voidborne.NPCs
{
    /// <summary>
    /// Volume 2.4 NPC definition — content data for friendly / interactable
    /// NPCs (named Kin and traders). Generated en masse by
    /// <c>Assets/Editor/Data/NpcSoGenerator.cs</c>.
    /// </summary>
    /// <remarks>
    /// Coop note: stateless content data. Per-instance state (current
    /// dialogue, trade inventory, quest flags) lives on a runtime
    /// MonoBehaviour wrapper added in Volumes 16 / 18.
    /// </remarks>
    [CreateAssetMenu(fileName = "NewNpc", menuName = "Voidborne/NPCs/NPC Definition")]
    public class NpcDefinition : ScriptableObject
    {
        // ----- Identity -----
        [Header("Identity")]
        [Tooltip("Slugified ID derived from the JSON name (e.g. 'vesh_the_smith').")]
        public string id;

        public string displayName;

        [TextArea(2, 6)]
        public string description;

        [Tooltip("Section / grouping from the source JSON (e.g. 'Kin Survivors', 'Travelling Merchants').")]
        public string section;

        // ----- Behaviour -----
        [Header("Behaviour")]
        public string[] behaviors;
        public string[] abilities;

        // ----- Inventory / trade -----
        [Header("Inventory / trade")]
        [Tooltip("Drop / trade bullets from the source JSON. Free-form strings — V16 / V18 will resolve them.")]
        public string[] dropOrTradeItems;

        // ----- Flags -----
        [Header("Flags")]
        [Tooltip("True if this NPC can be killed by the player. Named Kin + traders are non-killable.")]
        public bool isKillable;

        [Tooltip("True if this NPC can hand out quests (heuristic: cat == 'named').")]
        public bool isQuestGiver;

        // ----- Visuals (Volume 3 owns this) -----
        [Header("Visuals (assigned by Volume 3 pipeline)")]
        public GameObject prefab;
    }
}
