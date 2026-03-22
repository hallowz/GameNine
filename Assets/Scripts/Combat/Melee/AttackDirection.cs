using UnityEngine;

namespace Voidborne.Combat.Melee
{
    /// <summary>
    /// The four directional attack types for Mordhau-style melee.
    /// Direction is determined by mouse delta accumulated while the attack/block button is held.
    /// </summary>
    public enum AttackDirection
    {
        Left,
        Right,
        Overhead,
        Stab
    }

    public static class AttackDirectionUtility
    {
        /// <summary>
        /// Deadzone radius in pixels. Delta below this magnitude → Stab (no directional input yet).
        /// </summary>
        private const float DeadzonePixels = 12f;

        /// <summary>
        /// Determines attack or block direction from mouse delta accumulated since button press.
        ///
        /// Mapping:
        ///   Below deadzone  → Stab   (player hasn't committed to a direction)
        ///   Dominant X left → Left
        ///   Dominant X right→ Right
        ///   Dominant Y up   → Overhead
        ///   Dominant Y down → Stab
        /// </summary>
        public static AttackDirection FromMouseDelta(Vector2 delta)
        {
            if (delta.sqrMagnitude < DeadzonePixels * DeadzonePixels)
                return AttackDirection.Stab;

            Vector2 n  = delta.normalized;
            float   ax = Mathf.Abs(n.x);
            float   ay = Mathf.Abs(n.y);

            // Horizontal dominant
            if (ax >= ay)
                return n.x < 0f ? AttackDirection.Left : AttackDirection.Right;

            // Vertical dominant
            return n.y > 0f ? AttackDirection.Overhead : AttackDirection.Stab;
        }

        /// <summary>
        /// Returns a canonical delta vector that will produce <paramref name="dir"/> from
        /// <see cref="FromMouseDelta"/>. Used to seed the windup delta after a direction
        /// morph so the result "sticks" without requiring further mouse movement.
        /// </summary>
        public static Vector2 ToSeedDelta(AttackDirection dir) => dir switch
        {
            AttackDirection.Left     => new Vector2(-50f,   0f),
            AttackDirection.Right    => new Vector2( 50f,   0f),
            AttackDirection.Overhead => new Vector2(  0f,  50f),
            AttackDirection.Stab     => new Vector2(  0f, -50f),
            _                        => Vector2.zero
        };
    }
}
