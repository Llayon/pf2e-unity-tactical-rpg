namespace PF2e.Core
{
    public static class CheckResolver
    {
        public static CheckResult RollCheck(int modifier, int dc, IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            int nat = rng.RollD20();
            int total = nat + modifier;
            var degree = DegreeOfSuccessResolver.Resolve(total, nat, dc);
            return new CheckResult(nat, modifier, total, dc, degree);
        }

        public static CheckResult RollSkillCheck(EntityData roller, SkillType skill, int dc, IRng rng)
        {
            if (roller == null)
                return RollCheck(0, dc, rng);

            return RollCheck(roller.GetSkillModifier(skill), dc, rng);
        }

        public static CheckResult RollSave(EntityData roller, SaveType save, int dc, IRng rng)
        {
            if (roller == null)
                return RollCheck(0, dc, rng);

            return RollCheck(roller.GetSaveModifier(save), dc, rng);
        }
    }
}
