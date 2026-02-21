using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class DerivedStatsCacheTests
    {
        [Test]
        public void EffectiveAC_DirectDexterityWrite_RecomputesViaSnapshot()
        {
            var data = CreateEntity();

            Assert.AreEqual(18, data.EffectiveAC);
            data.Dexterity = 18;

            Assert.AreEqual(19, data.EffectiveAC);
        }

        [Test]
        public void EffectiveAC_DirectLevelWrite_RecomputesViaSnapshot()
        {
            var data = CreateEntity();

            Assert.AreEqual(18, data.EffectiveAC);
            data.Level = 5;

            Assert.AreEqual(20, data.EffectiveAC);
        }

        [Test]
        public void EffectiveAC_DirectArmorMutation_RecomputesViaSnapshot()
        {
            var data = CreateEntity();

            Assert.AreEqual(18, data.EffectiveAC);
            data.EquippedArmor.potencyBonus = 1;

            Assert.AreEqual(19, data.EffectiveAC);
        }

        [Test]
        public void ConditionPenalty_ConditionServiceMutations_KeepCacheConsistent()
        {
            var data = CreateEntity();
            var service = new ConditionService();
            var deltas = new List<ConditionDelta>(4);

            Assert.AreEqual(0, data.ConditionPenaltyToAttack);

            service.Apply(data, ConditionType.Frightened, value: 2, rounds: -1, deltas);
            Assert.AreEqual(2, data.ConditionPenaltyToAttack);

            deltas.Clear();
            service.TickEndTurn(data, deltas);
            Assert.AreEqual(1, data.ConditionPenaltyToAttack);

            deltas.Clear();
            service.Remove(data, ConditionType.Frightened, deltas);
            Assert.AreEqual(0, data.ConditionPenaltyToAttack);
        }

        [Test]
        public void DerivedStats_UseStackingRules_MaxStatusAndCircumstance()
        {
            var data = CreateEntity();
            var service = new ConditionService();
            var deltas = new List<ConditionDelta>(4);

            service.Apply(data, ConditionType.Frightened, value: 2, rounds: -1, deltas);
            service.Apply(data, ConditionType.Sickened, value: 3, rounds: -1, deltas);
            service.Apply(data, ConditionType.Prone, value: 0, rounds: -1, deltas);

            Assert.AreEqual(5, data.ConditionPenaltyToAttack, "Max status (3) + prone circumstance (2).");
            Assert.AreEqual(13, data.EffectiveAC, "Base 18 - max status 3 - circumstance 2.");
        }

        [Test]
        public void RepeatedReads_NoMutation_StableValues()
        {
            var data = CreateEntity();
            var service = new ConditionService();
            var deltas = new List<ConditionDelta>(2);
            service.Apply(data, ConditionType.Frightened, value: 1, rounds: -1, deltas);

            int acFirst = data.EffectiveAC;
            int acSecond = data.EffectiveAC;
            int penaltyFirst = data.ConditionPenaltyToAttack;
            int penaltySecond = data.ConditionPenaltyToAttack;

            Assert.AreEqual(acFirst, acSecond);
            Assert.AreEqual(penaltyFirst, penaltySecond);
        }

        [Test]
        public void ConditionsFingerprint_Changes_WhenRemainingRoundsChanges()
        {
            var data = CreateEntity();
            var service = new ConditionService();
            var deltas = new List<ConditionDelta>(2);
            service.Apply(data, ConditionType.Slowed, value: 1, rounds: 3, deltas);

            int hashBefore = InvokeConditionsFingerprint(data);
            data.Conditions[0].RemainingRounds = 2;
            int hashAfter = InvokeConditionsFingerprint(data);

            Assert.AreNotEqual(hashBefore, hashAfter);
        }

        private static int InvokeConditionsFingerprint(EntityData data)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var method = typeof(EntityData).GetMethod("ComputeConditionsFingerprint", flags);
            Assert.IsNotNull(method, "Missing ComputeConditionsFingerprint method.");
            return (int)method.Invoke(data, null);
        }

        private static EntityData CreateEntity()
        {
            return new EntityData
            {
                Handle = new EntityHandle(1500),
                Name = "DerivedStatsActor",
                Team = Team.Player,
                Level = 3,
                Dexterity = 16,
                Strength = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10
            };
        }
    }
}
