using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class AITurnTargetLockTests
    {
        [Test]
        public void ResolveTarget_StaysOnLockedTarget_WhileStillValid()
        {
            var actor = CreateEntity("Enemy", Team.Enemy, new Vector3Int(0, 0, 0), hp: 20);
            var primary = CreateEntity("Primary", Team.Player, new Vector3Int(3, 0, 0), hp: 20);
            var alternative = CreateEntity("Alternative", Team.Player, new Vector3Int(5, 0, 0), hp: 1);
            primary.Handle = new EntityHandle(10);
            alternative.Handle = new EntityHandle(11);

            var all = new[] { actor, primary, alternative };
            var lockState = new AITurnTargetLock();

            EntityHandle first = lockState.ResolveTarget(actor, all);
            Assert.AreEqual(primary.Handle, first);

            // Alternative becomes better by distance, but locked target stays valid -> keep same target.
            alternative.GridPosition = new Vector3Int(1, 0, 0);
            EntityHandle second = lockState.ResolveTarget(actor, all);
            Assert.AreEqual(primary.Handle, second);
        }

        [Test]
        public void ResolveTarget_Reacquires_WhenLockedTargetBecomesInvalid()
        {
            var actor = CreateEntity("Enemy", Team.Enemy, new Vector3Int(0, 0, 0), hp: 20);
            var primary = CreateEntity("Primary", Team.Player, new Vector3Int(2, 0, 0), hp: 20);
            var fallback = CreateEntity("Fallback", Team.Player, new Vector3Int(4, 0, 0), hp: 20);
            primary.Handle = new EntityHandle(20);
            fallback.Handle = new EntityHandle(21);

            var all = new[] { actor, primary, fallback };
            var lockState = new AITurnTargetLock();

            EntityHandle first = lockState.ResolveTarget(actor, all);
            Assert.AreEqual(primary.Handle, first);

            primary.CurrentHP = 0; // invalid -> dead
            EntityHandle second = lockState.ResolveTarget(actor, all);
            Assert.AreEqual(fallback.Handle, second);
        }

        [Test]
        public void Reset_ClearsLock_AllowsReacquire()
        {
            var actor = CreateEntity("Enemy", Team.Enemy, new Vector3Int(0, 0, 0), hp: 20);
            var firstTarget = CreateEntity("First", Team.Player, new Vector3Int(2, 0, 0), hp: 20);
            var secondTarget = CreateEntity("Second", Team.Player, new Vector3Int(3, 0, 0), hp: 20);
            firstTarget.Handle = new EntityHandle(30);
            secondTarget.Handle = new EntityHandle(31);

            var all = new[] { actor, firstTarget, secondTarget };
            var lockState = new AITurnTargetLock();

            Assert.AreEqual(firstTarget.Handle, lockState.ResolveTarget(actor, all));

            firstTarget.CurrentHP = 0;
            lockState.Reset();

            Assert.AreEqual(secondTarget.Handle, lockState.ResolveTarget(actor, all));
        }

        private static EntityData CreateEntity(string name, Team team, Vector3Int position, int hp)
        {
            return new EntityData
            {
                Name = name,
                Team = team,
                Handle = EntityHandle.None,
                GridPosition = position,
                MaxHP = Mathf.Max(1, hp),
                CurrentHP = hp,
                Speed = 25,
                Size = CreatureSize.Medium
            };
        }
    }
}
