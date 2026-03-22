using UnityEngine;

namespace Voidborne.Combat
{
    /// <summary>
    /// Static utility that returns the spread cone angle (in degrees) for a gun
    /// based on the player's current movement state.
    /// </summary>
    public static class SpreadCalculator
    {
        // Threshold below which a player is considered "standing still" (units/s).
        private const float StillThreshold = 0.1f;

        // Threshold above which a player is considered fully "moving" / running (units/s).
        private const float WalkThreshold = 4f;

        /// <summary>
        /// Calculates the spread angle in degrees for the supplied gun and movement state.
        /// Priority order (highest to lowest): airborne → crouching → still → walking → running.
        /// </summary>
        /// <param name="gun">The gun definition carrying spread values.</param>
        /// <param name="isGrounded">True when the player is on the ground.</param>
        /// <param name="isCrouching">True when the player is crouching.</param>
        /// <param name="speed">Horizontal speed of the player in units per second.</param>
        /// <returns>Spread angle in degrees.</returns>
        public static float Calculate(GunDefinition gun, bool isGrounded, bool isCrouching, float speed)
        {
            // Airborne takes highest priority.
            if (!isGrounded)
                return gun.spreadAirborne;

            // Crouching beats all ground-movement states.
            if (isCrouching)
                return gun.spreadCrouching;

            // Standing perfectly still → best accuracy.
            if (speed < StillThreshold)
                return gun.spreadStanding;

            // Running at or above the walk threshold → worst ground accuracy.
            if (speed >= WalkThreshold)
                return gun.spreadMoving;

            // Walking: lerp between standing and moving spreads.
            float t = (speed - StillThreshold) / (WalkThreshold - StillThreshold);
            return Mathf.Lerp(gun.spreadStanding, gun.spreadMoving, t);
        }
    }
}
