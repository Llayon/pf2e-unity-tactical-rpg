namespace PF2e.Core
{
    /// <summary>
    /// Result of an attack check resolution.
    /// </summary>
    public readonly struct StrikeCheckResult
    {
        public readonly bool performed;
        public readonly StrikeFailureReason failureReason;
        public readonly int naturalRoll;
        public readonly int attackBonus;
        public readonly int mapPenalty;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;

        public StrikeCheckResult(
            bool performed,
            StrikeFailureReason failureReason,
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int total,
            int dc,
            DegreeOfSuccess degree)
        {
            this.performed = performed;
            this.failureReason = failureReason;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
        }

        public static StrikeCheckResult Failed(StrikeFailureReason reason)
        {
            return new StrikeCheckResult(
                performed: false,
                failureReason: reason,
                naturalRoll: 0,
                attackBonus: 0,
                mapPenalty: 0,
                total: 0,
                dc: 0,
                degree: DegreeOfSuccess.CriticalFailure);
        }

        public static StrikeCheckResult Success(
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int total,
            int dc,
            DegreeOfSuccess degree)
        {
            return new StrikeCheckResult(
                performed: true,
                failureReason: StrikeFailureReason.None,
                naturalRoll: naturalRoll,
                attackBonus: attackBonus,
                mapPenalty: mapPenalty,
                total: total,
                dc: dc,
                degree: degree);
        }
    }
}
