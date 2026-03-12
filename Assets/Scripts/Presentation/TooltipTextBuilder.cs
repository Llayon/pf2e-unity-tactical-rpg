using System.Text;
using PF2e.Core;

namespace PF2e.Presentation
{
    public static class TooltipTextBuilder
    {
        private const int ValueColumnWidth = 5;
        private const string ValueMonoSpacing = "0.62em";
        private const string LabelColor = CombatUiPalette.TooltipBodyHex;
        private const string SecondaryColor = CombatUiPalette.TooltipSecondaryHex;
        private const string DividerColor = CombatUiPalette.TooltipDividerHex;
        private const string SectionColor = CombatUiPalette.TooltipTitleHex;
        private const string AccentColor = CombatUiPalette.TooltipAccentHex;
        private const string ValueColor = CombatUiPalette.TooltipValueHex;
        private const string DescriptionColor = CombatUiPalette.TooltipBodyHex;
        private const string SuccessColor = CombatUiPalette.SuccessHex;
        private const string FailureColor = CombatUiPalette.FailureHex;
        private const string CritSuccessColor = CombatUiPalette.CritSuccessHex;
        private const string CritFailureColor = CombatUiPalette.CritFailureHex;

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
            return BuildResultStandardBody(
                naturalRoll,
                attackBonus,
                mapPenalty,
                rangePenalty,
                volleyPenalty,
                aidCircumstanceBonus,
                total,
                degree,
                baseAc,
                coverBonus);
        }

        public static string BuildResultStandardBody(
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
            var sb = new StringBuilder(320);

            AppendSectionHeader(sb, "Attack Roll");
            sb.Append('\n');
            AppendContextLine(sb, $"against AC {effectiveAc}");
            sb.Append('\n');
            sb.Append(BuildUnsignedRow(naturalRoll, "D20 Roll"));
            sb.Append('\n');
            sb.Append(BuildSignedRow(attackBonus, "Attack Bonus"));

            if (mapPenalty != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(mapPenalty, "MAP"));
            }

