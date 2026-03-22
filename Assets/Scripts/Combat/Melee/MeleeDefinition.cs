using UnityEngine;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// ScriptableObject that defines the properties of a melee weapon.
    /// Create via: Assets > Create > Voidborne > Melee Definition
    /// </summary>
    [CreateAssetMenu(menuName = "Voidborne/Melee Definition", fileName = "NewMeleeWeapon")]
    public class MeleeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName = "Melee Weapon";

        [Header("Damage")]
        [Tooltip("Base damage per hit.")]
        public float damage = 30f;

        [Tooltip("Damage type to apply.")]
        public DamageType damageType = DamageType.Melee;

        [Header("Range & Hit")]
        [Tooltip("Reach of the weapon in metres.")]
        public float range = 2.5f;

        [Tooltip("Width of the hit arc (radius of the sweep sphere cast).")]
        public float hitRadius = 0.4f;

        [Header("Attack Timing (seconds)")]
        [Tooltip("Time before the attack becomes active. Player committed but not yet dangerous.")]
        public float windupTime = 0.25f;

        [Tooltip("Active window during which hits are registered.")]
        public float releaseTime = 0.20f;

        [Tooltip("Recovery time after the swing; cannot attack again until this ends.")]
        public float recoveryTime = 0.35f;

        [Header("Stamina")]
        [Tooltip("Stamina consumed per swing.")]
        public float staminaCost = 15f;

        [Header("Speed")]
        [Tooltip("Multiplier applied to all timings (>1 = faster weapon).")]
        [Range(0.3f, 3f)]
        public float attackSpeed = 1f;

        [Header("Combo")]
        [Tooltip("Maximum number of chained hits before forced recovery gap.")]
        [Range(1, 6)]
        public int maxComboChain = 3;

        [Tooltip("Extra time added to recovery when the combo chain is exhausted.")]
        public float comboExhaustionPenalty = 0.4f;

        [Header("On-Hit Effects")]
        [Tooltip("Fraction of target armor ignored on hit (0 = no penetration, 1 = full bypass).")]
        [Range(0f, 1f)]
        public float armorPenetration = 0f;

        [Tooltip("Stagger duration applied to the hit target (0 = no extra stagger).")]
        public float hitStaggerDuration = 0f;

        [Header("Visual")]
        [Tooltip("First-person weapon model prefab.")]
        public GameObject firstPersonModel;

        // Convenience accessors that apply attackSpeed scaling.
        public float ScaledWindupTime   => windupTime   / attackSpeed;
        public float ScaledReleaseTime  => releaseTime  / attackSpeed;
        public float ScaledRecoveryTime => recoveryTime / attackSpeed;
    }
}
