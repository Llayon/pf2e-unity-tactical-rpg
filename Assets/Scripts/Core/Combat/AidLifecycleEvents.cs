namespace PF2e.Core
{
    public enum AidClearReason : byte
    {
        None = 0,
        Consumed = 1,
        ExpiredOnHelperTurnStart = 2,
        CombatEnded = 3,
        OverwrittenByNewPreparation = 4
    }

    public readonly struct AidPreparedEvent
    {
        public readonly EntityHandle helper;
        public readonly EntityHandle ally;
        public readonly int preparedRound;

        public AidPreparedEvent(EntityHandle helper, EntityHandle ally, int preparedRound)
        {
            this.helper = helper;
            this.ally = ally;
            this.preparedRound = preparedRound;
        }
    }

    public readonly struct AidClearedEvent
    {
        public readonly EntityHandle helper;
        public readonly EntityHandle ally;
        public readonly AidClearReason reason;

        public AidClearedEvent(EntityHandle helper, EntityHandle ally, AidClearReason reason)
        {
            this.helper = helper;
            this.ally = ally;
            this.reason = reason;
        }
    }
}
