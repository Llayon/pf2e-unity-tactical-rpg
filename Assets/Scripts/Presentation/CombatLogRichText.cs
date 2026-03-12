using UnityEngine;
using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// TMP rich text helpers for combat log color coding (Solasta 2 palette).
    /// </summary>
    public static class CombatLogRichText
    {
        private const string AccentFont = "Lora SDF";

        // Entity names
        public const string PlayerColor = CombatUiPalette.PlayerNameHex;
        public const string EnemyColor = CombatUiPalette.EnemyNameHex;
        public const string NeutralColor = CombatUiPalette.NeutralTextHex;

        // Elements
        public const string WeaponColor = CombatUiPalette.WeaponTextHex;
        public const string VerbColor = CombatUiPalette.NarrativeTextHex;
        public const string RoundColor = CombatUiPalette.RoundHex;
        public const string ConditionGainColor = CombatUiPalette.ConditionGainHex;
        public const string ConditionLoseColor = CombatUiPalette.ConditionLoseHex;
        public const string DefeatedColor = CombatUiPalette.DefeatedHex;
        public const string StatusTokenColor = CombatUiPalette.StatusTokenHex;
        public const string HealColor = CombatUiPalette.HealHex;

        // Degree of success
        public const string SuccessColor = CombatUiPalette.SuccessHex;
        public const string FailureColor = CombatUiPalette.FailureHex;
        public const string CritSuccessColor = CombatUiPalette.CritSuccessHex;
        public const string CritFailureColor = CombatUiPalette.CritFailureHex;

        // Damage types
        public const string SlashingColor = CombatUiPalette.SlashingHex;
        public const string PiercingColor = CombatUiPalette.PiercingHex;
        public const string BludgeoningColor = CombatUiPalette.BludgeoningHex;

        public static string EntityName(string name, Team team)
        {
            string c = team switch
            {
                Team.Player => PlayerColor,
                Team.Enemy => EnemyColor,
                _ => NeutralColor
            };
            return $"<font=\"{AccentFont}\"><size=104%><color={c}><b>{name}</b></color></size></font>";
        }

        public static string Weapon(string name) =>
            $"<font=\"{AccentFont}\"><size=104%><cspace=0.55><b><color={WeaponColor}>{name}</color></b></cspace></size></font>";

        public static string Narrative(string text) =>
            $"<size=104%><cspace=0.12><color={VerbColor}>{text}</color></cspace></size>";

        public static string NarrativeAccent(string text, string colorHex) =>
            $"<size=104%><cspace=0.12><color={colorHex}>{text}</color></cspace></size>";

        public static string StatusToken(string text) =>
            $"<size=104%><cspace=0.12><color={StatusTokenColor}>{text}</color></cspace></size>";

        public static string StatusAppliedSuffix(string statusLabel) =>
            $"{Verb("is now")} {StatusToken(statusLabel)}.";

        public static string StatusRemovedSuffix(string statusLabel) =>
            $"{Verb("is no longer")} {StatusToken(statusLabel)}.";

        public static string Verb(string text) =>
            Narrative(text);

        public static string Secondary(string text) =>
            $"<color={CombatUiPalette.SecondaryNoteHex}>{text}</color>";

        public static string MinorNote(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return $"<size=92%><color={CombatUiPalette.SecondaryNoteHex}>{text}</color></size>";
        }

        public static string Round(string text) =>
            $"<color={RoundColor}>{text}</color>";

        public static string Degree(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => $"<size=110%><color={CritSuccessColor}><b><u>Critical Success!</u></b></color></size>",
                DegreeOfSuccess.Success => $"<size=110%><color={SuccessColor}><b><u>Success!</u></b></color></size>",
                DegreeOfSuccess.Failure => $"<size=110%><color={FailureColor}><b><u>Failure</u></b></color></size>",
                DegreeOfSuccess.CriticalFailure => $"<size=110%><color={CritFailureColor}><b><u>Critical Failure!</u></b></color></size>",
                _ => degree.ToString()
            };
        }

        public static string DegreeShort(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => $"<size=110%><color={CritSuccessColor}><b>Critical!</b></color></size>",
                DegreeOfSuccess.Success => $"<size=110%><color={SuccessColor}><b>Success</b></color></size>",
                DegreeOfSuccess.Failure => $"<size=110%><color={FailureColor}><b>Failure</b></color></size>",
                DegreeOfSuccess.CriticalFailure => $"<size=110%><color={CritFailureColor}><b>Crit Fail!</b></color></size>",
                _ => degree.ToString()
            };
        }

        public static string Damage(int amount) =>
            $"<size=110%><color={CombatUiPalette.TooltipValueHex}><b>{amount}</b></color></size>";

        public static string DamageAmountAndType(int amount, DamageType type)
        {
            return $"<size=108%><cspace=0.08><color={CombatUiPalette.DamageAccentHex}><nobr>{amount} {type}</nobr></color></cspace></size>";
        }

        public static string OutcomeTotal(int total)
        {
            return $"<size=110%><color={CombatUiPalette.TooltipValueHex}><b>{total}</b></color></size>";
        }

        public static string DegreeSummary(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => $"<size=110%><color={CritSuccessColor}><b>Critical Success!</b></color></size>",
                DegreeOfSuccess.Success => $"<size=110%><color={SuccessColor}><b>Success!</b></color></size>",
                DegreeOfSuccess.Failure => $"<size=110%><color={FailureColor}><b>Failure</b></color></size>",
                DegreeOfSuccess.CriticalFailure => $"<size=110%><color={CritFailureColor}><b>Critical Failure!</b></color></size>",
                _ => degree.ToString()
            };
        }

        public static string OutcomeSummary(int total, DegreeOfSuccess degree)
        {
            return $"<font=\"{AccentFont}\"><size=110%><nobr>{total} {TooltipTextBuilder.FormatDegreeLabel(degree)}</nobr></size></font>";
        }

        public static string DegreeColor(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => CritSuccessColor,
                DegreeOfSuccess.Success => SuccessColor,
                DegreeOfSuccess.Failure => FailureColor,
                DegreeOfSuccess.CriticalFailure => CritFailureColor,
                _ => CombatUiPalette.TooltipBodyHex
            };
        }

        public static string DmgType(DamageType type)
        {
            return $"<b><color={GetDamageTypeColor(type)}>{type}</color></b>";
        }

        public static string ConditionGain(string name) =>
            StatusToken(name);

        public static string ConditionLose(string name) =>
            StatusToken(name);

        public static string Defeated() =>
            StatusAppliedSuffix("Dead");

        public static string OpposedWinner(OpposedCheckWinner winner)
        {
            return winner switch
            {
                OpposedCheckWinner.Attacker => $"<color={SuccessColor}><b>Attacker wins</b></color>",
                OpposedCheckWinner.Defender => $"<color={FailureColor}><b>Defender wins</b></color>",
                _ => $"<color={VerbColor}><b>Tie</b></color>"
            };
        }

        public const string ActionDiamondColor = CombatUiPalette.ActionDiamondHex;

        public static string ActionCost(int cost)
        {
            int n = Mathf.Clamp(cost, 1, 3);
            return $"<color={ActionDiamondColor}><b>{new string('>', n)}</b></color>";
        }

        public static string Hp(int before, int after)
        {
            if (after <= 0)
            {
                return $"<size=92%><color={CombatUiPalette.SecondaryNoteHex}>(HP {before}→</color><color={DefeatedColor}><b>{after}</b></color><color={CombatUiPalette.SecondaryNoteHex}>)</color></size>";
            }

            return $"<size=92%><color={CombatUiPalette.SecondaryNoteHex}>(HP {before}→{after})</color></size>";
        }

        private static string GetDamageTypeColor(DamageType type)
        {
            return type switch
            {
                DamageType.Slashing => SlashingColor,
                DamageType.Piercing => PiercingColor,
                DamageType.Bludgeoning => BludgeoningColor,
                _ => CombatUiPalette.TooltipBodyHex
            };
        }
    }
}
