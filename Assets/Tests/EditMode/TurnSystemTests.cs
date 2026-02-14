using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    public class TurnSystemTests
    {
        // ─── TurnState ───────────────────────────────────────────────────────

        [Test]
        public void TurnState_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)TurnState.Inactive);
            Assert.AreEqual(1, (int)TurnState.RollingInitiative);
            Assert.AreEqual(2, (int)TurnState.PlayerTurn);
            Assert.AreEqual(3, (int)TurnState.ExecutingAction);
            Assert.AreEqual(4, (int)TurnState.EnemyTurn);
            Assert.AreEqual(5, (int)TurnState.CombatOver);
        }

        // ─── InitiativeEntry.SortValue ───────────────────────────────────────

        [Test]
        public void InitiativeEntry_SortValue_HigherRollWins()
        {
            var high = new InitiativeEntry { Handle = new EntityHandle(1), Roll = 18, Modifier = 0, IsPlayer = false };
            var low  = new InitiativeEntry { Handle = new EntityHandle(2), Roll = 12, Modifier = 0, IsPlayer = false };

            Assert.Greater(high.SortValue, low.SortValue);
        }

        [Test]
        public void InitiativeEntry_SortValue_SameRoll_HigherModifierWins()
        {
            var better = new InitiativeEntry { Handle = new EntityHandle(1), Roll = 15, Modifier = 5, IsPlayer = false };
            var worse  = new InitiativeEntry { Handle = new EntityHandle(2), Roll = 15, Modifier = 2, IsPlayer = false };

            Assert.Greater(better.SortValue, worse.SortValue);
        }

        [Test]
        public void InitiativeEntry_SortValue_TieBreak_PlayerBeatsEnemy()
        {
            var player = new InitiativeEntry { Handle = new EntityHandle(1), Roll = 10, Modifier = 3, IsPlayer = true };
            var enemy  = new InitiativeEntry { Handle = new EntityHandle(2), Roll = 10, Modifier = 3, IsPlayer = false };

            Assert.Greater(player.SortValue, enemy.SortValue);
        }

        [Test]
        public void InitiativeEntry_Sorting_ProducesCorrectOrder()
        {
            // SortValues: 20*1000+3*10+1=20031, 18*1000+5*10+0=18050, 18*1000+2*10+1=18021, 12*1000+8*10+0=12080
            // All unique → stable sort not required; expected order: Id 1, 2, 3, 4
            var entries = new List<InitiativeEntry>
            {
                new InitiativeEntry { Handle = new EntityHandle(4), Roll = 12, Modifier = 8,  IsPlayer = false }, // SortValue 12080
                new InitiativeEntry { Handle = new EntityHandle(2), Roll = 18, Modifier = 5,  IsPlayer = false }, // SortValue 18050
                new InitiativeEntry { Handle = new EntityHandle(3), Roll = 18, Modifier = 2,  IsPlayer = true  }, // SortValue 18021
                new InitiativeEntry { Handle = new EntityHandle(1), Roll = 20, Modifier = 3,  IsPlayer = true  }, // SortValue 20031
            };

            entries.Sort((a, b) => b.SortValue.CompareTo(a.SortValue)); // descending

            Assert.AreEqual(1, entries[0].Handle.Id, "first: Roll 20");
            Assert.AreEqual(2, entries[1].Handle.Id, "second: Roll 18 Mod 5");
            Assert.AreEqual(3, entries[2].Handle.Id, "third: Roll 18 Mod 2 player");
            Assert.AreEqual(4, entries[3].Handle.Id, "fourth: Roll 12");
        }

        [Test]
        public void InitiativeEntry_DefaultHandle_IsNone()
        {
            var entry = new InitiativeEntry();

            Assert.AreEqual(EntityHandle.None, entry.Handle);
        }
    }
}
