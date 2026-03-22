using UnityEngine;

namespace Voidborne.Combat
{
    public enum DamageType
    {
        Bullet,
        Melee,
        Projectile,
        Explosive,
        Fire,
        Poison,
        Energy,     // Cortex ability damage (Pulse shockwave, Conductor arc)
        Generic     // Untyped / fallback
    }

    public struct DamageInfo
    {
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public DamageType Type;
        public GameObject Attacker;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }
}
