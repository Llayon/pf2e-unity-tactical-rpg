namespace PF2e.Core
{
    public static class SkillRules
    {
        public static int GetKeyAbilityMod(EntityData entity, SkillType skill)
        {
            if (entity == null) return 0;

            return skill switch
            {
                SkillType.Acrobatics => entity.DexMod,
                SkillType.Arcana => entity.IntMod,
                SkillType.Athletics => entity.StrMod,
                SkillType.Crafting => entity.IntMod,
                SkillType.Deception => entity.ChaMod,
                SkillType.Diplomacy => entity.ChaMod,
                SkillType.Intimidation => entity.ChaMod,
                SkillType.Lore => entity.IntMod,
                SkillType.Medicine => entity.WisMod,
                SkillType.Nature => entity.WisMod,
                SkillType.Occultism => entity.IntMod,
                SkillType.Performance => entity.ChaMod,
                SkillType.Religion => entity.WisMod,
                SkillType.Society => entity.IntMod,
                SkillType.Stealth => entity.DexMod,
                SkillType.Survival => entity.WisMod,
                SkillType.Thievery => entity.DexMod,
                _ => 0
            };
        }

        public static int GetSaveAbilityMod(EntityData entity, SaveType save)
        {
            if (entity == null) return 0;

            return save switch
            {
                SaveType.Fortitude => entity.ConMod,
                SaveType.Reflex => entity.DexMod,
                SaveType.Will => entity.WisMod,
                _ => 0
            };
        }
    }
}
