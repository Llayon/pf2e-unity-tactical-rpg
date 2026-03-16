namespace PF2e.Core
{
    public readonly struct SpellResolvedTargetOutcome
    {
        public readonly EntityHandle target;
        public readonly int shardCount;
        public readonly int[] shardRolls;
        public readonly int rolledDamage;
        public readonly CheckResult? saveResult;
        public readonly int resolvedDamage;
        public readonly int appliedDamage;
        public readonly int hpBefore;
        public readonly int hpAfter;
        public readonly bool targetDefeated;

        public SpellResolvedTargetOutcome(
            EntityHandle target,
            int shardCount,
            int[] shardRolls,
            int rolledDamage,
            CheckResult? saveResult,
            int resolvedDamage,
            int appliedDamage,
            int hpBefore,
            int hpAfter,
            bool targetDefeated)
        {
            this.target = target;
            this.shardCount = shardCount;
            this.shardRolls = shardRolls;
            this.rolledDamage = rolledDamage;
            this.saveResult = saveResult;
            this.resolvedDamage = resolvedDamage;
            this.appliedDamage = appliedDamage;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
            this.targetDefeated = targetDefeated;
        }
    }

    public readonly struct SpellResolvedEvent
    {
        public readonly SpellId spellId;
        public readonly EntityHandle caster;
        public readonly int actionCost;
        public readonly int spellDc;
        public readonly int rolledDamage;
        public readonly SpellResolvedTargetOutcome[] targetOutcomes;

        public SpellResolvedEvent(
            SpellId spellId,
            EntityHandle caster,
            int actionCost,
            int spellDc,
            int rolledDamage,
            SpellResolvedTargetOutcome[] targetOutcomes)
        {
            this.spellId = spellId;
            this.caster = caster;
            this.actionCost = actionCost;
            this.spellDc = spellDc;
            this.rolledDamage = rolledDamage;
            this.targetOutcomes = targetOutcomes;
        }
    }
}
