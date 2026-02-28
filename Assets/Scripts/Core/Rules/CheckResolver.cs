namespace PF2e.Core
{
    public static class CheckResolver
    {
        public static OpposedCheckResult RollOpposedCheck(
            int attackerModifier,
            int defenderModifier,
            in CheckSource attackerSource,
            in CheckSource defenderSource,
            IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            var attackerRoll = new CheckRoll(rng.RollD20(), attackerModifier, attackerSource);
            var defenderRoll = new CheckRoll(rng.RollD20(), defenderModifier, defenderSource);
            return OpposedCheckResult.FromRolls(in attackerRoll, in defenderRoll);
        }

        public static OpposedCheckResult RollOpposedCheck(
            int attackerModifier,
            int defenderModifier,
            IRng rng)
        {
            return RollOpposedCheck(
                attackerModifier,
                defenderModifier,
                CheckSource.Custom("ATK"),
                CheckSource.Custom("DEF"),
                rng);
        }

        public static CheckResult RollCheck(int modifier, int dc, IRng rng)
        {
            return RollCheck(modifier, dc, CheckSource.Custom("Check"), rng);
        }

        public static CheckResult RollCheck(int modifier, int dc, in CheckSource source, IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            int nat = rng.RollD20();
            var roll = new CheckRoll(nat, modifier, source);
            var degree = DegreeOfSuccessResolver.Resolve(roll.total, roll.naturalRoll, dc);
            return new CheckResult(in roll, dc, degree);
        }

        public static CheckRoll RollPerception(EntityData roller, IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            int nat = rng.RollD20();
            int modifier = roller != null ? roller.PerceptionModifier : 0;
            return new CheckRoll(nat, modifier, CheckSource.Perception());
        }

        public static CheckRoll RollSkill(EntityData roller, SkillType skill, IRng rng)
        {
            if (rng == null)
                rng = UnityRng.Shared;

            int nat = rng.RollD20();
            int modifier = roller != null ? roller.GetSkillModifier(skill) : 0;
            return new CheckRoll(nat, modifier, CheckSource.Skill(skill));
        }

        public static CheckResult RollSkillCheck(EntityData roller, SkillType skill, int dc, IRng rng)
        {
            if (roller == null)
                return RollCheck(0, dc, CheckSource.Skill(skill), rng);

            return RollCheck(roller.GetSkillModifier(skill), dc, CheckSource.Skill(skill), rng);
        }

        public static CheckResult RollSave(EntityData roller, SaveType save, int dc, IRng rng)
        {
            if (roller == null)
                return RollCheck(0, dc, CheckSource.Save(save), rng);

            return RollCheck(roller.GetSaveModifier(save), dc, CheckSource.Save(save), rng);
        }
    }
}
