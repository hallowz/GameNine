using System.Collections.Generic;

namespace Voidborne.Data.Schema
{
    /// <summary>
    /// POCO mirroring a single entry in <c>Design Documents/GameDesign/data/npcs.json</c>.
    /// The top-level JSON is a flat array (<c>List&lt;NpcJson&gt;</c>) — entries cover bosses,
    /// finale enemies, fodder, wildlife, named Kin and traders. Use the <c>cat</c> field to
    /// route to the appropriate generator (boss/finale/fodder/wildlife/named/trader).
    /// </summary>
    /// <remarks>
    /// Coop note: pure data POCO; no Unity references; editor-time consumption only.
    /// </remarks>
    [System.Serializable]
    public class NpcJson
    {
        /// <summary>Category: "boss", "finale", "fodder", "wildlife", "named", "trader".</summary>
        public string cat;

        /// <summary>Section / family grouping (e.g. "Brood Family", "Kin Survivors").</summary>
        public string section;

        /// <summary>Display name (e.g. "Fungal Brood Mother").</summary>
        public string name;

        /// <summary>Designer description / lore blurb.</summary>
        public string desc;

        /// <summary>Free-form appearance description.</summary>
        public string appearance;

        /// <summary>Behaviour bullet points.</summary>
        public List<string> behaviors;

        /// <summary>Ability bullet points (free-form strings).</summary>
        public List<string> abilities;

        /// <summary>Drop / trade bullet points (free-form strings — not item IDs).</summary>
        public List<string> drops;
    }
}
