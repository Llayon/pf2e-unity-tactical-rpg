namespace PF2e.Core
{
    public readonly struct AidPreparedRecord
    {
        public readonly EntityHandle helper;
        public readonly EntityHandle ally;
        public readonly int preparedRound;
        public readonly int preparedOnHelperTurnStartCount;

        public AidPreparedRecord(
            EntityHandle helper,
            EntityHandle ally,
            int preparedRound,
            int preparedOnHelperTurnStartCount)
        {
            this.helper = helper;
            this.ally = ally;
            this.preparedRound = preparedRound;
            this.preparedOnHelperTurnStartCount = preparedOnHelperTurnStartCount;
        }
    }
}
