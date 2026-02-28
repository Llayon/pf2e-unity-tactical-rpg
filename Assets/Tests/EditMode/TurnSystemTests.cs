using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class TurnSystemTests
    {
        [Test]
        public void TurnState_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)TurnState.Inactive);
            Assert.AreEqual(1, (int)TurnState.RollingInitiative);
            Assert.AreEqual(2, (int)TurnState.PlayerTurn);
            Assert.AreEqual(3, (int)TurnState.ExecutingAction);
            Assert.AreEqual(4, (int)TurnState.EnemyTurn);
            Assert.AreEqual(5, (int)TurnState.CombatOver);
            Assert.AreEqual(6, (int)TurnState.DelayReturnWindow);
        }

        [Test]
        public void InitiativeEntry_Total_UsesNaturalRollPlusModifier()
        {
            var entry = new InitiativeEntry
            {
                Handle = new EntityHandle(1),
                Roll = new CheckRoll(14, 7, CheckSource.Perception()),
                IsPlayer = true
            };

            Assert.AreEqual(21, entry.Total);
        }

        [Test]
        public void CheckComparer_CompareByTotal_HigherTotalWins()
        {
            var high = new CheckRoll(12, 9, CheckSource.Perception()); // 21
            var low = new CheckRoll(18, 0, CheckSource.Perception());  // 18

            int compare = CheckComparer.CompareByTotal(high, low);
            Assert.Less(compare, 0, "Higher total should sort first.");
        }

        [Test]
        public void CheckComparer_InitiativeOrder_TieEnemyBeforePlayer()
        {
            var player = new InitiativeEntry
            {
                Handle = new EntityHandle(1),
                Roll = new CheckRoll(15, 5, CheckSource.Perception()), // total 20
                IsPlayer = true
            };
            var enemy = new InitiativeEntry
            {
                Handle = new EntityHandle(2),
                Roll = new CheckRoll(14, 6, CheckSource.Perception()), // total 20
                IsPlayer = false
            };

            int compare = CheckComparer.CompareInitiative(player, enemy, InitiativeTieBreakPolicy);
            Assert.Greater(compare, 0, "Enemy must act first on equal total.");
        }

        [Test]
        public void InitiativeEntry_Sorting_ProducesExpectedOrder_ByTotalAndTieRules()
        {
            // totals:
            // id 1 (player): 20
            // id 2 (enemy): 20
            // id 3 (enemy): 18
            // id 4 (player): 17
            var entries = new List<InitiativeEntry>
            {
                new InitiativeEntry { Handle = new EntityHandle(4), Roll = new CheckRoll(9, 8, CheckSource.Perception()), IsPlayer = true },
                new InitiativeEntry { Handle = new EntityHandle(3), Roll = new CheckRoll(10, 8, CheckSource.Perception()), IsPlayer = false },
                new InitiativeEntry { Handle = new EntityHandle(1), Roll = new CheckRoll(12, 8, CheckSource.Perception()), IsPlayer = true },
                new InitiativeEntry { Handle = new EntityHandle(2), Roll = new CheckRoll(11, 9, CheckSource.Perception()), IsPlayer = false },
            };

            entries.Sort((a, b) => CheckComparer.CompareInitiative(a, b, InitiativeTieBreakPolicy));

            Assert.AreEqual(2, entries[0].Handle.Id, "enemy should win tie on equal total");
            Assert.AreEqual(1, entries[1].Handle.Id, "player should be after enemy on equal total");
            Assert.AreEqual(3, entries[2].Handle.Id);
            Assert.AreEqual(4, entries[3].Handle.Id);
        }

        [Test]
        public void InitiativeEntry_DefaultHandle_IsNone()
        {
            var entry = new InitiativeEntry();
            Assert.AreEqual(EntityHandle.None, entry.Handle);
        }

        private static int InitiativeTieBreakPolicy(in InitiativeEntry left, in InitiativeEntry right)
        {
            if (left.IsPlayer == right.IsPlayer)
                return 0;

            return left.IsPlayer ? 1 : -1;
        }
    }
}
