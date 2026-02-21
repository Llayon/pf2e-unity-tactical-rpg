namespace PF2e.Core
{
    /// <summary>
    /// Canonical condition mutation payload used by condition lifecycle events.
    /// </summary>
    public readonly struct ConditionDelta
    {
        public readonly EntityHandle entity;
        public readonly ConditionType type;
        public readonly ConditionChangeType changeType;
        public readonly int oldValue;
        public readonly int newValue;
        public readonly int oldRemainingRounds;
        public readonly int newRemainingRounds;
        public readonly bool removed;

        public ConditionDelta(
            EntityHandle entity,
            ConditionType type,
            ConditionChangeType changeType,
            int oldValue,
            int newValue)
            : this(entity, type, changeType, oldValue, newValue, -1, -1)
        {
        }

        public ConditionDelta(
            EntityHandle entity,
            ConditionType type,
            ConditionChangeType changeType,
            int oldValue,
            int newValue,
            int oldRemainingRounds,
            int newRemainingRounds)
        {
            this.entity = entity;
            this.type = type;
            this.changeType = changeType;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.oldRemainingRounds = oldRemainingRounds;
            this.newRemainingRounds = newRemainingRounds;
            removed = changeType == ConditionChangeType.Removed;
        }
    }
}
