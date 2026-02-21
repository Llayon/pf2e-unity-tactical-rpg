namespace PF2e.Core
{
    public readonly struct ShieldRaisedEvent
    {
        public readonly EntityHandle actor;
        public readonly int acBonus;
        public readonly int shieldHP;
        public readonly int shieldMaxHP;

        public ShieldRaisedEvent(EntityHandle actor, int acBonus, int shieldHP, int shieldMaxHP)
        {
            this.actor = actor;
            this.acBonus = acBonus;
            this.shieldHP = shieldHP;
            this.shieldMaxHP = shieldMaxHP;
        }
    }
}
