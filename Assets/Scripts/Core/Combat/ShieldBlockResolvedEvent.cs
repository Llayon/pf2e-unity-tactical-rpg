namespace PF2e.Core
{
    public readonly struct ShieldBlockResolvedEvent
    {
        public readonly EntityHandle reactor;
        public readonly int incomingDamage;
        public readonly int damageReduction;
        public readonly int shieldSelfDamage;
        public readonly int shieldHpBefore;
        public readonly int shieldHpAfter;

        public ShieldBlockResolvedEvent(
            EntityHandle reactor,
            int incomingDamage,
            int damageReduction,
            int shieldSelfDamage,
            int shieldHpBefore,
            int shieldHpAfter)
        {
            this.reactor = reactor;
            this.incomingDamage = incomingDamage;
            this.damageReduction = damageReduction;
            this.shieldSelfDamage = shieldSelfDamage;
            this.shieldHpBefore = shieldHpBefore;
            this.shieldHpAfter = shieldHpAfter;
        }
    }
}
