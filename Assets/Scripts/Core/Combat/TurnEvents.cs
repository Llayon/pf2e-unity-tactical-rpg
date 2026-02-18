namespace PF2e.Core
{
    public readonly struct CombatStartedEvent { }
    public readonly struct CombatEndedEvent { }

    public readonly struct RoundStartedEvent
    {
        public readonly int round;
        public RoundStartedEvent(int round) { this.round = round; }
    }

    public readonly struct TurnStartedEvent
    {
        public readonly EntityHandle actor;
        public readonly int actionsAtStart;
        public TurnStartedEvent(EntityHandle actor, int actionsAtStart)
        {
            this.actor = actor;
            this.actionsAtStart = actionsAtStart;
        }
    }

    public readonly struct TurnEndedEvent
    {
        public readonly EntityHandle actor;
        public TurnEndedEvent(EntityHandle actor) { this.actor = actor; }
    }

    public readonly struct ActionsChangedEvent
    {
        public readonly EntityHandle actor;
        public readonly int remaining;
        public ActionsChangedEvent(EntityHandle actor, int remaining)
        {
            this.actor = actor;
            this.remaining = remaining;
        }
    }
}
