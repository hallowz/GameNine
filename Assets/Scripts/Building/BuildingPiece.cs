using UnityEngine;
using Voidborne.Combat;

namespace Voidborne.Building
{
    public class BuildingPiece : MonoBehaviour, IDamageable
    {
        public BuildingPieceType pieceType;
        public MaterialTier materialTier;
        public float health;
        public float maxHealth;
        public SnapSocket[] sockets;
        public string buildingPieceId;

        [Header("Data Reference")]
        public BuildingPieceData pieceData;

        private bool _dead;

        private void Awake()
        {
            sockets = GetComponentsInChildren<SnapSocket>();
            foreach (var s in sockets)
                s.ownerPiece = this;
        }

        public void Initialize(BuildingPieceData data, MaterialTier tier)
        {
            pieceData = data;
            pieceType = data.pieceType;
            materialTier = tier;
            int tierIndex = (int)tier;
            maxHealth = (data.healthPerTier != null && tierIndex < data.healthPerTier.Length)
                ? data.healthPerTier[tierIndex]
                : 150f;
            health = maxHealth;
            buildingPieceId = System.Guid.NewGuid().ToString();
        }

        // IDamageable
        public void TakeDamage(DamageInfo info)
        {
            if (_dead) return;
            BuildingDamage.AttackerType attackerType = DamageTypeToAttacker(info.Type);
            float multiplier = BuildingDamage.GetMultiplier(attackerType, materialTier);
            health -= info.Amount * multiplier;
            if (health <= 0f) Die();
        }

        public void TakeBuildingDamage(float amount, BuildingDamage.AttackerType attackerType)
        {
            if (_dead) return;
            float multiplier = BuildingDamage.GetMultiplier(attackerType, materialTier);
            health -= amount * multiplier;
            if (health <= 0f) Die();
        }

        private static BuildingDamage.AttackerType DamageTypeToAttacker(DamageType type)
        {
            switch (type)
            {
                case DamageType.Bullet:     return BuildingDamage.AttackerType.Gun;
                case DamageType.Explosive:  return BuildingDamage.AttackerType.Explosive;
                case DamageType.Projectile: return BuildingDamage.AttackerType.Gun;
                default:                    return BuildingDamage.AttackerType.PickaxeMelee;
            }
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;

            // Clear socket connections
            foreach (var socket in sockets)
            {
                if (socket.connectedPiece != null)
                {
                    // Find the partner socket on the connected piece and clear it
                    foreach (var partnerSocket in socket.connectedPiece.sockets)
                    {
                        if (partnerSocket.connectedPiece == this)
                        {
                            partnerSocket.isOccupied = false;
                            partnerSocket.connectedPiece = null;
                        }
                    }
                }
            }

            BuildingManager.Instance?.UnregisterPiece(this);
            DropRefund();
            Destroy(gameObject);
        }

        private void DropRefund()
        {
            // Drop 50% of crafting cost if the piece data has associated item info
            // This hooks into WorldItemSpawner if available
            if (pieceData == null || string.IsNullOrEmpty(pieceData.inventoryItemId)) return;
            // Refund handled externally via events or direct spawning
        }
    }
}
