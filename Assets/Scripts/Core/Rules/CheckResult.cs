namespace PF2e.Core
{
    public readonly struct CheckResult
    {
        public readonly int naturalRoll;
        public readonly int modifier;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;

        public CheckResult(int naturalRoll, int modifier, int total, int dc, DegreeOfSuccess degree)
        {
            this.naturalRoll = naturalRoll;
            this.modifier = modifier;
            this.total = total;
            this.dc = dc;
            this.degree = degree;
        }
    }
}
