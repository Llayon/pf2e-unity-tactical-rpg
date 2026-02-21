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
        public ShieldInstance EquippedShield;

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

        // ─── Derived Stats Cache (Phase 18.6) ───
        private int cachedEffectiveAC;
        private int cachedConditionPenaltyToAttack;
        private bool derivedStatsCacheValid;
        private int snapshotDexterity;
        private int snapshotLevel;
        private ArmorDefinition snapshotArmorDefinition;
        private int snapshotArmorDexCap;
        private int snapshotArmorItemACBonus;
        private ArmorCategory snapshotArmorCategory;
        private int snapshotArmorPotencyBonus;
        private ShieldDefinition snapshotShieldDefinition;
        private bool snapshotShieldIsRaised;
        private int snapshotShieldACBonus;
        private int snapshotConditionsFingerprint;

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
                EnsureDerivedStatsUpToDate();
                return cachedConditionPenaltyToAttack;
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
                EnsureDerivedStatsUpToDate();
                return cachedEffectiveAC;
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

        /// <summary>
        /// Optional invalidation hint.
        /// Correctness does not depend on this call because getters also verify a snapshot.
        /// </summary>
        public void MarkDerivedStatsDirty()
        {
            derivedStatsCacheValid = false;
        }

        private void EnsureDerivedStatsUpToDate()
        {
            if (derivedStatsCacheValid && IsDerivedStatsSnapshotValid())
                return;

            RecomputeDerivedStats();
            CaptureDerivedStatsSnapshot();
            derivedStatsCacheValid = true;
        }

        private bool IsDerivedStatsSnapshotValid()
        {
            if (snapshotDexterity != Dexterity) return false;
            if (snapshotLevel != Level) return false;
            if (snapshotArmorDefinition != EquippedArmor.def) return false;
            if (snapshotArmorDexCap != EquippedArmor.DexCap) return false;
            if (snapshotArmorItemACBonus != EquippedArmor.ItemACBonus) return false;
            if (snapshotArmorCategory != EquippedArmor.Category) return false;
            if (snapshotArmorPotencyBonus != EquippedArmor.potencyBonus) return false;
            if (snapshotShieldDefinition != EquippedShield.def) return false;
            if (snapshotShieldIsRaised != EquippedShield.isRaised) return false;
            if (snapshotShieldACBonus != EquippedShield.ACBonus) return false;
            if (snapshotConditionsFingerprint != ComputeConditionsFingerprint()) return false;
            return true;
        }

        private void CaptureDerivedStatsSnapshot()
        {
            snapshotDexterity = Dexterity;
            snapshotLevel = Level;
            snapshotArmorDefinition = EquippedArmor.def;
            snapshotArmorDexCap = EquippedArmor.DexCap;
            snapshotArmorItemACBonus = EquippedArmor.ItemACBonus;
            snapshotArmorCategory = EquippedArmor.Category;
            snapshotArmorPotencyBonus = EquippedArmor.potencyBonus;
            snapshotShieldDefinition = EquippedShield.def;
            snapshotShieldIsRaised = EquippedShield.isRaised;
            snapshotShieldACBonus = EquippedShield.ACBonus;
            snapshotConditionsFingerprint = ComputeConditionsFingerprint();
        }

        private void RecomputeDerivedStats()
        {
            ConditionRules.ComputeAttackAndAcPenalties(
                Conditions,
                out cachedConditionPenaltyToAttack,
                out int acPenalty);

            int shieldCircumstanceBonus = EquippedShield.ACBonus;
            cachedEffectiveAC = BaseAC + shieldCircumstanceBonus - acPenalty;
        }

        private int ComputeConditionsFingerprint()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < Conditions.Count; i++)
                {
                    var condition = Conditions[i];
                    hash = (hash * 31) + (int)condition.Type;
                    hash = (hash * 31) + condition.Value;
                    hash = (hash * 31) + condition.RemainingRounds;
                }
                return hash;
            }
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
        /// <summary>
        /// Compatibility-only legacy helper.
        /// Runtime/gameplay code should use ConditionService.TickStartTurn.
        /// </summary>
        [System.Obsolete("Compatibility-only. Use ConditionService.TickStartTurn.")]
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

        /// <summary>
        /// Compatibility-only legacy helper.
        /// Runtime/gameplay code should use ConditionService.TickEndTurn.
        /// </summary>
        [System.Obsolete("Compatibility-only. Use ConditionService.TickEndTurn.")]
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

        public void SetShieldRaised(bool raised)
        {
            if (!EquippedShield.IsEquipped) return;
            if (EquippedShield.isRaised == raised) return;

            var shield = EquippedShield;
            shield.isRaised = raised;
            EquippedShield = shield;
            MarkDerivedStatsDirty();
        }

        public void ApplyShieldDamage(int damage)
        {
            if (damage <= 0) return;
            if (!EquippedShield.IsEquipped) return;

            var shield = EquippedShield;
            if (shield.currentHP <= 0) return;

            shield.currentHP = Mathf.Max(0, shield.currentHP - damage);
            if (shield.currentHP <= 0)
                shield.isRaised = false;

            EquippedShield = shield;
            MarkDerivedStatsDirty();
        }
    }
}
