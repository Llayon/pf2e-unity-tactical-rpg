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

            if (evaluation.IsSuccess)
                return new TargetingHintMessage(TargetingHintTone.Valid, GetValidMessage(mode));

            return new TargetingHintMessage(TargetingHintTone.Invalid, GetInvalidMessage(mode, evaluation.failureReason, strikeIsRanged));
        }

        private static string GetModePrompt(TargetingMode mode, bool strikeIsRanged)
        {
            return mode switch
            {
                TargetingMode.Strike => strikeIsRanged
                    ? "Strike: choose an enemy in range"
                    : "Strike: choose an enemy in reach",
                TargetingMode.Trip => "Trip: choose an enemy in reach",
                TargetingMode.Shove => "Shove: choose an enemy in reach",
                TargetingMode.Grapple => "Grapple: choose an enemy in reach",
                TargetingMode.Reposition => "Reposition: choose an enemy in reach",
                TargetingMode.Demoralize => "Demoralize: choose an enemy within 30 ft",
                TargetingMode.Escape => "Escape: choose the creature grappling you",
                _ => "Choose a target"
            };
        }

        private static string GetValidMessage(TargetingMode mode)
        {
            return mode switch
            {
                TargetingMode.Trip => "Trip: valid target (Athletics vs Reflex DC)",
                TargetingMode.Shove => "Shove: valid target (Athletics vs Fortitude DC)",
                TargetingMode.Grapple => "Grapple: valid target (Athletics vs Fortitude DC)",
                TargetingMode.Reposition => "Reposition: valid target (Athletics vs Fortitude DC)",
                TargetingMode.Demoralize => "Demoralize: valid target (Intimidation vs Will DC)",
                TargetingMode.Escape => "Escape: valid target (best of Athletics/Acrobatics)",
                TargetingMode.Strike => "Strike: valid target",
                _ => "Valid target"
            };
        }

        private static string GetInvalidMessage(TargetingMode mode, TargetingFailureReason reason, bool strikeIsRanged)
        {
            string action = GetActionLabel(mode);

            return reason switch
            {
                TargetingFailureReason.WrongTeam => mode == TargetingMode.Escape
                    ? "Escape: choose the creature grappling you"
                    : $"{action}: choose an enemy",

                TargetingFailureReason.NoGrappleRelation => "Escape: choose the creature grappling you",
                TargetingFailureReason.SelfTarget => $"{action}: cannot target self",
                TargetingFailureReason.NotAlive => $"{action}: target is not alive",
                TargetingFailureReason.OutOfRange => mode == TargetingMode.Demoralize
                    ? "Demoralize: target is out of range (30 ft)"
                    : mode == TargetingMode.Strike
                        ? (strikeIsRanged ? "Strike: target is out of range" : "Strike: target is out of reach")
                        : $"{action}: target is out of reach",
                TargetingFailureReason.NoLineOfSight => mode == TargetingMode.Strike
                    ? "Strike: no line of sight"
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
                TargetingMode.Trip => "Trip",
                TargetingMode.Shove => "Shove",
                TargetingMode.Grapple => "Grapple",
                TargetingMode.Reposition => "Reposition",
                TargetingMode.Escape => "Escape",
                TargetingMode.Demoralize => "Demoralize",
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
