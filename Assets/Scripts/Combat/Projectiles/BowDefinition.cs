using UnityEngine;

namespace Voidborne.Combat.Projectiles
{
    [CreateAssetMenu(menuName = "Voidborne/Combat/Bow Definition", fileName = "NewBow")]
    public class BowDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string bowName = "Bow";

        [Header("Projectile")]
        [Tooltip("Arrow or bolt definition fired by this bow.")]
        public ProjectileDefinition arrowDefinition;

        [Header("Draw Mechanics")]
        [Tooltip("Minimum draw time (seconds) before the bow can fire.")]
        public float minDrawTime = 0.2f;

        [Tooltip("Time (seconds) to reach full draw power.")]
        public float fullDrawTime = 1.2f;

        [Tooltip("Arrow launch speed (m/s) at minimum draw.")]
        public float minVelocity = 15f;

        [Tooltip("Arrow launch speed (m/s) at full draw.")]
        public float maxVelocity = 55f;

        [Header("Accuracy")]
        [Tooltip("Cone of spread in degrees when released at minimum draw.")]
        public float spreadAtMinDraw = 5f;

        [Tooltip("Cone of spread in degrees when released at full draw.")]
        public float spreadAtFullDraw = 0.5f;

        [Tooltip("Seconds after full draw time before aim wobble begins.")]
        public float wobbleStartDelay = 1.5f;

        [Tooltip("Maximum degrees of aim wobble when the bow is over-drawn.")]
        public float wobbleMaxAngle = 6f;

        [Header("Stamina")]
        [Tooltip("Stamina drained per second while the bow is held at draw.")]
        public float staminaDrainPerSecond = 8f;

        [Header("View")]
        [Tooltip("FOV multiplier applied while the bow is drawn. 1 = no zoom.")]
        public float drawZoom = 1.15f;
    }
}
