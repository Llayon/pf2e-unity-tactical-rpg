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
        public readonly bool removed;

        public ConditionDelta(
            EntityHandle entity,
            ConditionType type,
            ConditionChangeType changeType,
            int oldValue,
            int newValue)
        {
            this.entity = entity;
            this.type = type;
            this.changeType = changeType;
            this.oldValue = oldValue;
            this.newValue = newValue;
            removed = changeType == ConditionChangeType.Removed;
        }
    }
}
