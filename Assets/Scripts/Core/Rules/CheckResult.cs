namespace PF2e.Core
{
    public readonly struct CheckResult
    {
        public readonly CheckRoll roll;
        public readonly int total;
        public readonly int dc;
        public readonly DegreeOfSuccess degree;

        public int naturalRoll => roll.naturalRoll;
        public int modifier => roll.modifier;

        public CheckResult(in CheckRoll roll, int dc, DegreeOfSuccess degree)
        {
            this.roll = roll;
            this.total = roll.total;
            this.dc = dc;
            this.degree = degree;
        }
    }
}
