namespace PF2e.Core
{
    /// <summary>
    /// A condition currently affecting an entity.
    /// Value and RemainingRounds are independent.
    /// RemainingRounds = -1 means infinite duration.
    /// </summary>
    [System.Serializable]
    public class ActiveCondition
    {
        public ConditionType Type;
        public int Value;
        public int RemainingRounds;

        public ActiveCondition(ConditionType type, int value = 0, int remainingRounds = -1)
        {
            Type = type;
            Value = value;
            RemainingRounds = remainingRounds;
        }

        /// <summary>
        /// Apply one tick and return true if the condition should be removed.
        /// - Value and duration countdown are independent.
        /// - Duration reaching 0 always removes.
        /// - Valued conditions with infinite duration remove when value reaches 0.
        /// </summary>
        public bool TickDown(bool decrementValue, bool decrementRounds)
        {
            if (decrementValue && Value > 0)
                Value--;

            if (decrementRounds && RemainingRounds > 0)
                RemainingRounds--;

            if (RemainingRounds == 0)
                return true;

            return ConditionRules.IsValued(Type) && Value <= 0 && RemainingRounds < 0;
        }
    }
}
