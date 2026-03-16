using UnityEngine;

namespace PF2e.Core
{
    public static class SpellcastingRules
    {
        public static int ComputeWizardSpellAttackModifier(EntityData caster)
        {
            if (caster == null)
                return 0;

            const int trainedBaseline = 2;
            int conditionPenalty = ConditionRules.ComputeCheckPenalty(caster.Conditions);
            return Mathf.Max(0, caster.Level) + trainedBaseline + caster.IntMod - conditionPenalty;
        }

        public static int ComputeWizardSpellDc(EntityData caster)
        {
            return 10 + ComputeWizardSpellAttackModifier(caster);
        }
    }
}
