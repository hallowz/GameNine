using UnityEngine;

namespace Voidborne.Building
{
    public class SnapSocket : MonoBehaviour
    {
        public SocketType socketType;
        public bool isOccupied;
        public BuildingPiece ownerPiece;
        public BuildingPiece connectedPiece;
    }
}
