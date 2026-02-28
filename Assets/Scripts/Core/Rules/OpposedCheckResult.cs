namespace PF2e.Core
{
    public enum OpposedCheckWinner : byte
    {
        Tie = 0,
        Attacker = 1,
        Defender = 2
    }

    /// <summary>
    /// Result of a contested roll where both sides roll d20 + modifier.
    /// margin is attacker.total - defender.total.
    /// </summary>
    public readonly struct OpposedCheckResult
    {
        public readonly CheckRoll attackerRoll;
        public readonly CheckRoll defenderRoll;
        public readonly int margin;
        public readonly OpposedCheckWinner winner;

        private OpposedCheckResult(in CheckRoll attackerRoll, in CheckRoll defenderRoll, int margin, OpposedCheckWinner winner)
        {
            this.attackerRoll = attackerRoll;
            this.defenderRoll = defenderRoll;
            this.margin = margin;
            this.winner = winner;
        }

        public static OpposedCheckResult FromRolls(in CheckRoll attackerRoll, in CheckRoll defenderRoll)
        {
            int margin = attackerRoll.total - defenderRoll.total;
            OpposedCheckWinner winner = margin switch
            {
                > 0 => OpposedCheckWinner.Attacker,
                < 0 => OpposedCheckWinner.Defender,
                _ => OpposedCheckWinner.Tie
            };

            return new OpposedCheckResult(in attackerRoll, in defenderRoll, margin, winner);
        }

        /// <summary>
        /// Builds an opposed-style projection for check-vs-DC flows.
        /// The defender roll uses natural=0 and modifier=dc so defender.total equals the DC.
        /// </summary>
        public static OpposedCheckResult FromRollVsDc(in CheckRoll attackerRoll, int dc, in CheckSource defenseSource)
        {
            var defenderProjection = new CheckRoll(naturalRoll: 0, modifier: dc, defenseSource);
            return FromRolls(in attackerRoll, in defenderProjection);
        }
    }
}
