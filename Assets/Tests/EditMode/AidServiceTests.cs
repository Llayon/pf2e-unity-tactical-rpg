using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class AidServiceTests
    {
        [Test]
        public void PrepareAid_OverwritesExistingRecord_ForSameAlly()
        {
            var service = new AidService();
            var helperA = new EntityHandle(1);
            var helperB = new EntityHandle(2);
            var ally = new EntityHandle(3);

            Assert.IsTrue(service.PrepareAid(helperA, ally, preparedRound: 1));
            Assert.IsTrue(service.PrepareAid(helperB, ally, preparedRound: 1));
            Assert.AreEqual(1, service.PreparedAidCount);
            Assert.IsTrue(service.TryGetPreparedAidForAlly(ally, out var record));
            Assert.AreEqual(helperB, record.helper);
        }

        [Test]
        public void NotifyTurnStarted_ExpiresPreparedAid_OnHelpersNextTurnStart()
        {
            var service = new AidService();
            var helper = new EntityHandle(1);
            var ally = new EntityHandle(2);

            service.NotifyTurnStarted(helper); // helper current turn begins
            Assert.IsTrue(service.PrepareAid(helper, ally, preparedRound: 1));
            Assert.IsTrue(service.HasPreparedAidForAlly(ally));

            int expired = service.NotifyTurnStarted(helper); // helper next turn begins

            Assert.AreEqual(1, expired);
            Assert.IsFalse(service.HasPreparedAidForAlly(ally));
        }

        [Test]
        public void TryConsumeAidForSkillCheck_Success_AppliesPlusOne_AndConsumesReaction()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 16);
            helper.AthleticsProf = ProficiencyRank.Trained;
            helper.ReactionAvailable = true;
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForSkill(ally.Handle, SkillType.Athletics, "Trip"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 9 }),
                out var outcome);

            Assert.IsTrue(consumed);
            Assert.AreEqual(DegreeOfSuccess.Success, outcome.degree);
            Assert.AreEqual(1, outcome.appliedModifier);
            Assert.IsTrue(outcome.reactionConsumed);
            Assert.IsFalse(helper.ReactionAvailable);
            Assert.IsFalse(service.HasPreparedAidForAlly(ally.Handle));
        }

        [Test]
        public void TryConsumeAidForSkillCheck_CriticalSuccess_MasterScaling_AppliesPlusThree()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 10);
            helper.AthleticsProf = ProficiencyRank.Master;
            helper.ReactionAvailable = true;
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForSkill(ally.Handle, SkillType.Athletics, "Trip"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 20 }),
                out var outcome);

            Assert.IsTrue(consumed);
            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, outcome.degree);
            Assert.AreEqual(3, outcome.appliedModifier);
        }

        [Test]
        public void TryConsumeAidForSkillCheck_CriticalSuccess_LegendaryScaling_AppliesPlusFour()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 10);
            helper.AthleticsProf = ProficiencyRank.Legendary;
            helper.ReactionAvailable = true;
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForSkill(ally.Handle, SkillType.Athletics, "Trip"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 20 }),
                out var outcome);

            Assert.IsTrue(consumed);
            Assert.AreEqual(DegreeOfSuccess.CriticalSuccess, outcome.degree);
            Assert.AreEqual(4, outcome.appliedModifier);
        }

        [Test]
        public void TryConsumeAidForSkillCheck_CriticalFailure_AppliesMinusOne()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 10);
            helper.AthleticsProf = ProficiencyRank.Untrained;
            helper.ReactionAvailable = true;
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForSkill(ally.Handle, SkillType.Athletics, "Trip"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 1 }),
                out var outcome);

            Assert.IsTrue(consumed);
            Assert.AreEqual(DegreeOfSuccess.CriticalFailure, outcome.degree);
            Assert.AreEqual(-1, outcome.appliedModifier);
        }

        [Test]
        public void TryConsumeAidForStrikeCheck_UsesAttackCheckSource()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 14);
            helper.SimpleWeaponProf = ProficiencyRank.Master;
            helper.ReactionAvailable = true;
            helper.EquippedWeapon = new WeaponInstance { Category = WeaponCategory.Simple };
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForStrike(ally.Handle, "Strike"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 20 }),
                out var outcome);

            Assert.IsTrue(consumed);
            Assert.AreEqual(AidCheckType.Strike, outcome.checkType);
            Assert.AreEqual(CheckSourceType.Custom, outcome.roll.source.type);
            Assert.AreEqual("AID-ATK", outcome.roll.source.customLabel);
            Assert.AreEqual(3, outcome.appliedModifier);
        }

        [Test]
        public void TryConsumeAidForCheck_WhenReactionUnavailable_DoesNotConsume()
        {
            var service = new AidService();
            var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 16);
            helper.AthleticsProf = ProficiencyRank.Trained;
            helper.ReactionAvailable = false;
            helper.Handle = new EntityHandle(1);

            var ally = CreateEntity("Ally", Team.Player, level: 1, strength: 10);
            ally.Handle = new EntityHandle(2);

            var entities = new Dictionary<EntityHandle, EntityData>
            {
                [helper.Handle] = helper,
                [ally.Handle] = ally
            };

            service.NotifyTurnStarted(helper.Handle);
            Assert.IsTrue(service.PrepareAid(helper.Handle, ally.Handle, preparedRound: 1));

            bool consumed = service.TryConsumeAidForCheck(
                AidCheckContext.ForSkill(ally.Handle, SkillType.Athletics, "Trip"),
                getEntity: handle => entities.TryGetValue(handle, out var data) ? data : null,
                canUseReaction: handle => entities.TryGetValue(handle, out var data) && data.ReactionAvailable,
                rng: new FixedRng(new[] { 20 }),
                out _);

            Assert.IsFalse(consumed);
            Assert.IsTrue(service.HasPreparedAidForAlly(ally.Handle));
        }

        [Test]
        public void TurnManager_AidService_ExpiresPreparedAid_OnHelpersNextTurnStart()
        {
            var turnManagerGo = new GameObject("TM_Aid_Expire_TurnManager");
            var entityManagerGo = new GameObject("TM_Aid_Expire_EntityManager");

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 12, wisdom: 5000);
                helper.EncounterActorId = "helper";
                registry.Register(helper);

                var enemy = CreateEntity("Enemy", Team.Enemy, level: 1, strength: 10, wisdom: 10);
                enemy.EncounterActorId = "enemy";
                registry.Register(enemy);

                turnManager.StartCombat();
                Assert.AreEqual(helper.Handle, turnManager.CurrentEntity);

                Assert.IsTrue(turnManager.AidService.PrepareAid(helper.Handle, enemy.Handle, turnManager.RoundNumber));
                Assert.IsTrue(turnManager.AidService.HasPreparedAidForAlly(enemy.Handle));

                turnManager.EndTurn(); // enemy turn
                turnManager.EndTurn(); // next round helper turn start => expire

                Assert.IsFalse(turnManager.AidService.HasPreparedAidForAlly(enemy.Handle));
            }
            finally
            {
                if (turnManagerGo != null)
                    Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null)
                    Object.DestroyImmediate(entityManagerGo);
            }
        }

        [Test]
        public void TurnManager_EndCombat_ClearsAidServiceState()
        {
            var turnManagerGo = new GameObject("TM_Aid_Clear_TurnManager");
            var entityManagerGo = new GameObject("TM_Aid_Clear_EntityManager");

            try
            {
                var turnManager = turnManagerGo.AddComponent<TurnManager>();
                LogAssert.Expect(LogType.Error, "[EntityManager] Missing reference: GridManager. Assign it in Inspector.");
                var entityManager = entityManagerGo.AddComponent<EntityManager>();
                var registry = new EntityRegistry();

                SetPrivateField(turnManager, "entityManager", entityManager);
                SetAutoPropertyBackingField(entityManager, "Registry", registry);

                var helper = CreateEntity("Helper", Team.Player, level: 1, strength: 12, wisdom: 5000);
                helper.EncounterActorId = "helper";
                registry.Register(helper);

                var enemy = CreateEntity("Enemy", Team.Enemy, level: 1, strength: 10, wisdom: 10);
                enemy.EncounterActorId = "enemy";
                registry.Register(enemy);

                turnManager.StartCombat();
                Assert.IsTrue(turnManager.AidService.PrepareAid(helper.Handle, enemy.Handle, turnManager.RoundNumber));
                Assert.AreEqual(1, turnManager.AidService.PreparedAidCount);

                turnManager.EndCombat();

                Assert.AreEqual(0, turnManager.AidService.PreparedAidCount);
            }
            finally
            {
                if (turnManagerGo != null)
                    Object.DestroyImmediate(turnManagerGo);
                if (entityManagerGo != null)
                    Object.DestroyImmediate(entityManagerGo);
            }
        }

        private static EntityData CreateEntity(
            string name,
            Team team,
            int level,
            int strength = 10,
            int wisdom = 10)
        {
            return new EntityData
            {
                Name = name,
                Team = team,
                Level = level,
                Strength = strength,
                Dexterity = 10,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = wisdom,
                Charisma = 10,
                MaxHP = 20,
                CurrentHP = 20,
                Speed = 25,
                EquippedWeapon = new WeaponInstance()
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class FixedRng : IRng
        {
            private readonly Queue<int> d20Queue;

            public FixedRng(IEnumerable<int> d20Rolls)
            {
                d20Queue = d20Rolls != null ? new Queue<int>(d20Rolls) : new Queue<int>();
            }

            public int RollD20()
            {
                if (d20Queue.Count <= 0)
                    return 10;

                return Mathf.Clamp(d20Queue.Dequeue(), 1, 20);
            }

            public int RollDie(int sides)
            {
                if (sides <= 0)
                    return 0;

                return 1;
            }
        }
    }
}
