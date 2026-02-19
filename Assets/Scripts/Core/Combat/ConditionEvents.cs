namespace PF2e.Core
{
    public enum ConditionChangeType : byte
    {
        Added,
        Removed,
        ValueChanged
    }

    public readonly struct ConditionChangedEvent
    {
        public readonly EntityHandle entity;
        public readonly ConditionType conditionType;
        public readonly ConditionChangeType changeType;
        public readonly int oldValue;
        public readonly int newValue;

        public ConditionChangedEvent(
            EntityHandle entity, ConditionType conditionType,
            ConditionChangeType changeType, int oldValue, int newValue)
        {
            this.entity = entity;
            this.conditionType = conditionType;
            this.changeType = changeType;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }
    }
}
