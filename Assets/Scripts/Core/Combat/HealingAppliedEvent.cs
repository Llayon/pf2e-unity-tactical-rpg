namespace PF2e.Core
{
    /// <summary>
    /// Typed event for direct healing application.
    /// </summary>
    public readonly struct HealingAppliedEvent
    {
        public readonly EntityHandle source;
        public readonly EntityHandle target;
        public readonly int amount;
        public readonly string sourceActionName;
        public readonly int hpBefore;
        public readonly int hpAfter;

        public HealingAppliedEvent(
            EntityHandle source,
            EntityHandle target,
            int amount,
            string sourceActionName,
            int hpBefore,
            int hpAfter)
        {
            this.source = source;
            this.target = target;
            this.amount = amount;
            this.sourceActionName = sourceActionName;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
        }
    }
}
