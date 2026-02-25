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
        public readonly int rangePenalty;
        public readonly int volleyPenalty;
        public readonly int coverAcBonus;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;

        public StrikeCheckResult(
            bool performed,
            StrikeFailureReason failureReason,
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int rangePenalty,
            int volleyPenalty,
            int coverAcBonus,
            int total,
            int dc,
            DegreeOfSuccess degree)
        {
            this.performed = performed;
            this.failureReason = failureReason;
            this.naturalRoll = naturalRoll;
            this.attackBonus = attackBonus;
            this.mapPenalty = mapPenalty;
            this.rangePenalty = rangePenalty;
            this.volleyPenalty = volleyPenalty;
            this.coverAcBonus = coverAcBonus;
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
                rangePenalty: 0,
                volleyPenalty: 0,
                coverAcBonus: 0,
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
            DegreeOfSuccess degree,
            int rangePenalty = 0,
            int volleyPenalty = 0,
            int coverAcBonus = 0)
        {
            return new StrikeCheckResult(
                performed: true,
                failureReason: StrikeFailureReason.None,
                naturalRoll: naturalRoll,
                attackBonus: attackBonus,
                mapPenalty: mapPenalty,
                rangePenalty: rangePenalty,
                volleyPenalty: volleyPenalty,
                coverAcBonus: coverAcBonus,
                total: total,
                dc: dc,
                degree: degree);
        }

        public static StrikeCheckResult Success(
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int total,
            int dc,
            DegreeOfSuccess degree)
        {
            return Success(naturalRoll, attackBonus, mapPenalty, total, dc, degree, rangePenalty: 0);
        }
    }
}
