namespace PF2e.Core
{
    public readonly struct DelayTurnBeginTriggerChangedEvent
    {
        public readonly EntityHandle actor;
        public readonly bool isOpen;

        public DelayTurnBeginTriggerChangedEvent(EntityHandle actor, bool isOpen)
        {
            this.actor = actor;
            this.isOpen = isOpen;
        }
    }

    public readonly struct DelayPlacementSelectionChangedEvent
    {
        public readonly EntityHandle actor;
        public readonly bool isOpen;

        public DelayPlacementSelectionChangedEvent(EntityHandle actor, bool isOpen)
        {
            this.actor = actor;
            this.isOpen = isOpen;
        }
    }

    public readonly struct DelayReturnWindowOpenedEvent
    {
        public readonly EntityHandle afterActor;

        public DelayReturnWindowOpenedEvent(EntityHandle afterActor)
        {
            this.afterActor = afterActor;
        }
    }

    public readonly struct DelayReturnWindowClosedEvent
    {
        public readonly EntityHandle afterActor;

        public DelayReturnWindowClosedEvent(EntityHandle afterActor)
        {
            this.afterActor = afterActor;
        }
    }

    public readonly struct DelayedTurnEnteredEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle plannedReturnAfterActor;

        public DelayedTurnEnteredEvent(EntityHandle actor, EntityHandle plannedReturnAfterActor)
        {
            this.actor = actor;
            this.plannedReturnAfterActor = plannedReturnAfterActor;
        }
    }

    public readonly struct DelayedTurnResumedEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle afterActor;
        public readonly bool wasPlanned;

        public DelayedTurnResumedEvent(EntityHandle actor, EntityHandle afterActor, bool wasPlanned)
        {
            this.actor = actor;
            this.afterActor = afterActor;
            this.wasPlanned = wasPlanned;
        }
    }

    public readonly struct DelayedTurnExpiredEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle afterActor;

        public DelayedTurnExpiredEvent(EntityHandle actor, EntityHandle afterActor)
        {
            this.actor = actor;
            this.afterActor = afterActor;
        }
    }
}
