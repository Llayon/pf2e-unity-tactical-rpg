namespace PF2e.Core
{
    public readonly struct ShieldBlockResult
    {
        public readonly int targetDamageReduction;
        public readonly int shieldSelfDamage;

        public ShieldBlockResult(int targetDamageReduction, int shieldSelfDamage)
        {
            this.targetDamageReduction = targetDamageReduction;
            this.shieldSelfDamage = shieldSelfDamage;
        }
    }
}
