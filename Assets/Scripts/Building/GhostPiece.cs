using UnityEngine;

namespace Voidborne.Building
{
    public class GhostPiece : MonoBehaviour
    {
        public SnapSocket[] sockets;
        public bool isValid;

        private void Awake()
        {
            sockets = GetComponentsInChildren<SnapSocket>();
        }
    }
}
