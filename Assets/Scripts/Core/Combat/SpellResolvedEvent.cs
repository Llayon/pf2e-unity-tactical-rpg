namespace PF2e.Core
{
    public readonly struct SpellResolvedTargetOutcome
    {
        public readonly EntityHandle target;
        public readonly int shardCount;
        public readonly int[] shardRolls;
        public readonly int rolledDamage;
        public readonly CheckResult? attackResult;
        public readonly CheckResult? saveResult;
        public readonly ConditionType? appliedConditionType;
        public readonly int appliedConditionValue;
        public readonly int appliedConditionRounds;
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
            CheckResult? attackResult,
            CheckResult? saveResult,
            ConditionType? appliedConditionType,
            int appliedConditionValue,
            int appliedConditionRounds,
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
            this.attackResult = attackResult;
            this.saveResult = saveResult;
            this.appliedConditionType = appliedConditionType;
            this.appliedConditionValue = appliedConditionValue;
            this.appliedConditionRounds = appliedConditionRounds;
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
        public readonly int spellAttackModifier;
        public readonly int rolledDamage;
        public readonly SpellResolvedTargetOutcome[] targetOutcomes;

        public SpellResolvedEvent(
            SpellId spellId,
            EntityHandle caster,
            int actionCost,
            int spellDc,
            int spellAttackModifier,
            int rolledDamage,
            SpellResolvedTargetOutcome[] targetOutcomes)
        {
            this.spellId = spellId;
            this.caster = caster;
            this.actionCost = actionCost;
            this.spellDc = spellDc;
            this.spellAttackModifier = spellAttackModifier;
            this.rolledDamage = rolledDamage;
            this.targetOutcomes = targetOutcomes;
        }
    }
}
