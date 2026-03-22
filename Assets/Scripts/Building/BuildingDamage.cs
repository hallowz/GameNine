namespace Voidborne.Building
{
    public static class BuildingDamage
    {
        public enum AttackerType
        {
            PickaxeMelee,
            AxeMelee,
            Gun,
            Explosive,
            VoidWeapon
        }

        public static float GetMultiplier(AttackerType attacker, MaterialTier tier)
        {
            switch (attacker)
            {
                case AttackerType.Gun:
                    return 0.25f;

                case AttackerType.Explosive:
                    return 3.0f;

                case AttackerType.AxeMelee:
                    if (tier == MaterialTier.Wood)  return 1.0f;
                    if (tier == MaterialTier.Stone) return 0.5f;
                    if (tier == MaterialTier.Iron)  return 0.25f;
                    return 0.5f;

                case AttackerType.PickaxeMelee:
                    if (tier == MaterialTier.Wood)  return 0.5f;
                    if (tier == MaterialTier.Stone) return 1.0f;
                    if (tier == MaterialTier.Iron)  return 1.0f;
                    return 0.75f;

                case AttackerType.VoidWeapon:
                    return 1.5f;

                default:
                    return 0.5f;
            }
        }
    }
}
