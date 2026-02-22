using System;

namespace PF2e.Core
{
    public static class ShieldBlockRules
    {
        public static ShieldBlockResult Calculate(ShieldInstance shield, int incomingDamage)
        {
            if (incomingDamage <= 0)
                return new ShieldBlockResult(0, 0);

            if (!shield.IsEquipped || shield.IsBroken)
                return new ShieldBlockResult(0, 0);

            int hardness = Math.Max(0, shield.Hardness);
            int reduction = Math.Min(incomingDamage, hardness);
            int shieldSelfDamage = Math.Max(0, incomingDamage - hardness);

            return new ShieldBlockResult(reduction, shieldSelfDamage);
        }
    }
}
