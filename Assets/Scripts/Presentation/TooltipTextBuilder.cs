using PF2e.Core;

namespace PF2e.Presentation
{
    public static class TooltipTextBuilder
    {
        public static string StrikeAttackBreakdown(
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int rangePenalty,
            int volleyPenalty,
            int aidCircumstanceBonus,
            int total)
        {
            string rng = rangePenalty != 0 ? $" + RNG({RollBreakdownFormatter.FormatSigned(rangePenalty)})" : string.Empty;
            string volley = volleyPenalty != 0 ? $" + VOLLEY({RollBreakdownFormatter.FormatSigned(volleyPenalty)})" : string.Empty;
            string aid = aidCircumstanceBonus != 0 ? $" + AID({RollBreakdownFormatter.FormatSigned(aidCircumstanceBonus)})" : string.Empty;
            return
                $"d20({naturalRoll})" +
                $" + ATK({RollBreakdownFormatter.FormatSigned(attackBonus)})" +
                $" + MAP({RollBreakdownFormatter.FormatSigned(mapPenalty)})" +
                rng +
                volley +
                aid +
                $" = {total}";
        }

        public static string StrikeDefenseBreakdown(int baseAc, int coverBonus)
        {
            if (coverBonus == 0)
            {
                return $"AC {baseAc} = {baseAc}";
            }

            int effectiveAc = baseAc + coverBonus;
            return $"AC {baseAc} + COVER({RollBreakdownFormatter.FormatSigned(coverBonus)}) = {effectiveAc}";
        }

        public static string SkillCheckBreakdown(in CheckRoll roll, int aidCircumstanceBonus = 0)
        {
            string aid = aidCircumstanceBonus != 0
                ? $" + AID({RollBreakdownFormatter.FormatSigned(aidCircumstanceBonus)})"
                : string.Empty;
            return
                $"{roll.source.ToShortLabel()} d20({roll.naturalRoll})" +
                $" {RollBreakdownFormatter.FormatSigned(roll.modifier)}" +
                aid +
                $" = {roll.total}";
        }
    }
}
