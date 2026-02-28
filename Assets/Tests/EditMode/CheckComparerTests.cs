using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class CheckComparerTests
    {
        [Test]
        public void CompareByTotal_Descending()
        {
            var a = new CheckRoll(12, 8, CheckSource.Perception()); // 20
            var b = new CheckRoll(10, 5, CheckSource.Perception()); // 15

            Assert.Less(CheckComparer.CompareByTotal(a, b), 0);
            Assert.Greater(CheckComparer.CompareByTotal(b, a), 0);
        }

        [Test]
        public void CompareInitiative_UsesTiePolicyWhenTotalsEqual()
        {
            var player = new InitiativeEntry
            {
                Handle = new EntityHandle(100),
                Roll = new CheckRoll(11, 9, CheckSource.Perception()),
                IsPlayer = true
            };
            var enemy = new InitiativeEntry
            {
                Handle = new EntityHandle(101),
                Roll = new CheckRoll(12, 8, CheckSource.Perception()),
                IsPlayer = false
            };

            int compare = CheckComparer.CompareInitiative(player, enemy, EnemyFirstTiePolicy);
            Assert.Greater(compare, 0);
        }

        [Test]
        public void CompareInitiative_UsesHandleFallbackWhenNoTiePolicyDecision()
        {
            var left = new InitiativeEntry
            {
                Handle = new EntityHandle(2),
                Roll = new CheckRoll(10, 10, CheckSource.Perception()),
                IsPlayer = true
            };
            var right = new InitiativeEntry
            {
                Handle = new EntityHandle(1),
                Roll = new CheckRoll(11, 9, CheckSource.Perception()),
                IsPlayer = true
            };

            int compare = CheckComparer.CompareInitiative(left, right);
            Assert.Greater(compare, 0, "Lower handle id should be ordered first on equal total.");
        }

        private static int EnemyFirstTiePolicy(in InitiativeEntry left, in InitiativeEntry right)
        {
            if (left.IsPlayer == right.IsPlayer)
                return 0;

            return left.IsPlayer ? 1 : -1;
        }
    }
}
