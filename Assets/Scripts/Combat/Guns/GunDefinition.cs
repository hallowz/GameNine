using UnityEngine;

namespace Voidborne.Combat
{
    public enum FireMode
    {
        Auto,
        Semi,
        Burst
    }

    public enum BulletType
    {
        Hitscan,
        Projectile
    }

    [CreateAssetMenu(menuName = "Voidborne/Gun Definition")]
    public class GunDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string gunName;

        [Header("Firing")]
        public float fireRate;          // rounds per minute
        public float damage;
        public int magazineSize;
        public float reloadTime;
        public FireMode fireMode;
        public BulletType bulletType;
        [Tooltip("Number of pellets fired per shot. Default 1 (single bullet). Set > 1 for shotguns.")]
        [SerializeField] public int pelletCount = 1;

        [Header("Recoil & Spread")]
        public Vector2[] recoilPattern; // x = horizontal, y = vertical camera offset per shot
        public float spreadStanding;
        public float spreadMoving;
        public float spreadCrouching;
        public float spreadAirborne;

        [Header("Ballistics")]
        public float range;
        public float penetration;

        [Header("Handling")]
        public float adsZoomMultiplier;
        public float equipTime;

        [Header("Visuals & Audio")]
        public GameObject modelPrefab1P;
        public GameObject modelPrefab3P;
        public GameObject muzzleFlashPrefab;
        public AudioClip fireSound;
        public AudioClip reloadSound;
    }
}
