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

        // ─── Computed: Effective AC ───
        public int EffectiveAC
        {
            get
            {
                int ac = ArmorClass;
                if (HasCondition(ConditionType.FlatFooted)) ac -= 2;
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
        public void AddCondition(ConditionType type, int value = 0, int rounds = -1)
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

        public void RemoveCondition(ConditionType type)
        {
            for (int i = Conditions.Count - 1; i >= 0; i--)
                if (Conditions[i].Type == type)
                    Conditions.RemoveAt(i);
        }

        private void TickCondition(ConditionType type)
        {
            for (int i = Conditions.Count - 1; i >= 0; i--)
            {
                if (Conditions[i].Type == type)
                {
                    if (Conditions[i].TickDown())
                        Conditions.RemoveAt(i);
                }
            }
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
                RemoveCondition(ConditionType.Stunned);
        }

        public void EndTurn()
        {
            TickCondition(ConditionType.Frightened);
        }

        public void SpendActions(int count)
        {
            ActionsRemaining -= count;
            if (ActionsRemaining < 0) ActionsRemaining = 0;
        }
    }
}
