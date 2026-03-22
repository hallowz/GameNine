using UnityEngine;

namespace Voidborne.Building
{
    [CreateAssetMenu(menuName = "Building/Piece Data")]
    public class BuildingPieceData : ScriptableObject
    {
        public BuildingPieceType pieceType;
        public GameObject prefab;
        public GameObject ghostPrefab;
        public string inventoryItemId;
        public bool canPlaceOnTerrain;

        [Tooltip("Index 0=Wood, 1=Stone, 2=Iron")]
        public float[] healthPerTier = new float[3] { 150f, 400f, 800f };
    }
}
