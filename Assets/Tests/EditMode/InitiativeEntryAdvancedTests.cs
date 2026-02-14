using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class InitiativeEntryAdvancedTests
    {
        [Test]
        public void InitiativeEntry_NegativeModifier_SortValueStillWorks()
        {
            // Roll=15, Mod=-2 → SortValue = 15000 + (-20) + 0 = 14980
            var entry1 = new InitiativeEntry { Handle = new EntityHandle(1), Roll = 15, Modifier = -2, IsPlayer = false };
            // Roll=15, Mod=3  → SortValue = 15000 + 30 + 0 = 15030
            var entry2 = new InitiativeEntry { Handle = new EntityHandle(2), Roll = 15, Modifier =  3, IsPlayer = false };

            Assert.Greater(entry2.SortValue, entry1.SortValue);
        }

        [Test]
        public void InitiativeEntry_SortValue_RollDominatesModifier()
        {
            // Roll=15, Mod=-3 → SortValue = 15000 + (-30) + 0 = 14970
            var entry1 = new InitiativeEntry { Handle = new EntityHandle(1), Roll = 15, Modifier = -3, IsPlayer = false };
            // Roll=14, Mod=5  → SortValue = 14000 + 50 + 0 = 14050
            var entry2 = new InitiativeEntry { Handle = new EntityHandle(2), Roll = 14, Modifier =  5, IsPlayer = false };

            Assert.Greater(entry1.SortValue, entry2.SortValue);
        }
    }
}
