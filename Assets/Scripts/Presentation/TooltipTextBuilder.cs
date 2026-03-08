using PF2e.Core;

namespace PF2e.Presentation
{
    public static class TooltipTextBuilder
    {
        public static string StrikeResultBreakdown(
            int naturalRoll,
            int attackBonus,
            int mapPenalty,
            int rangePenalty,
            int volleyPenalty,
            int aidCircumstanceBonus,
            int total,
            DegreeOfSuccess degree,
            int baseAc,
            int coverBonus)
        {
            int effectiveAc = baseAc + coverBonus;
            string map = mapPenalty != 0
                ? $"MAP: {RollBreakdownFormatter.FormatSigned(mapPenalty)}\n"
                : string.Empty;
            string range = rangePenalty != 0
                ? $"Range Penalty: {RollBreakdownFormatter.FormatSigned(rangePenalty)}\n"
                : string.Empty;
            string volley = volleyPenalty != 0
                ? $"Volley Penalty: {RollBreakdownFormatter.FormatSigned(volleyPenalty)}\n"
                : string.Empty;
            string aid = aidCircumstanceBonus != 0
                ? $"Aid: {RollBreakdownFormatter.FormatSigned(aidCircumstanceBonus)}\n"
                : string.Empty;
            string cover = coverBonus != 0
                ? $"Cover: {RollBreakdownFormatter.FormatSigned(coverBonus)}\n"
                : string.Empty;

            return
                $"Attack Roll vs AC {effectiveAc}\n" +
                $"D20 Roll: {naturalRoll}\n" +
                $"Attack Bonus: {RollBreakdownFormatter.FormatSigned(attackBonus)}\n" +
                map +
                range +
                volley +
                aid +
                $"Total: {total}\n" +
                $"Degree: {FormatDegreeLabel(degree)}\n\n" +
                "Armor Class\n" +
                $"Base AC: {baseAc}\n" +
                cover +
                $"Total: {effectiveAc}";
        }

        public static string StrikeDamageBreakdown(
            int totalDamage,
            DamageType damageType,
            int fatalBonusDamage = 0,
            int deadlyBonusDamage = 0)
        {
            int traitBonus = 0;
            if (fatalBonusDamage > 0)
            {
                traitBonus += fatalBonusDamage;
            }

            if (deadlyBonusDamage > 0)
            {
                traitBonus += deadlyBonusDamage;
            }

            int baseDamage = totalDamage - traitBonus;
            if (baseDamage < 0)
            {
                baseDamage = 0;
            }

            string fatal = fatalBonusDamage > 0
                ? $"Fatal Bonus: {RollBreakdownFormatter.FormatSigned(fatalBonusDamage)}\n"
                : string.Empty;
            string deadly = deadlyBonusDamage > 0
                ? $"Deadly Bonus: {RollBreakdownFormatter.FormatSigned(deadlyBonusDamage)}\n"
                : string.Empty;

            return
                "Damage Roll\n" +
                $"Base Damage: {baseDamage}\n" +
                fatal +
                deadly +
                $"Total: {totalDamage} {damageType.ToString().ToUpperInvariant()}";
        }

        public static string SkillCheckResultBreakdown(
            in CheckRoll roll,
            in CheckSource defenseSource,
            int dc,
            DegreeOfSuccess degree,
            int aidCircumstanceBonus = 0)
        {
            string aid = aidCircumstanceBonus != 0
                ? $"Aid: {RollBreakdownFormatter.FormatSigned(aidCircumstanceBonus)}\n"
                : string.Empty;

            string defenseLabel = defenseSource.ToShortLabel();
            return
                $"{roll.source.ToShortLabel()} Check vs {defenseLabel} DC {dc}\n" +
                $"D20 Roll: {roll.naturalRoll}\n" +
                $"Modifier: {RollBreakdownFormatter.FormatSigned(roll.modifier)}\n" +
                aid +
                $"Total: {roll.total}\n" +
                $"Degree: {FormatDegreeLabel(degree)}\n\n" +
                "Defense\n" +
                $"{defenseLabel} DC: {dc}\n" +
                $"Total: {dc}";
        }

        public static string FormatDegreeLabel(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => "Critical Success!",
                DegreeOfSuccess.Success => "Success!",
                DegreeOfSuccess.Failure => "Failure",
                DegreeOfSuccess.CriticalFailure => "Critical Failure!",
                _ => degree.ToString()
            };
        }
    }
}
