using System;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Asymmetric render distance with separate values for each axis direction.
    /// Allows different render distances per direction — e.g. fewer chunks below
    /// the player (hidden caves) and more above (visible mountains).
    /// </summary>
    [Serializable]
    public struct DirectionalDistance : IEquatable<DirectionalDistance>
    {
        [Tooltip("+X direction (chunks)")]
        public int posX;
        [Tooltip("-X direction (chunks)")]
        public int negX;
        [Tooltip("+Y direction / up (chunks)")]
        public int posY;
        [Tooltip("-Y direction / down (chunks)")]
        public int negY;
        [Tooltip("+Z direction (chunks)")]
        public int posZ;
        [Tooltip("-Z direction (chunks)")]
        public int negZ;

        /// <summary>Symmetric horizontal, separate up/down vertical.</summary>
        public DirectionalDistance(int horizontal, int verticalUp, int verticalDown)
        {
            posX = negX = posZ = negZ = horizontal;
            posY = verticalUp;
            negY = verticalDown;
        }

        /// <summary>Symmetric horizontal and vertical.</summary>
        public DirectionalDistance(int horizontal, int vertical)
        {
            posX = negX = posZ = negZ = horizontal;
            posY = negY = vertical;
        }

        /// <summary>
        /// Returns true if the signed offset (dx, dy, dz) from the player chunk
        /// is within this distance box.
        /// </summary>
        public bool Contains(int dx, int dy, int dz)
        {
            return dx >= -negX && dx <= posX
                && dy >= -negY && dy <= posY
                && dz >= -negZ && dz <= posZ;
        }

        /// <summary>
        /// Contains check with an extra margin added to each direction.
        /// Used for hysteresis to prevent oscillation at LOD boundaries.
        /// </summary>
        public bool ContainsWithMargin(int dx, int dy, int dz, int margin)
        {
            return dx >= -(negX + margin) && dx <= posX + margin
                && dy >= -(negY + margin) && dy <= posY + margin
                && dz >= -(negZ + margin) && dz <= posZ + margin;
        }

        /// <summary>Maximum horizontal extent across all 4 horizontal directions.</summary>
        public int MaxHorizontal => Mathf.Max(posX, Mathf.Max(negX, Mathf.Max(posZ, negZ)));

        /// <summary>Maximum vertical extent (up or down).</summary>
        public int MaxVertical => Mathf.Max(posY, negY);

        /// <summary>True if all directions are zero or negative (LOD level disabled).</summary>
        public bool IsDisabled => MaxHorizontal <= 0;

        /// <summary>Per-component maximum of two distances.</summary>
        public static DirectionalDistance Max(DirectionalDistance a, DirectionalDistance b)
        {
            return new DirectionalDistance
            {
                posX = Mathf.Max(a.posX, b.posX),
                negX = Mathf.Max(a.negX, b.negX),
                posY = Mathf.Max(a.posY, b.posY),
                negY = Mathf.Max(a.negY, b.negY),
                posZ = Mathf.Max(a.posZ, b.posZ),
                negZ = Mathf.Max(a.negZ, b.negZ),
            };
        }

        /// <summary>Per-component minimum of two distances (used for capping).</summary>
        public static DirectionalDistance Min(DirectionalDistance a, DirectionalDistance b)
        {
            return new DirectionalDistance
            {
                posX = Mathf.Min(a.posX, b.posX),
                negX = Mathf.Min(a.negX, b.negX),
                posY = Mathf.Min(a.posY, b.posY),
                negY = Mathf.Min(a.negY, b.negY),
                posZ = Mathf.Min(a.posZ, b.posZ),
                negZ = Mathf.Min(a.negZ, b.negZ),
            };
        }

        /// <summary>
        /// Expands this box to include the point at signed offset (dx, dy, dz).
        /// </summary>
        public void ExpandToInclude(int dx, int dy, int dz)
        {
            if (dx > posX) posX = dx;
            if (-dx > negX) negX = -dx;
            if (dy > posY) posY = dy;
            if (-dy > negY) negY = -dy;
            if (dz > posZ) posZ = dz;
            if (-dz > negZ) negZ = -dz;
        }

        public bool Equals(DirectionalDistance other)
        {
            return posX == other.posX && negX == other.negX
                && posY == other.posY && negY == other.negY
                && posZ == other.posZ && negZ == other.negZ;
        }

        public override bool Equals(object obj) => obj is DirectionalDistance d && Equals(d);
        public override int GetHashCode() => HashCode.Combine(posX, negX, posY, negY, posZ, negZ);
        public static bool operator ==(DirectionalDistance a, DirectionalDistance b) => a.Equals(b);
        public static bool operator !=(DirectionalDistance a, DirectionalDistance b) => !a.Equals(b);
    }
}
