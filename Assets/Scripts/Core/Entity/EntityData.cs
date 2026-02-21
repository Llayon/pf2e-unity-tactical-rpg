using System.Collections.Generic;
using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// All data for a single creature. Pure data class, no MonoBehaviour.
    /// </summary>
    [System.Serializable]
    public class EntityData
    {
        // ─── Identity ───
        public EntityHandle Handle;
        public string Name;
        public CreatureSize Size;
        public Team Team;

        // ─── Grid Position ───
        public Vector3Int GridPosition;
        public int SizeCells => Size.ToSizeCells();

        // ─── Core PF2e Stats ───
        public int Level;
        public int MaxHP;
        public int CurrentHP;
        public int ArmorClass;
        public int Speed;            // in feet (25, 30, etc.)
        public int SpeedCells => Speed / 5;

        // ─── Equipment & Proficiency (Phase 11 UNIFIED) ───
        public WeaponInstance EquippedWeapon;
        public ArmorInstance EquippedArmor;

        public ProficiencyRank SimpleWeaponProf = ProficiencyRank.Trained;
        public ProficiencyRank MartialWeaponProf = ProficiencyRank.Trained;
        public ProficiencyRank AdvancedWeaponProf = ProficiencyRank.Untrained;

        public ProficiencyRank UnarmoredProf = ProficiencyRank.Trained;
        public ProficiencyRank LightArmorProf = ProficiencyRank.Trained;
        public ProficiencyRank MediumArmorProf = ProficiencyRank.Trained;
        public ProficiencyRank HeavyArmorProf = ProficiencyRank.Untrained;

        public int ItemBonusToDamage = 0;

        // ─── Ability Scores ───
        public int Strength;
        public int Dexterity;
        public int Constitution;
        public int Intelligence;
        public int Wisdom;
        public int Charisma;

        // ─── Ability Modifiers ───
        // PF2e: floor((score - 10) / 2)
        // C# integer division truncates toward zero, which is WRONG for odd negatives.
        private static int AbilityMod(int score)
            => Mathf.FloorToInt((score - 10) / 2f);

        public int StrMod => AbilityMod(Strength);
        public int DexMod => AbilityMod(Dexterity);
        public int ConMod => AbilityMod(Constitution);
        public int IntMod => AbilityMod(Intelligence);
        public int WisMod => AbilityMod(Wisdom);
        public int ChaMod => AbilityMod(Charisma);

        // ─── Combat State (per round) ───
        public int ActionsRemaining;
        public int MAPCount;           // 0, 1, 2 — how many Strikes made this turn
        public bool ReactionAvailable;

        // ─── Conditions ───
        public List<ActiveCondition> Conditions = new List<ActiveCondition>();

        // ─── Computed: Multiple Attack Penalty ───
        public int CurrentMAP
        {
            get
            {
                switch (MAPCount)
                {
                    case 0:  return 0;
                    case 1:  return -5;
                    default: return -10;
                }
            }
        }

        // ─── Computed: Proficiency Helpers (Phase 11 UNIFIED) ───
        public int GetProficiencyBonus(ProficiencyRank rank)
        {
            if (rank == ProficiencyRank.Untrained) return 0;

            int add = rank switch
            {
                ProficiencyRank.Trained => 2,
                ProficiencyRank.Expert => 4,
                ProficiencyRank.Master => 6,
                ProficiencyRank.Legendary => 8,
                _ => 2
            };

            return Level + add;
        }

        public ProficiencyRank GetArmorProfRank(ArmorCategory cat)
        {
            return cat switch
            {
                ArmorCategory.Unarmored => UnarmoredProf,
                ArmorCategory.Light => LightArmorProf,
                ArmorCategory.Medium => MediumArmorProf,
                ArmorCategory.Heavy => HeavyArmorProf,
                _ => UnarmoredProf
            };
        }

        // ─── Computed: AC (PF2e correct formula, no double-count) ───
        public int BaseAC
        {
            get
            {
                // PF2e: 10 + min(DexMod, DexCap) + prof bonus + armor item bonus
                int dexApplied = Mathf.Min(DexMod, EquippedArmor.DexCap);
                int prof = GetProficiencyBonus(GetArmorProfRank(EquippedArmor.Category));
                int item = EquippedArmor.ItemACBonus;
                return 10 + dexApplied + prof + item;
            }
        }

        // ─── Computed: Weapon Attack Helpers ───
        public int ConditionPenaltyToAttack
        {
            get
            {
                int penalty = GetConditionValue(ConditionType.Frightened)
                            + GetConditionValue(ConditionType.Sickened);
                if (HasCondition(ConditionType.Prone))
                    penalty += 2;
                return penalty;
            }
        }

        public ProficiencyRank GetWeaponProfRank(WeaponCategory cat)
        {
            return cat switch
            {
                WeaponCategory.Simple => SimpleWeaponProf,
                WeaponCategory.Martial => MartialWeaponProf,
                WeaponCategory.Advanced => AdvancedWeaponProf,
                _ => SimpleWeaponProf
            };
        }

        public int GetAttackAbilityMod(in WeaponInstance weapon)
        {
            if (weapon.IsRanged) return DexMod;
            if ((weapon.Traits & WeaponTraitFlags.Finesse) != 0) return Mathf.Max(StrMod, DexMod);
            return StrMod;
        }

        public int GetAttackBonus(in WeaponInstance weapon)
        {
            int prof = GetProficiencyBonus(GetWeaponProfRank(weapon.Category));
            int potency = weapon.Potency;
            return prof + GetAttackAbilityMod(weapon) + potency - ConditionPenaltyToAttack;
        }

        public int GetMAPPenalty(in WeaponInstance weapon)
        {
            if (MAPCount <= 0) return 0;

            bool agile = (weapon.Traits & WeaponTraitFlags.Agile) != 0;
            if (MAPCount == 1) return agile ? -4 : -5;
            return agile ? -8 : -10;
        }

        public int WeaponDamageBonus
        {
            get
            {
                // MVP: melee uses STR, ranged uses 0 (propulsive/thrown later)
                int ability = EquippedWeapon.IsRanged ? 0 : StrMod;
                return ability + ItemBonusToDamage;
            }
        }

        public int EffectiveAC
        {
            get
            {
                int ac = BaseAC;
                // Off-Guard OR Prone: -2 circumstance to AC (same type = no stacking)
                if (HasCondition(ConditionType.OffGuard) || HasCondition(ConditionType.Prone))
                    ac -= 2;
                ac -= GetConditionValue(ConditionType.Frightened);
                ac -= GetConditionValue(ConditionType.Sickened);
                return ac;
            }
        }

        // ─── State Queries ───
        public bool IsAlive => CurrentHP > 0;

        public bool HasCondition(ConditionType type)
        {
            for (int i = 0; i < Conditions.Count; i++)
                if (Conditions[i].Type == type) return true;
            return false;
        }

        public int GetConditionValue(ConditionType type)
        {
            for (int i = 0; i < Conditions.Count; i++)
                if (Conditions[i].Type == type) return Conditions[i].Value;
            return 0;
        }

        // ─── Condition Management ───
        [System.Obsolete("Use ConditionService for condition mutations.")]
        internal void AddCondition(ConditionType type, int value = 0, int rounds = -1)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i].Type == type)
                {
                    if (value > Conditions[i].Value)
                        Conditions[i].Value = value;
                    return;
                }
            }
            Conditions.Add(new ActiveCondition(type, value, rounds));
        }

        [System.Obsolete("Use ConditionService for condition mutations.")]
        internal void RemoveCondition(ConditionType type)
        {
            for (int i = Conditions.Count - 1; i >= 0; i--)
                if (Conditions[i].Type == type)
                    Conditions.RemoveAt(i);
        }

        // ─── Turn Management ───
        public void StartTurn()
        {
            ActionsRemaining = 3;
            MAPCount = 0;
            ReactionAvailable = true;

            int slowed = GetConditionValue(ConditionType.Slowed);
            ActionsRemaining -= slowed;

            int stunned = GetConditionValue(ConditionType.Stunned);
            ActionsRemaining -= stunned;

            if (ActionsRemaining < 0) ActionsRemaining = 0;

            if (stunned > 0)
            {
                for (int i = Conditions.Count - 1; i >= 0; i--)
                {
                    if (Conditions[i].Type == ConditionType.Stunned)
                        Conditions.RemoveAt(i);
                }
            }
        }

        public void EndTurn(List<ConditionTick> outTicks = null)
        {
            for (int i = Conditions.Count - 1; i >= 0; i--)
            {
                var cond = Conditions[i];
                if (!ConditionRules.AutoDecrementsAtEndOfTurn(cond.Type)) continue;
                if (cond.Value <= 0) continue;

                int oldVal = cond.Value;
                if (cond.TickDown(decrementValue: true, decrementRounds: false))
                {
                    outTicks?.Add(new ConditionTick(cond.Type, oldVal, 0, removed: true));
                    Conditions.RemoveAt(i);
                }
                else
                {
                    outTicks?.Add(new ConditionTick(cond.Type, oldVal, cond.Value, removed: false));
                }
            }
        }

        public void SpendActions(int count)
        {
            ActionsRemaining -= count;
            if (ActionsRemaining < 0) ActionsRemaining = 0;
        }
    }
}
