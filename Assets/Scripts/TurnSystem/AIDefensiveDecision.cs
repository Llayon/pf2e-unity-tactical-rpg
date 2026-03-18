using PF2e.Core;

namespace PF2e.TurnSystem
{
    public enum AIDefensiveActionKind
    {
        None = 0,
        RaisePhysicalShield = 1,
        CastShieldSpell = 2
    }

    public readonly struct AIDefensiveDecision
    {
        public readonly AIDefensiveActionKind actionKind;
        public readonly RaiseShieldSpellMode shieldSpellMode;

        public AIDefensiveDecision(AIDefensiveActionKind actionKind, RaiseShieldSpellMode shieldSpellMode = RaiseShieldSpellMode.Standard)
        {
            this.actionKind = actionKind;
            this.shieldSpellMode = shieldSpellMode;
        }

        public bool IsValid => actionKind != AIDefensiveActionKind.None;

        public static AIDefensiveDecision RaisePhysicalShield()
        {
            return new AIDefensiveDecision(AIDefensiveActionKind.RaisePhysicalShield);
        }

        public static AIDefensiveDecision CastShieldSpell(RaiseShieldSpellMode mode)
        {
            return new AIDefensiveDecision(AIDefensiveActionKind.CastShieldSpell, mode);
        }
    }
}
