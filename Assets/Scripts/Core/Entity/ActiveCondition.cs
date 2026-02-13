namespace PF2e.Core
{
    /// <summary>
    /// A condition currently affecting an entity.
    ///
    /// CURRENT MODEL (Phase 7):
    /// - Conditions with Value (frightened, sickened): Value decreases by 1 each tick.
    ///   Do NOT set RemainingRounds for these — use Value only, RemainingRounds = -1.
    /// - Conditions with duration only (buff lasting N rounds):
    ///   Set Value = 0, RemainingRounds = N.
    /// - Do NOT combine Value > 0 AND RemainingRounds > 0 on same condition.
    ///
    /// TODO Phase 10+: redesign to support Value + Duration simultaneously
    /// (e.g. "slowed 1 for 2 rounds").
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
        /// Returns true if the condition should be removed after ticking.
        /// </summary>
        public bool TickDown()
        {
            if (Value > 0)
            {
                Value--;
                return Value <= 0;
            }
            if (RemainingRounds > 0)
            {
                RemainingRounds--;
                return RemainingRounds <= 0;
            }
            return false;
        }
    }
}
