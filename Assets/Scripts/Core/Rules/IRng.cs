namespace PF2e.Core
{
    /// <summary>
    /// Random number generator interface for testable randomness.
    /// </summary>
    public interface IRng
    {
        int RollD20();
        int RollDie(int sides);
    }
}
