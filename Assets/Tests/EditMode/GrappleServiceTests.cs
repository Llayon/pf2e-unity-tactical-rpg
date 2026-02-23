using System.Collections.Generic;
using NUnit.Framework;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class GrappleServiceTests
    {
        [Test]
        public void ApplyOrRefresh_SetsRelation_AndGrabbed()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);

            Assert.IsTrue(service.HasExactRelation(grappler, target));
            Assert.IsTrue(registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Restrained));
            Assert.IsTrue(service.TryGetRelationByGrappler(grappler, out var relation));
            Assert.AreEqual(GrappleHoldState.Grabbed, relation.holdState);
            Assert.AreEqual(2, relation.turnEndsUntilExpire);
        }

        [Test]
        public void ApplyOrRefresh_CritUpgradesToRestrained_AndRemovesGrabbed()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            deltas.Clear();
            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Restrained, registry, deltas);

            Assert.IsTrue(service.HasExactRelation(grappler, target));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsTrue(registry.Get(target).HasCondition(ConditionType.Restrained));
            Assert.IsTrue(service.TryGetRelationByTarget(target, out var relation));
            Assert.AreEqual(GrappleHoldState.Restrained, relation.holdState);
            Assert.AreEqual(2, relation.turnEndsUntilExpire);
        }

        [Test]
        public void ApplyOrRefresh_RefreshesExpiryCountdownToTwo()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            service.OnTurnEnded(grappler, registry, deltas);
            Assert.IsTrue(service.TryGetRelationByGrappler(grappler, out var beforeRefresh));
            Assert.AreEqual(1, beforeRefresh.turnEndsUntilExpire);

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            Assert.IsTrue(service.TryGetRelationByGrappler(grappler, out var afterRefresh));
            Assert.AreEqual(2, afterRefresh.turnEndsUntilExpire);
        }

        [Test]
        public void OnTurnEnded_GrapplerSecondEnd_ReleasesRelationAndConditions()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            deltas.Clear();

            service.OnTurnEnded(grappler, registry, deltas);
            Assert.IsTrue(service.HasExactRelation(grappler, target));
            Assert.IsTrue(registry.Get(target).HasCondition(ConditionType.Grabbed));

            deltas.Clear();
            service.OnTurnEnded(grappler, registry, deltas);

            Assert.IsFalse(service.HasExactRelation(grappler, target));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Restrained));
        }

        [Test]
        public void OnEntityMoved_Grappler_ReleasesRelation()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            deltas.Clear();

            service.OnEntityMoved(grappler, registry, deltas);

            Assert.IsFalse(service.HasExactRelation(grappler, target));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Grabbed));
        }

        [Test]
        public void OnEntityMoved_Target_DoesNotRelease_MvpRule()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Grabbed, registry, deltas);
            deltas.Clear();

            service.OnEntityMoved(target, registry, deltas);

            Assert.IsTrue(service.HasExactRelation(grappler, target));
            Assert.IsTrue(registry.Get(target).HasCondition(ConditionType.Grabbed));
        }

        [Test]
        public void ReleaseExact_NonMatchingExpectedTarget_DoesNothing()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var targetA = Register(registry, "TargetA");
            var targetB = Register(registry, "TargetB");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(targetA), GrappleHoldState.Grabbed, registry, deltas);
            deltas.Clear();

            bool released = service.ReleaseExact(grappler, registry, deltas, expectedTarget: targetB);

            Assert.IsFalse(released);
            Assert.IsTrue(service.HasExactRelation(grappler, targetA));
            Assert.IsTrue(registry.Get(targetA).HasCondition(ConditionType.Grabbed));
        }

        [Test]
        public void ClearAll_RemovesRelationsAndConditions()
        {
            var service = new GrappleService();
            var registry = new EntityRegistry();
            var deltas = new List<ConditionDelta>();
            var grappler = Register(registry, "Grappler");
            var target = Register(registry, "Target");

            service.ApplyOrRefresh(registry.Get(grappler), registry.Get(target), GrappleHoldState.Restrained, registry, deltas);
            deltas.Clear();

            service.ClearAll(registry, deltas);

            Assert.AreEqual(0, service.ActiveRelationCount);
            Assert.IsFalse(service.HasExactRelation(grappler, target));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Grabbed));
            Assert.IsFalse(registry.Get(target).HasCondition(ConditionType.Restrained));
        }

        private static EntityHandle Register(EntityRegistry registry, string name)
        {
            return registry.Register(new EntityData
            {
                Name = name,
                Team = Team.Enemy,
                Size = CreatureSize.Medium,
                Level = 1,
                MaxHP = 10,
                CurrentHP = 10,
                Speed = 25,
                Strength = 10,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10
            });
        }
    }
}
