using UnityEngine;

namespace PF2e.Core
{
    public static class SpellcastingRules
    {
        public static int ComputeWizardSpellDc(EntityData caster)
        {
            if (caster == null)
                return 10;

            const int trainedBaseline = 2;
            return 10 + Mathf.Max(0, caster.Level) + trainedBaseline + caster.IntMod;
        }
    }
}
