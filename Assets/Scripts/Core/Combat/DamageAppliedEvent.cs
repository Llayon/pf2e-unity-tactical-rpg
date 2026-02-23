namespace PF2e.Core
{
    /// <summary>
    /// Typed event for generic damage application (non-strike and future unified damage paths).
    /// </summary>
    public readonly struct DamageAppliedEvent
    {
        public readonly EntityHandle source;
        public readonly EntityHandle target;
        public readonly int amount;
        public readonly DamageType damageType;
        public readonly string sourceActionName;
        public readonly bool isCritical;
        public readonly int hpBefore;
        public readonly int hpAfter;
        public readonly bool targetDefeated;

        public DamageAppliedEvent(
            EntityHandle source,
            EntityHandle target,
            int amount,
            DamageType damageType,
            string sourceActionName,
            bool isCritical,
            int hpBefore,
            int hpAfter,
            bool targetDefeated)
        {
            this.source = source;
            this.target = target;
            this.amount = amount;
            this.damageType = damageType;
            this.sourceActionName = sourceActionName;
            this.isCritical = isCritical;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
            this.targetDefeated = targetDefeated;
        }
    }
}
