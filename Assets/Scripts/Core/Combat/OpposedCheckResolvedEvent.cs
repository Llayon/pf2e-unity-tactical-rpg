namespace PF2e.Core
{
    /// <summary>
    /// Typed event for contested checks where both sides roll.
    /// </summary>
    public readonly struct OpposedCheckResolvedEvent
    {
        public readonly EntityHandle actor;
        public readonly EntityHandle target;
        public readonly string actionName;
        public readonly CheckRoll attackerRoll;
        public readonly CheckRoll defenderRoll;
        public readonly int margin;
        public readonly OpposedCheckWinner winner;

        public OpposedCheckResolvedEvent(
            EntityHandle actor,
            EntityHandle target,
            string actionName,
            in CheckRoll attackerRoll,
            in CheckRoll defenderRoll,
            int margin,
            OpposedCheckWinner winner)
        {
            this.actor = actor;
            this.target = target;
            this.actionName = actionName;
            this.attackerRoll = attackerRoll;
            this.defenderRoll = defenderRoll;
            this.margin = margin;
            this.winner = winner;
        }
    }
}
