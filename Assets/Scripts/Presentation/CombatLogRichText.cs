using PF2e.Core;

namespace PF2e.Presentation
{
    /// <summary>
    /// TMP rich text helpers for combat log color coding (Solasta 2 palette).
    /// </summary>
    public static class CombatLogRichText
    {
        // Entity names
        public const string PlayerColor = "#7CB8D4";
        public const string EnemyColor = "#E8A050";
        public const string NeutralColor = "#B0B0B0";

        // Elements
        public const string WeaponColor = "#E8DCC0";
        public const string VerbColor = "#B0B0B0";
        public const string RoundColor = "#7A776F";
        public const string ConditionGainColor = "#E8A0A0";
        public const string ConditionLoseColor = "#A0D8D8";
        public const string DefeatedColor = "#E04A4A";
        public const string HealColor = "#4AE04A";

        // Degree of success
        public const string SuccessColor = "#4AE04A";
        public const string FailureColor = "#E04A4A";
        public const string CritSuccessColor = "#FFD700";
        public const string CritFailureColor = "#CC3333";

        // Damage types
        public const string SlashingColor = "#E05050";
        public const string PiercingColor = "#6090D0";
        public const string BludgeoningColor = "#D0D0D0";

        public static string EntityName(string name, Team team)
        {
            string c = team switch
            {
                Team.Player => PlayerColor,
                Team.Enemy => EnemyColor,
                _ => NeutralColor
            };
            return $"<color={c}><b>{name}</b></color>";
        }

        public static string Weapon(string name) =>
            $"<b><color={WeaponColor}>{name}</color></b>";

        public static string Verb(string text) =>
            $"<color={VerbColor}>{text}</color>";

        public static string Round(string text) =>
            $"<color={RoundColor}>{text}</color>";

        public static string Degree(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => $"<color={CritSuccessColor}><b><u>Critical Success!</u></b></color>",
                DegreeOfSuccess.Success => $"<color={SuccessColor}><b><u>Success!</u></b></color>",
                DegreeOfSuccess.Failure => $"<color={FailureColor}><b><u>Failure</u></b></color>",
                DegreeOfSuccess.CriticalFailure => $"<color={CritFailureColor}><b><u>Critical Failure!</u></b></color>",
                _ => degree.ToString()
            };
        }

        public static string DegreeShort(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => $"<color={CritSuccessColor}><b>Critical!</b></color>",
                DegreeOfSuccess.Success => $"<color={SuccessColor}><b>Success</b></color>",
                DegreeOfSuccess.Failure => $"<color={FailureColor}><b>Failure</b></color>",
                DegreeOfSuccess.CriticalFailure => $"<color={CritFailureColor}><b>Crit Fail!</b></color>",
                _ => degree.ToString()
            };
        }

        public static string Damage(int amount) =>
            $"<b>{amount}</b>";

        public static string DmgType(DamageType type)
        {
            string c = type switch
            {
                DamageType.Slashing => SlashingColor,
                DamageType.Piercing => PiercingColor,
                DamageType.Bludgeoning => BludgeoningColor,
                _ => "#FFFFFF"
            };
            return $"<color={c}>{type}</color>";
        }

        public static string ConditionGain(string name) =>
            $"<color={ConditionGainColor}>{name}</color>";

        public static string ConditionLose(string name) =>
            $"<color={ConditionLoseColor}>{name}</color>";

        public static string Defeated(string name) =>
            $"<color={DefeatedColor}><b>{name}</b></color> {Verb("is")} <color={DefeatedColor}><b>defeated</b></color>.";

        public static string OpposedWinner(OpposedCheckWinner winner)
        {
            return winner switch
            {
                OpposedCheckWinner.Attacker => $"<color={SuccessColor}><b>Attacker wins</b></color>",
                OpposedCheckWinner.Defender => $"<color={FailureColor}><b>Defender wins</b></color>",
                _ => $"<color={VerbColor}><b>Tie</b></color>"
            };
        }

        public static string Hp(int before, int after)
        {
            string afterStr = after <= 0
                ? $"<color={DefeatedColor}><b>{after}</b></color>"
                : after.ToString();
            return $"{Verb("(HP")} {before}→{afterStr}{Verb(")")}";
        }
    }
}
