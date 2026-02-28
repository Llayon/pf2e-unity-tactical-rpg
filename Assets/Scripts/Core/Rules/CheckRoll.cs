namespace PF2e.Core
{
    /// <summary>
    /// Common roll primitive used by skill/save/perception/initiative checks.
    /// </summary>
    public readonly struct CheckRoll
    {
        public readonly int naturalRoll;
        public readonly int modifier;
        public readonly int total;
        public readonly CheckSource source;

        public CheckRoll(int naturalRoll, int modifier, in CheckSource source)
        {
            this.naturalRoll = naturalRoll;
            this.modifier = modifier;
            this.total = naturalRoll + modifier;
            this.source = source;
        }
    }
}