            if (rangePenalty != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(rangePenalty, "Range Penalty"));
            }

            if (volleyPenalty != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(volleyPenalty, "Volley Penalty"));
            }

            if (aidCircumstanceBonus != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(aidCircumstanceBonus, "Aid"));
            }

            sb.Append('\n');
            AppendDivider(sb);
            sb.Append('\n');
            sb.Append(BuildResultLine(total, degree));
            sb.Append('\n');
            sb.Append('\n');
            AppendSectionHeader(sb, "Armor Class (AC)");
            sb.Append('\n');
            sb.Append(BuildUnsignedRow(baseAc, "Base AC"));

            if (coverBonus != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(coverBonus, "Cover"));
            }

            sb.Append('\n');
            AppendDivider(sb);
            sb.Append('\n');
            sb.Append(BuildTotalLine(effectiveAc));
            return sb.ToString();
        }

        public static string StrikeDamageBreakdown(
            int totalDamage,
            DamageType damageType,
            int fatalBonusDamage = 0,
            int deadlyBonusDamage = 0)
        {
            return BuildDamageCompactBody(totalDamage, damageType, fatalBonusDamage, deadlyBonusDamage);
        }

        public static string BuildDamageCompactBody(
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

            var sb = new StringBuilder(180);
            AppendSectionHeader(sb, "Damage Roll");
            sb.Append('\n');
            sb.Append(BuildUnsignedRow(baseDamage, "Base Damage"));

            if (fatalBonusDamage > 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(fatalBonusDamage, "Fatal Bonus"));
            }

            if (deadlyBonusDamage > 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(deadlyBonusDamage, "Deadly Bonus"));
            }

            sb.Append('\n');
            AppendDivider(sb);
            sb.Append('\n');
            sb.Append($"<color={AccentColor}><b>Total: {totalDamage} {damageType.ToString().ToUpperInvariant()}</b></color>");
            sb.Append('\n');
            sb.Append('\n');
            AppendSectionHeader(sb, "Damage Type");
            sb.Append('\n');
            sb.Append($"<color={SectionColor}><b>{damageType}</b></color>");
            sb.Append('\n');
            sb.Append(BuildDescriptionText(GetDamageTypeDescription(damageType)));
            return sb.ToString();
        }

        public static string SkillCheckResultBreakdown(
            in CheckRoll roll,
            in CheckSource defenseSource,
            int dc,
            DegreeOfSuccess degree,
            int aidCircumstanceBonus = 0)
        {
            string defenseLabel = defenseSource.ToShortLabel();
            string checkLabel = roll.source.ToShortLabel();
            var sb = new StringBuilder(320);

            AppendSectionHeader(sb, $"{checkLabel} Check");
            sb.Append('\n');
            AppendContextLine(sb, $"against {defenseLabel} DC {dc}");
            sb.Append('\n');
            sb.Append(BuildUnsignedRow(roll.naturalRoll, "D20 Roll"));
            sb.Append('\n');
            sb.Append(BuildSignedRow(roll.modifier, "Modifier"));

            if (aidCircumstanceBonus != 0)
            {
                sb.Append('\n');
                sb.Append(BuildSignedRow(aidCircumstanceBonus, "Aid"));
            }

            sb.Append('\n');
            AppendDivider(sb);
            sb.Append('\n');
            sb.Append(BuildResultLine(roll.total, degree));
            sb.Append('\n');
            sb.Append('\n');
            AppendSectionHeader(sb, $"Difficulty Class ({defenseLabel})");
            sb.Append('\n');
            sb.Append(BuildUnsignedRow(dc, "Base"));
            sb.Append('\n');
            AppendDivider(sb);
            sb.Append('\n');
            sb.Append(BuildTotalLine(dc));
            return sb.ToString();
        }

        public static string BuildResultExtendedBody(
            string coreBody,
            string ruleTitle,
            string ruleCategory,
            string ruleDescription)
        {
            var sb = new StringBuilder(512);
            if (!string.IsNullOrEmpty(coreBody))
            {
                sb.Append(coreBody);
            }

            if (!string.IsNullOrEmpty(ruleTitle) || !string.IsNullOrEmpty(ruleDescription))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                    sb.Append('\n');
                }

                if (!string.IsNullOrEmpty(ruleTitle))
                {
                    AppendSectionHeader(sb, ruleTitle);
                    sb.Append('\n');
                }

                if (!string.IsNullOrEmpty(ruleCategory))
                {
                    sb.Append(BuildContextText(ruleCategory.ToUpperInvariant()));
                    sb.Append('\n');
                }

                if (!string.IsNullOrEmpty(ruleDescription))
                {
                    sb.Append(BuildDescriptionText(ruleDescription));
                }
            }

            return sb.ToString();
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

        public static string FormatDegreeColored(DegreeOfSuccess degree)
        {
            string color = degree switch
            {
                DegreeOfSuccess.CriticalSuccess => CritSuccessColor,
                DegreeOfSuccess.Success => SuccessColor,
                DegreeOfSuccess.Failure => FailureColor,
                DegreeOfSuccess.CriticalFailure => CritFailureColor,
                _ => LabelColor
            };

            return $"<color={color}><b>{FormatDegreeLabel(degree)}</b></color>";
        }

        private static string BuildUnsignedRow(int value, string label)
        {
            return BuildRow(value.ToString(), label);
        }

        private static string BuildSignedRow(int value, string label)
        {
            return BuildRow(RollBreakdownFormatter.FormatSigned(value), label);
        }

        private static string BuildRow(string value, string label)
        {
            string valueCell = string.IsNullOrEmpty(value) ? string.Empty : value.PadLeft(ValueColumnWidth);
            string safeLabel = label ?? string.Empty;
            return $"<mspace={ValueMonoSpacing}><color={ValueColor}><b>{valueCell}</b></color></mspace> <color={LabelColor}>{safeLabel}</color>";
        }

        private static string BuildResultLine(int total, DegreeOfSuccess degree)
        {
            return $"<color={AccentColor}><b>Result: {total}</b></color> {FormatDegreeColored(degree)}";
        }

        private static string BuildTotalLine(int total)
        {
            return $"<color={AccentColor}><b>Total: {total}</b></color>";
        }

        private static string GetDamageTypeDescription(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Slashing => "Swords, axes, and monster claws deal slashing damage.",
                DamageType.Piercing => "Puncturing and impaling attacks, including spears and monster bites, deal piercing damage.",
                DamageType.Bludgeoning => "Hammers, clubs, fists, and crushing impacts deal bludgeoning damage.",
                _ => "Damage type description unavailable."
            };
        }

        private static void AppendSectionHeader(StringBuilder sb, string title)
        {
            string safeTitle = string.IsNullOrEmpty(title) ? string.Empty : title;
            sb.Append($"<color={SectionColor}><b>{safeTitle}</b></color>");
        }

        private static void AppendContextLine(StringBuilder sb, string text)
        {
            sb.Append(BuildContextText(text));
        }

        private static string BuildContextText(string text)
        {
            string safeText = text ?? string.Empty;
            return $"<color={SecondaryColor}><size=88%>{safeText}</size></color>";
        }

        private static void AppendDivider(StringBuilder sb)
        {
            sb.Append($"<color={DividerColor}><size=78%>-------------------------</size></color>");
        }

        private static string BuildDescriptionText(string text)
        {
            string safeText = text ?? string.Empty;
            return $"<color={DescriptionColor}><size=90%>{safeText}</size></color>";
        }
    }
}
