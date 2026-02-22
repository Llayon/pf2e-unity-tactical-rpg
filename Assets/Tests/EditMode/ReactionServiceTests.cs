using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ReactionServiceTests
    {
        [Test]
        public void CollectEligibleReactions_PostHit_TargetWithRaisedShield_IsIncluded()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var source = new EntityHandle(1);
                var target = new EntityHandle(2);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [source] = CreateEntity(reactionAvailable: true, shieldEquipped: false, shieldDef),
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = source, Roll = 14, Modifier = 2, IsPlayer = false },
                    new InitiativeEntry { Handle = target, Roll = 12, Modifier = 3, IsPlayer = true }
                };

                var options = new List<ReactionOption>();
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit,
                    source,
                    target,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    options);

                Assert.AreEqual(1, options.Count);
                Assert.AreEqual(target, options[0].entity);
                Assert.AreEqual(ReactionType.ShieldBlock, options[0].type);
                Assert.AreEqual(ReactionTriggerPhase.PostHit, options[0].phase);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void CollectEligibleReactions_PostHit_FiltersOutInvalidShieldBlockCases()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var source = new EntityHandle(1);
                var target = new EntityHandle(2);

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = target, Roll = 10, Modifier = 0, IsPlayer = true }
                };

                var options = new List<ReactionOption>();

                var noReaction = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: false, shieldEquipped: true, shieldDef, shieldRaised: true)
                };
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit, source, target, initiative,
                    handle => noReaction.TryGetValue(handle, out var data) ? data : null, options);
                Assert.AreEqual(0, options.Count);

                var notRaised = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: false)
                };
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit, source, target, initiative,
                    handle => notRaised.TryGetValue(handle, out var data) ? data : null, options);
                Assert.AreEqual(0, options.Count);

                var broken = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true, shieldBroken: true)
                };
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit, source, target, initiative,
                    handle => broken.TryGetValue(handle, out var data) ? data : null, options);
                Assert.AreEqual(0, options.Count);

                var dead = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true, alive: false)
                };
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit, source, target, initiative,
                    handle => dead.TryGetValue(handle, out var data) ? data : null, options);
                Assert.AreEqual(0, options.Count);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void CollectEligibleReactions_PreHit_ReturnsEmptyForMvp()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var target = new EntityHandle(2);
                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = target, Roll = 10, Modifier = 0, IsPlayer = true }
                };

                var options = new List<ReactionOption>();
                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PreHit,
                    new EntityHandle(1),
                    target,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    options);

                Assert.AreEqual(0, options.Count);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void CollectEligibleReactions_IteratesInitiativeOrder_Directly()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var a = new EntityHandle(10);
                var b = new EntityHandle(11);
                var c = new EntityHandle(12);

                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [a] = CreateEntity(reactionAvailable: true, shieldEquipped: false, shieldDef),
                    [b] = CreateEntity(reactionAvailable: true, shieldEquipped: false, shieldDef),
                    [c] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true)
                };

                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = b, Roll = 20, Modifier = 0, IsPlayer = false },
                    new InitiativeEntry { Handle = a, Roll = 15, Modifier = 0, IsPlayer = false },
                    new InitiativeEntry { Handle = c, Roll = 10, Modifier = 0, IsPlayer = true }
                };

                var visited = new List<EntityHandle>(3);
                var options = new List<ReactionOption>();

                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit,
                    a,
                    c,
                    initiative,
                    handle =>
                    {
                        visited.Add(handle);
                        return entities.TryGetValue(handle, out var data) ? data : null;
                    },
                    options);

                CollectionAssert.AreEqual(new[] { b, a, c }, visited);
                Assert.AreEqual(1, options.Count);
                Assert.AreEqual(c, options[0].entity);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void CollectEligibleReactions_MvpGuard_TruncatesAndWarnsWhenMultipleProduced()
        {
            var shieldDef = CreateShieldDef();
            try
            {
                var target = new EntityHandle(99);
                var entities = new Dictionary<EntityHandle, EntityData>
                {
                    [target] = CreateEntity(reactionAvailable: true, shieldEquipped: true, shieldDef, shieldRaised: true)
                };

                // Duplicate handle simulates bad upstream data and triggers guard path.
                var initiative = new List<InitiativeEntry>
                {
                    new InitiativeEntry { Handle = target, Roll = 20, Modifier = 0, IsPlayer = true },
                    new InitiativeEntry { Handle = target, Roll = 19, Modifier = 0, IsPlayer = true }
                };

                var options = new List<ReactionOption>();
                LogAssert.Expect(LogType.Warning, new Regex(@"\[ReactionService\] More than one eligible reaction found"));

                ReactionService.CollectEligibleReactions(
                    ReactionTriggerPhase.PostHit,
                    new EntityHandle(1),
                    target,
                    initiative,
                    handle => entities.TryGetValue(handle, out var data) ? data : null,
                    options);

                Assert.AreEqual(1, options.Count);
                Assert.AreEqual(target, options[0].entity);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        private static EntityData CreateEntity(
            bool reactionAvailable,
            bool shieldEquipped,
            ShieldDefinition shieldDef,
            bool shieldRaised = false,
            bool shieldBroken = false,
            bool alive = true)
        {
            var shield = default(ShieldInstance);
            if (shieldEquipped && shieldDef != null)
            {
                shield = ShieldInstance.CreateEquipped(shieldDef);
                shield.isRaised = shieldRaised;
                if (shieldBroken) shield.currentHP = 0;
            }

            return new EntityData
            {
                Team = Team.Player,
                Size = CreatureSize.Medium,
                MaxHP = 10,
                CurrentHP = alive ? 10 : 0,
                ReactionAvailable = reactionAvailable,
                EquippedShield = shield
            };
        }

        private static ShieldDefinition CreateShieldDef()
        {
            var def = ScriptableObject.CreateInstance<ShieldDefinition>();
            def.itemName = "TestShield";
            def.acBonus = 2;
            def.hardness = 5;
            def.maxHP = 20;
            return def;
        }
    }
}
