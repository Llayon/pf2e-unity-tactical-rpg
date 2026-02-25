namespace PF2e.Core
{
    public readonly struct StrikeLineResult
    {
        public readonly bool hasLineOfSight;
        public readonly int coverAcBonus;

        public StrikeLineResult(bool hasLineOfSight, int coverAcBonus)
        {
            this.hasLineOfSight = hasLineOfSight;
            this.coverAcBonus = coverAcBonus;
        }
    }
}
