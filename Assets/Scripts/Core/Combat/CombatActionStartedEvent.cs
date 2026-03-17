using System;

namespace PF2e.Core
{
    public enum CombatActionKind : byte
    {
        Other = 0,
        Stand = 1,
        Spell = 2
    }

    [Flags]
    public enum CombatActionTraitFlags : byte
    {
        None = 0,
        Manipulate = 1 << 0
    }

    /// <summary>
    /// Published immediately before an action resolves, allowing reactions such as Reactive Strike
    /// to interrupt the source action without spending its action cost on failure.
    /// </summary>
    public readonly struct CombatActionStartedEvent
    {
        public readonly EntityHandle actor;
        public readonly string actionName;
        public readonly CombatActionKind actionKind;
        public readonly CombatActionTraitFlags traits;
        public readonly int actionCost;

        public CombatActionStartedEvent(
            EntityHandle actor,
            string actionName,
            CombatActionKind actionKind,
            CombatActionTraitFlags traits,
            int actionCost)
        {
            this.actor = actor;
            this.actionName = actionName ?? string.Empty;
            this.actionKind = actionKind;
            this.traits = traits;
            this.actionCost = actionCost;
        }

        public bool HasTrait(CombatActionTraitFlags trait)
        {
            return (traits & trait) == trait;
        }
    }
}
