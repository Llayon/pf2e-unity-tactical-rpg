using System.Collections.Generic;
using PF2e.TurnSystem;

namespace PF2e.Core
{
    public enum EncounterResult : byte
    {
        Unknown = 0,
        Victory = 1,
        Defeat = 2,
        Aborted = 3
    }

    public readonly struct CombatStartedEvent { }

    public readonly struct CombatEndedEvent
    {
        public readonly EncounterResult result;
        public CombatEndedEvent(EncounterResult result) { this.result = result; }
    }

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

    public readonly struct ConditionsTickedEvent
    {
        public readonly EntityHandle actor;
        public readonly IReadOnlyList<ConditionTick> ticks;

        public ConditionsTickedEvent(EntityHandle actor, IReadOnlyList<ConditionTick> ticks)
        {
            this.actor = actor;
            this.ticks = ticks;
        }
    }

    public readonly struct InitiativeRolledEvent
    {
        public readonly IReadOnlyList<InitiativeEntry> order;

        public InitiativeRolledEvent(IReadOnlyList<InitiativeEntry> order)
        {
            this.order = order;
        }
    }
}
