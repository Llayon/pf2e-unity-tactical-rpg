using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class ConditionRulesStackingTests
    {
        [Test]
        public void ComputeAttackAndAcPenalties_Frightened2_Sickened1_UsesMaxStatus()
        {
            var conditions = Build(
                new ActiveCondition(ConditionType.Frightened, value: 2, remainingRounds: -1),
                new ActiveCondition(ConditionType.Sickened, value: 1, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(2, attackPenalty);
            Assert.AreEqual(2, acPenalty);
        }

        [Test]
        public void ComputeAttackAndAcPenalties_Frightened1_Sickened3_UsesHigherStatus()
        {
            var conditions = Build(
                new ActiveCondition(ConditionType.Frightened, value: 1, remainingRounds: -1),
                new ActiveCondition(ConditionType.Sickened, value: 3, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(3, attackPenalty);
            Assert.AreEqual(3, acPenalty);
        }

        [Test]
        public void ComputeAttackAndAcPenalties_ProneAndOffGuard_DoesNotDoubleAcCircumstance()
        {
            var conditions = Build(
                new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1),
                new ActiveCondition(ConditionType.OffGuard, value: 0, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(2, attackPenalty);
            Assert.AreEqual(2, acPenalty);
        }

        [Test]
        public void ComputeAttackAndAcPenalties_ProneOnly_ImpliedOffGuardForAc()
        {
            var conditions = Build(new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(2, attackPenalty);
            Assert.AreEqual(2, acPenalty);
        }

        [Test]
        public void ComputeAttackAndAcPenalties_ProneWithStatuses_StacksTypesAndUsesMaxStatus()
        {
            var conditions = Build(
                new ActiveCondition(ConditionType.Prone, value: 0, remainingRounds: -1),
                new ActiveCondition(ConditionType.Frightened, value: 2, remainingRounds: -1),
                new ActiveCondition(ConditionType.Sickened, value: 3, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(5, attackPenalty);
            Assert.AreEqual(5, acPenalty);
        }

        [Test]
        public void ComputeAttackAndAcPenalties_OffGuardWithStatuses_NoAttackCircumstance()
        {
            var conditions = Build(
                new ActiveCondition(ConditionType.OffGuard, value: 0, remainingRounds: -1),
                new ActiveCondition(ConditionType.Frightened, value: 2, remainingRounds: -1),
                new ActiveCondition(ConditionType.Sickened, value: 3, remainingRounds: -1));

            ConditionRules.ComputeAttackAndAcPenalties(conditions, out int attackPenalty, out int acPenalty);

            Assert.AreEqual(3, attackPenalty);
            Assert.AreEqual(5, acPenalty);
        }

        private static List<ActiveCondition> Build(params ActiveCondition[] conditions)
        {
            return new List<ActiveCondition>(conditions);
        }
    }
}
