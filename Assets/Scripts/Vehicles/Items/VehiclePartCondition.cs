using UnityEngine;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Runtime condition of a vehicle part instance. Carried by WorldItem and VehicleBodyPartSlot.
    /// </summary>
    [System.Serializable]
    public struct VehiclePartCondition
    {
        public float currentHP;
        public float maxHP;
        public DamageStage stage;

        public float Ratio => maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
        public bool IsDestroyed => stage == DamageStage.Destroyed;

        public static VehiclePartCondition Fresh(float maxHP)
        {
            return new VehiclePartCondition
            {
                currentHP = maxHP,
                maxHP = maxHP,
                stage = DamageStage.Healthy
            };
        }

        /// <summary>Apply damage and update stage. Returns true if stage changed.</summary>
        public bool TakeDamage(float amount)
        {
            if (IsDestroyed) return false;

            currentHP = Mathf.Max(0f, currentHP - amount);
            var oldStage = stage;
            stage = EvaluateStage();
            return stage != oldStage;
        }

        /// <summary>Restore HP and recalculate stage. Returns true if stage changed.</summary>
        public bool Repair(float amount)
        {
            if (IsDestroyed) return false;

            currentHP = Mathf.Min(maxHP, currentHP + amount);
            var oldStage = stage;
            stage = EvaluateStage();
            return stage != oldStage;
        }

        DamageStage EvaluateStage()
        {
            float ratio = Ratio;
            if (ratio <= 0f) return DamageStage.Destroyed;
            if (ratio <= 0.3f) return DamageStage.Critical;
            if (ratio <= 0.6f) return DamageStage.Damaged;
            return DamageStage.Healthy;
        }
    }
}
