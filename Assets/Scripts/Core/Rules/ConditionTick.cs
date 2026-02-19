namespace PF2e.Core
{
    public readonly struct ConditionTick
    {
        public readonly ConditionType type;
        public readonly int oldValue;
        public readonly int newValue;
        public readonly bool removed;

        public ConditionTick(ConditionType type, int oldValue, int newValue, bool removed)
        {
            this.type = type;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.removed = removed;
        }
    }
}
