using System.Collections.Generic;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Presentation
{
    /// <summary>
    /// Maps targeting mode + preview evaluation into player-facing hint text.
    /// TODO: Localize strings when UI localization pipeline is introduced.
    /// </summary>
    public static class TargetingReasonFormatter
    {
        public static TargetingHintMessage ForModeNoHover(TargetingMode mode, bool strikeIsRanged = false)
        {
            if (mode == TargetingMode.None)
                return TargetingHintMessage.Hidden();

            return new TargetingHintMessage(TargetingHintTone.Info, GetModePrompt(mode, strikeIsRanged));
        }

        public static TargetingHintMessage ForPreview(TargetingMode mode, TargetingEvaluationResult evaluation, bool strikeIsRanged = false)
        {
            if (mode == TargetingMode.None)
                return TargetingHintMessage.Hidden();

            if (evaluation.IsSuccess && evaluation.HasWarning)
                return new TargetingHintMessage(TargetingHintTone.Warning, GetWarningMessage(mode, evaluation.warningReason));

            if (evaluation.IsSuccess)
                return new TargetingHintMessage(TargetingHintTone.Valid, GetValidMessage(mode));

            return new TargetingHintMessage(TargetingHintTone.Invalid, GetInvalidMessage(mode, evaluation.failureReason, strikeIsRanged));
        }

        public static TargetingHintMessage ForJumpPreview(in JumpPreviewResult preview, int actionsRemaining)
        {
            if (!preview.isValid)
            {
                string invalidText = preview.failureReason switch
                {
                    JumpFailureReason.InvalidLanding => "Jump: invalid landing cell",
                    JumpFailureReason.MissingRunUp => "Jump: need a 10 ft run-up",
                    JumpFailureReason.Unreachable => "Jump: destination is unreachable",
                    JumpFailureReason.InvalidState => "Jump: action unavailable",
                    _ => "Jump: choose a reachable landing cell"
                };

                return new TargetingHintMessage(TargetingHintTone.Invalid, invalidText);
            }

            if (preview.actionCost > actionsRemaining)
            {
                return new TargetingHintMessage(
                    TargetingHintTone.Invalid,
                    $"Jump: needs {preview.actionCost} action(s), only {actionsRemaining} left");
            }

            return preview.jumpType switch
            {
                JumpType.Leap => new TargetingHintMessage(TargetingHintTone.Valid, $"Jump: Leap [1] ({preview.jumpDistanceFeet} ft)"),
                JumpType.LongJump => new TargetingHintMessage(TargetingHintTone.Valid, $"Jump: Long Jump [2], Athletics vs DC {preview.dc}"),
                JumpType.HighJump => new TargetingHintMessage(TargetingHintTone.Valid, $"Jump: High Jump [2], Athletics vs DC {preview.dc}"),
                _ => new TargetingHintMessage(TargetingHintTone.Valid, "Jump: valid destination")
            };
        }

        private static string GetModePrompt(TargetingMode mode, bool strikeIsRanged)
        {
            return mode switch
            {
                TargetingMode.Strike => strikeIsRanged
                    ? "Strike: choose an enemy in range"
                    : "Strike: choose an enemy in reach",
                TargetingMode.ReadyStrike => "Ready Strike: choose an enemy in reach",
                TargetingMode.Trip => "Trip: choose an enemy in reach",
                TargetingMode.Shove => "Shove: choose an enemy in reach",
                TargetingMode.Grapple => "Grapple: choose an enemy in reach",
                TargetingMode.Reposition => "Reposition: choose an enemy in reach",
                TargetingMode.Demoralize => "Demoralize: choose an enemy within 30 ft",
                TargetingMode.Escape => "Escape: choose the creature grappling you",
                TargetingMode.Aid => "Aid: choose an ally in reach",
                TargetingMode.Jump => "Jump: choose a landing cell",
                TargetingMode.SpellAoE => "Burning Hands: choose a cone direction",
                TargetingMode.ForceBarrage => "Force Barrage: choose a visible creature within 120 ft",
                TargetingMode.ElectricArc => "Electric Arc: choose 1 or 2 visible creatures within 30 ft",
                TargetingMode.Snowball => "Snowball: choose a visible creature within 30 ft",
                TargetingMode.Fear => "Fear: choose a visible creature within 30 ft",
                TargetingMode.HealSingle => "Heal: choose self, ally, or undead creature",
                TargetingMode.HarmSingle => "Harm: choose a living enemy or undead creature",
                _ => "Choose a target"
            };
        }

        private static string GetValidMessage(TargetingMode mode)
        {
            return mode switch
            {
                TargetingMode.Trip => $"Trip: valid target ({RollBreakdownFormatter.FormatCheckVsDcLabel(CheckSource.Skill(SkillType.Athletics), CheckSource.Save(SaveType.Reflex))})",
                TargetingMode.Shove => $"Shove: valid target ({RollBreakdownFormatter.FormatCheckVsDcLabel(CheckSource.Skill(SkillType.Athletics), CheckSource.Save(SaveType.Fortitude))})",
                TargetingMode.Grapple => $"Grapple: valid target ({RollBreakdownFormatter.FormatCheckVsDcLabel(CheckSource.Skill(SkillType.Athletics), CheckSource.Save(SaveType.Fortitude))})",
                TargetingMode.Reposition => $"Reposition: valid target ({RollBreakdownFormatter.FormatCheckVsDcLabel(CheckSource.Skill(SkillType.Athletics), CheckSource.Save(SaveType.Fortitude))})",
                TargetingMode.Demoralize => $"Demoralize: valid target ({RollBreakdownFormatter.FormatCheckVsDcLabel(CheckSource.Skill(SkillType.Intimidation), CheckSource.Save(SaveType.Will))})",
                TargetingMode.Escape => "Escape: valid target (best of Athletics/Acrobatics)",
                TargetingMode.Strike => "Strike: valid target",
                TargetingMode.Aid => "Aid: valid ally target",
                TargetingMode.Jump => "Jump: valid destination",
                TargetingMode.ReadyStrike => "Ready Strike: valid target",
                TargetingMode.ForceBarrage => "Force Barrage: valid target (auto-hit force shard)",
                TargetingMode.ElectricArc => "Electric Arc: valid target (basic Reflex)",
                TargetingMode.Snowball => "Snowball: valid target (spell attack vs AC)",
                TargetingMode.Fear => "Fear: valid target (Will save)",
                TargetingMode.HealSingle => "Heal: valid target (living heal / undead basic Fortitude)",
                TargetingMode.HarmSingle => "Harm: valid target (living basic Fortitude / undead healing)",
                _ => "Valid target"
            };
        }

        private static string GetWarningMessage(TargetingMode mode, TargetingWarningReason warning)
        {
            if (mode == TargetingMode.Strike && TryBuildStrikeWarningMessage(warning, out string strikeWarningMessage))
                return strikeWarningMessage;

            return warning switch
            {
                TargetingWarningReason.ConcealmentFlatCheck when mode == TargetingMode.Strike
                    => "Strike: valid target (concealed: DC 5 flat check)",
                TargetingWarningReason.ConcealmentFlatCheck
                    => "Valid target (concealed: DC 5 flat check)",
                TargetingWarningReason.CoverAcBonus when mode == TargetingMode.Strike
                    => "Strike: valid target (cover: +2 AC)",
                TargetingWarningReason.CoverAcBonus
                    => "Valid target (cover: +2 AC)",
                _ => GetValidMessage(mode)
            };
        }

        private static bool TryBuildStrikeWarningMessage(TargetingWarningReason warning, out string message)
        {
            var parts = new List<string>(2);

            if ((warning & TargetingWarningReason.CoverAcBonus) != 0)
                parts.Add("cover: +2 AC");

            if ((warning & TargetingWarningReason.ConcealmentFlatCheck) != 0)
                parts.Add("concealed: DC 5 flat check");

            if (parts.Count == 0)
            {
                message = null;
                return false;
            }

            message = $"Strike: valid target ({string.Join("; ", parts)})";
            return true;
        }

        private static string GetInvalidMessage(TargetingMode mode, TargetingFailureReason reason, bool strikeIsRanged)
        {
            string action = GetActionLabel(mode);

            return reason switch
            {
                TargetingFailureReason.WrongTeam => mode == TargetingMode.Escape
                    ? "Escape: choose the creature grappling you"
                    : mode == TargetingMode.Aid
                        ? "Aid: choose an ally"
                    : mode == TargetingMode.HealSingle
                        ? "Heal: choose self, an ally, or an undead creature"
                    : mode == TargetingMode.HarmSingle
                        ? "Harm: choose a living enemy or undead creature"
                    : $"{action}: choose an enemy",

                TargetingFailureReason.NoGrappleRelation => "Escape: choose the creature grappling you",
                TargetingFailureReason.SelfTarget => $"{action}: cannot target self",
                TargetingFailureReason.NotAlive => $"{action}: target is not alive",
                TargetingFailureReason.OutOfRange => mode == TargetingMode.Demoralize
                    ? "Demoralize: target is out of range (30 ft)"
                    : mode == TargetingMode.ForceBarrage
                        ? "Force Barrage: target is out of range (120 ft)"
                    : mode == TargetingMode.ElectricArc
                        ? "Electric Arc: target is out of range (30 ft)"
                    : mode == TargetingMode.Snowball
                        ? "Snowball: target is out of range (30 ft)"
                    : mode == TargetingMode.Fear
                        ? "Fear: target is out of range (30 ft)"
                    : mode == TargetingMode.HealSingle
                        ? "Heal: target is out of range"
                    : mode == TargetingMode.HarmSingle
                        ? "Harm: target is out of range"
                    : mode == TargetingMode.Aid
                        ? "Aid: ally is out of reach"
                    : mode == TargetingMode.Strike
                        ? (strikeIsRanged ? "Strike: target is out of range" : "Strike: target is out of reach")
                        : $"{action}: target is out of reach",
                TargetingFailureReason.NoLineOfSight => mode == TargetingMode.Strike
                    ? "Strike: no line of sight"
                    : mode == TargetingMode.ForceBarrage
                        ? "Force Barrage: target is not visible"
                    : mode == TargetingMode.ElectricArc
                        ? "Electric Arc: target is not visible"
                    : mode == TargetingMode.Snowball
                        ? "Snowball: target is not visible"
                    : mode == TargetingMode.Fear
                        ? "Fear: target is not visible"
                    : mode == TargetingMode.HealSingle
                        ? "Heal: target is not visible"
                    : mode == TargetingMode.HarmSingle
                        ? "Harm: target is not visible"
                    : $"{action}: no line of sight",
                TargetingFailureReason.WrongElevation => $"{action}: target is on a different elevation",
                TargetingFailureReason.TargetTooLarge => $"{action}: target is too large",
                TargetingFailureReason.RequiresMeleeWeapon => $"{action}: requires a melee weapon",
                TargetingFailureReason.MissingRequiredWeaponTrait => $"{action}: weapon lacks {GetRequiredTraitName(mode)} trait",
                TargetingFailureReason.InvalidState => $"{action}: action unavailable",
                TargetingFailureReason.ModeNotSupported => "Targeting mode not supported",
                TargetingFailureReason.InvalidTarget => $"{action}: invalid target",
                TargetingFailureReason.None => GetValidMessage(mode),
                _ => $"{action}: invalid target"
            };
        }

        private static string GetActionLabel(TargetingMode mode)
        {
            return mode switch
            {
                TargetingMode.Strike => "Strike",
                TargetingMode.ReadyStrike => "Ready Strike",
                TargetingMode.Trip => "Trip",
                TargetingMode.Shove => "Shove",
                TargetingMode.Grapple => "Grapple",
                TargetingMode.Reposition => "Reposition",
                TargetingMode.Escape => "Escape",
                TargetingMode.Demoralize => "Demoralize",
                TargetingMode.Aid => "Aid",
                TargetingMode.Jump => "Jump",
                TargetingMode.SpellAoE => "Burning Hands",
                TargetingMode.ForceBarrage => "Force Barrage",
                TargetingMode.ElectricArc => "Electric Arc",
                TargetingMode.Snowball => "Snowball",
                TargetingMode.Fear => "Fear",
                TargetingMode.HealSingle => "Heal",
                TargetingMode.HarmSingle => "Harm",
                _ => "Action"
            };
        }

        private static string GetRequiredTraitName(TargetingMode mode)
        {
            return mode switch
            {
                TargetingMode.Trip => "Trip",
                TargetingMode.Shove => "Shove",
                TargetingMode.Grapple => "Grapple",
                TargetingMode.Reposition => "Reposition",
                _ => "required"
            };
        }
    }
}
