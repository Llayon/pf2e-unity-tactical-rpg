using System;

namespace PF2e.Core
{
    public readonly struct SpellSliceDefinition
    {
        public readonly SpellId spellId;
        public readonly string displayName;
        public readonly string actionName;
        public readonly int minActionCost;
        public readonly int maxActionCost;
        public readonly int rangeFeet;
        public readonly SpellTargetingKind targetingKind;
        public readonly SpellResolutionKind resolutionKind;
        public readonly DamageType damageType;
        public readonly SaveType? saveType;
        public readonly bool requiresLineOfSight;

        public SpellSliceDefinition(
            SpellId spellId,
            string displayName,
            string actionName,
            int minActionCost,
            int maxActionCost,
            int rangeFeet,
            SpellTargetingKind targetingKind,
            SpellResolutionKind resolutionKind,
            DamageType damageType,
            SaveType? saveType,
            bool requiresLineOfSight)
        {
            this.spellId = spellId;
            this.displayName = displayName;
            this.actionName = actionName;
            this.minActionCost = minActionCost;
            this.maxActionCost = maxActionCost;
            this.rangeFeet = rangeFeet;
            this.targetingKind = targetingKind;
            this.resolutionKind = resolutionKind;
            this.damageType = damageType;
            this.saveType = saveType;
            this.requiresLineOfSight = requiresLineOfSight;
        }
    }

    public static class SpellCatalog
    {
        public static SpellSliceDefinition Get(SpellId spellId)
        {
            return spellId switch
            {
                SpellId.ForceBarrage => new SpellSliceDefinition(
                    SpellId.ForceBarrage,
                    "Force Barrage",
                    "Force Barrage",
                    minActionCost: 1,
                    maxActionCost: 3,
                    rangeFeet: 120,
                    targetingKind: SpellTargetingKind.MultiShardCreature,
                    resolutionKind: SpellResolutionKind.AutoHitDamage,
                    damageType: DamageType.Force,
                    saveType: null,
                    requiresLineOfSight: true),

                SpellId.ElectricArc => new SpellSliceDefinition(
                    SpellId.ElectricArc,
                    "Electric Arc",
                    "Electric Arc",
                    minActionCost: 2,
                    maxActionCost: 2,
                    rangeFeet: 30,
                    targetingKind: SpellTargetingKind.ChainCreature,
                    resolutionKind: SpellResolutionKind.BasicSaveDamage,
                    damageType: DamageType.Electricity,
                    saveType: SaveType.Reflex,
                    requiresLineOfSight: true),

                SpellId.Snowball => new SpellSliceDefinition(
                    SpellId.Snowball,
                    "Snowball",
                    "Snowball",
                    minActionCost: 2,
                    maxActionCost: 2,
                    rangeFeet: 30,
                    targetingKind: SpellTargetingKind.SingleCreature,
                    resolutionKind: SpellResolutionKind.SpellAttackDamage,
                    damageType: DamageType.Cold,
                    saveType: null,
                    requiresLineOfSight: true),

                SpellId.BurningHands => new SpellSliceDefinition(
                    SpellId.BurningHands,
                    "Burning Hands",
                    "Burning Hands",
                    minActionCost: 2,
                    maxActionCost: 2,
                    rangeFeet: 15,
                    targetingKind: SpellTargetingKind.ConeCells,
                    resolutionKind: SpellResolutionKind.BasicSaveAreaDamage,
                    damageType: DamageType.Fire,
                    saveType: SaveType.Reflex,
                    requiresLineOfSight: false),

                SpellId.Fear => new SpellSliceDefinition(
                    SpellId.Fear,
                    "Fear",
                    "Fear",
                    minActionCost: 2,
                    maxActionCost: 2,
                    rangeFeet: 30,
                    targetingKind: SpellTargetingKind.SingleCreature,
                    resolutionKind: SpellResolutionKind.SaveCondition,
                    damageType: DamageType.Force,
                    saveType: SaveType.Will,
                    requiresLineOfSight: true),

                SpellId.Heal => new SpellSliceDefinition(
                    SpellId.Heal,
                    "Heal",
                    "Heal",
                    minActionCost: 1,
                    maxActionCost: 3,
                    rangeFeet: 30,
                    targetingKind: SpellTargetingKind.SingleCreature,
                    resolutionKind: SpellResolutionKind.HealingOrSaveDamage,
                    damageType: DamageType.Vitality,
                    saveType: SaveType.Fortitude,
                    requiresLineOfSight: true),

                SpellId.Harm => new SpellSliceDefinition(
                    SpellId.Harm,
                    "Harm",
                    "Harm",
                    minActionCost: 1,
                    maxActionCost: 3,
                    rangeFeet: 30,
                    targetingKind: SpellTargetingKind.SingleCreature,
                    resolutionKind: SpellResolutionKind.HealingOrSaveDamage,
                    damageType: DamageType.Void,
                    saveType: SaveType.Fortitude,
                    requiresLineOfSight: true),

                _ => throw new ArgumentOutOfRangeException(nameof(spellId), spellId, "Unknown spell slice id.")
            };
        }

        public static bool IsSliceSpellActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return false;

            return string.Equals(actionName, Get(SpellId.ForceBarrage).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.ElectricArc).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.Snowball).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.BurningHands).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.Fear).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.Heal).actionName, StringComparison.Ordinal)
                || string.Equals(actionName, Get(SpellId.Harm).actionName, StringComparison.Ordinal);
        }

        public static string GetShortToken(SpellId spellId, int actionCount = 1)
        {
            return spellId switch
            {
                SpellId.ForceBarrage => $"FBR{Math.Clamp(actionCount, 1, 3)}",
                SpellId.ElectricArc => "ARC",
                SpellId.Snowball => "SNW",
                SpellId.BurningHands => "BRN",
                SpellId.Fear => "FER",
                SpellId.Heal => $"HEL{Math.Clamp(actionCount, 1, 3)}",
                SpellId.Harm => $"HRM{Math.Clamp(actionCount, 1, 3)}",
                _ => "SPL"
            };
        }
    }
}
