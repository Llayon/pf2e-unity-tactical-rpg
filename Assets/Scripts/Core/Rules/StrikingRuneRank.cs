namespace PF2e.Core
{
    public enum StrikingRuneRank : byte
    {
        None = 0,
        Striking = 1,
        GreaterStriking = 2,
        MajorStriking = 3
    }

    public static class StrikingRuneRankExtensions
    {
        public static int DamageDiceMultiplier(this StrikingRuneRank rank)
        {
            return rank switch
            {
                StrikingRuneRank.None => 1,
                StrikingRuneRank.Striking => 2,
                StrikingRuneRank.GreaterStriking => 3,
                StrikingRuneRank.MajorStriking => 4,
                _ => 1
            };
        }
    }
}
