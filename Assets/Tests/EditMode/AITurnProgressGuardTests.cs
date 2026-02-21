using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class AITurnProgressGuardTests
    {
        [Test]
        public void RegisterStep_TriggersAfterTwoNoProgressLoops()
        {
            var guard = new AITurnProgressGuard(2);
            var position = new Vector3Int(1, 0, 1);
            var target = new EntityHandle(10);

            Assert.IsFalse(guard.RegisterStep(position, 3, target)); // baseline snapshot
            Assert.IsFalse(guard.RegisterStep(position, 3, target)); // streak = 1
            Assert.IsTrue(guard.RegisterStep(position, 3, target));  // streak = 2 -> trigger
        }

        [Test]
        public void RegisterStep_AnySnapshotChange_ResetsStreak()
        {
            var guard = new AITurnProgressGuard(2);
            var target = new EntityHandle(10);

            Assert.IsFalse(guard.RegisterStep(new Vector3Int(0, 0, 0), 3, target));
            Assert.IsFalse(guard.RegisterStep(new Vector3Int(0, 0, 0), 3, target)); // streak = 1

            // Changed actions => streak reset.
            Assert.IsFalse(guard.RegisterStep(new Vector3Int(0, 0, 0), 2, target));
            Assert.IsFalse(guard.RegisterStep(new Vector3Int(0, 0, 0), 2, target)); // streak = 1
            Assert.IsTrue(guard.RegisterStep(new Vector3Int(0, 0, 0), 2, target));  // streak = 2
        }

        [Test]
        public void Reset_ClearsProgressState()
        {
            var guard = new AITurnProgressGuard(2);
            var position = new Vector3Int(2, 0, 2);
            var target = new EntityHandle(7);

            Assert.IsFalse(guard.RegisterStep(position, 2, target));
            Assert.IsFalse(guard.RegisterStep(position, 2, target));
            Assert.IsTrue(guard.RegisterStep(position, 2, target));

            guard.Reset();

            Assert.IsFalse(guard.RegisterStep(position, 2, target)); // baseline again
            Assert.IsFalse(guard.RegisterStep(position, 2, target));
            Assert.IsTrue(guard.RegisterStep(position, 2, target));
        }
    }
}
