using System.Collections.Generic;

namespace Voidborne.Building
{
    public static class SocketCompatibility
    {
        private static readonly HashSet<(SocketType, SocketType)> _pairs = new HashSet<(SocketType, SocketType)>
        {
            (SocketType.FoundationEdge,   SocketType.FoundationEdge),
            (SocketType.FoundationEdge,   SocketType.WallBottom),
            (SocketType.WallTop,          SocketType.FloorEdge),
            (SocketType.WallTop,          SocketType.WallBottom),
            (SocketType.FloorEdge,        SocketType.FloorEdge),
            (SocketType.FloorEdge,        SocketType.WallBottom),
            (SocketType.FoundationCorner, SocketType.PillarBottom),
            // Half wall connections — walls, floors, and foundations can sit on top
            (SocketType.HalfWallTop,      SocketType.WallBottom),
            (SocketType.HalfWallTop,      SocketType.FloorEdge),
            (SocketType.HalfWallTop,      SocketType.FoundationEdge),
            // Foundation vertical stacking
            (SocketType.FoundationStack,  SocketType.FoundationStack),
            // Wall side-to-side (end-to-end horizontal)
            (SocketType.WallSide,         SocketType.WallSide),
        };

        public static bool AreCompatible(SocketType a, SocketType b)
        {
            return _pairs.Contains((a, b)) || _pairs.Contains((b, a));
        }
    }
}
