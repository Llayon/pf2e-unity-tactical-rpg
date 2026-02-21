namespace PF2e.Core
{
    public enum ConditionChangeType : byte
    {
        Added,
        Removed,
        ValueChanged,
        DurationChanged
    }

    public readonly struct ConditionChangedEvent
    {
        public readonly EntityHandle entity;
        public readonly ConditionType conditionType;
        public readonly ConditionChangeType changeType;
        public readonly int oldValue;
        public readonly int newValue;
        public readonly int oldRemainingRounds;
        public readonly int newRemainingRounds;

        public ConditionChangedEvent(
            EntityHandle entity, ConditionType conditionType,
            ConditionChangeType changeType, int oldValue, int newValue)
            : this(entity, conditionType, changeType, oldValue, newValue, -1, -1)
        {
        }

        public ConditionChangedEvent(
            EntityHandle entity, ConditionType conditionType,
            ConditionChangeType changeType, int oldValue, int newValue,
            int oldRemainingRounds, int newRemainingRounds)
        {
            this.entity = entity;
            this.conditionType = conditionType;
            this.changeType = changeType;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.oldRemainingRounds = oldRemainingRounds;
            this.newRemainingRounds = newRemainingRounds;
        }
    }
}
