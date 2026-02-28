using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class InitiativeEntryAdvancedTests
    {
        [Test]
        public void InitiativeEntry_NegativeModifier_TotalStillWorks()
        {
            var entry1 = new InitiativeEntry
            {
                Handle = new EntityHandle(1),
                Roll = new CheckRoll(15, -2, CheckSource.Perception()),
                IsPlayer = false
            };
            var entry2 = new InitiativeEntry
            {
                Handle = new EntityHandle(2),
                Roll = new CheckRoll(15, 3, CheckSource.Perception()),
                IsPlayer = false
            };

            Assert.Greater(entry2.Total, entry1.Total);
            Assert.Less(CheckComparer.CompareInitiative(entry2, entry1), 0);
        }

        [Test]
        public void InitiativeEntry_TotalBeatsHigherNaturalRollWhenModifierDifferenceIsLarger()
        {
            var higherNatural = new InitiativeEntry
            {
                Handle = new EntityHandle(1),
                Roll = new CheckRoll(15, -3, CheckSource.Perception()), // total 12
                IsPlayer = false
            };
            var higherTotal = new InitiativeEntry
            {
                Handle = new EntityHandle(2),
                Roll = new CheckRoll(14, 5, CheckSource.Perception()), // total 19
                IsPlayer = false
            };

            Assert.Less(CheckComparer.CompareInitiative(higherTotal, higherNatural), 0);
        }
    }
}
