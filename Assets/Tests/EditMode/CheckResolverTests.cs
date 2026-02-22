using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class CheckResolverTests
    {
        [Test]
        public void GetSkillModifier_Trained_CorrectFormula()
        {
            var data = CreateEntity(level: 3, strength: 16);
            data.AthleticsProf = ProficiencyRank.Trained;

            int result = data.GetSkillModifier(SkillType.Athletics);

            Assert.AreEqual(8, result); // +3 STR + (level 3 + trained 2) = +8
        }

        [Test]
        public void GetSkillModifier_Untrained_ReturnsAbilityModOnly()
        {
            var data = CreateEntity(level: 3, strength: 16);
            data.AthleticsProf = ProficiencyRank.Untrained;

            int result = data.GetSkillModifier(SkillType.Athletics);

            Assert.AreEqual(3, result);
        }

        [Test]
        public void GetSkillModifier_Frightened_ReducesModifier()
        {
            var data = CreateEntity(level: 3, strength: 16);
            data.AthleticsProf = ProficiencyRank.Trained;
            data.Conditions.Add(new ActiveCondition(ConditionType.Frightened, value: 2));

            int result = data.GetSkillModifier(SkillType.Athletics);

            Assert.AreEqual(6, result);
        }

        [Test]
        public void GetSaveModifier_Trained_CorrectFormula()
        {
            var data = CreateEntity(level: 3, constitution: 14);
            data.FortitudeProf = ProficiencyRank.Trained;

            int result = data.GetSaveModifier(SaveType.Fortitude);

            Assert.AreEqual(7, result); // +2 CON + (3+2)
        }

        [Test]
        public void GetSaveDC_Is10PlusModifier()
        {
            var data = CreateEntity(level: 3, constitution: 14);
            data.FortitudeProf = ProficiencyRank.Trained;

            int dc = data.GetSaveDC(SaveType.Fortitude);

            Assert.AreEqual(17, dc);
        }

        [Test]
        public void GetSaveDC_Sickened_ReducesDC()
        {
            var data = CreateEntity(level: 3, constitution: 14);
            data.FortitudeProf = ProficiencyRank.Trained;
            data.Conditions.Add(new ActiveCondition(ConditionType.Sickened, value: 1));

            int dc = data.GetSaveDC(SaveType.Fortitude);

            Assert.AreEqual(16, dc);
        }

        [Test]
        public void RollCheck_Success()
        {
            var result = CheckResolver.RollCheck(modifier: 8, dc: 15, rng: new FixedRng(d20Rolls: new[] { 10 }));

            Assert.AreEqual(10, result.naturalRoll);
            Assert.AreEqual(8, result.modifier);
            Assert.AreEqual(18, result.total);
            Assert.AreEqual(15, result.dc);
            Assert.AreEqual(DegreeOfSuccess.Success, result.degree);
        }

        [Test]
        public void RollCheck_Nat20_Upgrades()
        {
            var result = CheckResolver.RollCheck(modifier: -2, dc: 20, rng: new FixedRng(d20Rolls: new[] { 20 }));

            Assert.AreEqual(DegreeOfSuccess.Success, result.degree);
        }

        [Test]
        public void RollCheck_Nat1_Downgrades()
        {
            var result = CheckResolver.RollCheck(modifier: 15, dc: 15, rng: new FixedRng(d20Rolls: new[] { 1 }));

            Assert.AreEqual(DegreeOfSuccess.Failure, result.degree);
        }

        [Test]
        public void RollSkillCheck_UsesCorrectModifier()
        {
            var data = CreateEntity(level: 3, strength: 16);
            data.AthleticsProf = ProficiencyRank.Trained;

            var result = CheckResolver.RollSkillCheck(data, SkillType.Athletics, dc: 20, rng: new FixedRng(d20Rolls: new[] { 11 }));

            Assert.AreEqual(data.GetSkillModifier(SkillType.Athletics), result.modifier);
            Assert.AreEqual(19, result.total);
        }

        [Test]
        public void ComputeCheckPenalty_FrightenedAndSickened_Max()
        {
            var conditions = new List<ActiveCondition>
            {
                new ActiveCondition(ConditionType.Frightened, value: 2),
                new ActiveCondition(ConditionType.Sickened, value: 1)
            };

            int penalty = ConditionRules.ComputeCheckPenalty(conditions);

            Assert.AreEqual(2, penalty);
        }

        [Test]
        public void ComputeCheckPenalty_NoConditions_Zero()
        {
            Assert.AreEqual(0, ConditionRules.ComputeCheckPenalty(null));
            Assert.AreEqual(0, ConditionRules.ComputeCheckPenalty(new List<ActiveCondition>()));
        }

        [Test]
        public void ComputeAttackAndAcPenalties_StillCorrectAfterRefactor()
        {
            var conditions = new List<ActiveCondition>
            {
                new ActiveCondition(ConditionType.Frightened, value: 2),
                new ActiveCondition(ConditionType.Sickened, value: 1),
                new ActiveCondition(ConditionType.Prone),
                new ActiveCondition(ConditionType.OffGuard)
            };

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(4, attackPenalty); // status 2 + prone circumstance 2
            Assert.AreEqual(4, acPenalty);     // status 2 + off-guard/prone circumstance 2
        }

        private static EntityData CreateEntity(
            int level = 1,
            int strength = 10,
            int dexterity = 10,
            int constitution = 10,
            int intelligence = 10,
            int wisdom = 10,
            int charisma = 10)
        {
            return new EntityData
            {
                Level = level,
                Strength = strength,
                Dexterity = dexterity,
                Constitution = constitution,
                Intelligence = intelligence,
                Wisdom = wisdom,
                Charisma = charisma
            };
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20;
            private readonly Queue<int> dice;

            public FixedRng(IEnumerable<int> d20Rolls = null, IEnumerable<int> dieRolls = null)
            {
                d20 = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
                dice = dieRolls != null ? new Queue<int>(dieRolls) : new Queue<int>();
            }

            public int RollD20()
            {
                return d20.Count > 0 ? d20.Dequeue() : 10;
            }

            public int RollDie(int sides)
            {
                if (sides <= 0) return 0;
                int value = dice.Count > 0 ? dice.Dequeue() : 1;
                if (value < 1) value = 1;
                if (value > sides) value = sides;
                return value;
            }
        }
    }
}
