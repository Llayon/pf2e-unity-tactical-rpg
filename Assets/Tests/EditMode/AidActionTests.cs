using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class AidActionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void GetAidTargetFailure_AllyInReach_ReturnsNone()
        {
            using var ctx = new AidActionContext();
            var helper = ctx.Register("Helper", Team.Player, new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var ally = ctx.Register("Ally", Team.Player, new Vector3Int(1, 0, 0), isRanged: false, reachFeet: 5);

            var reason = ctx.Action.GetAidTargetFailure(helper, ally);

            Assert.AreEqual(TargetingFailureReason.None, reason);
        }

        [Test]
        public void GetAidTargetFailure_EnemyTarget_ReturnsWrongTeam()
        {
            using var ctx = new AidActionContext();
            var helper = ctx.Register("Helper", Team.Player, new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var enemy = ctx.Register("Enemy", Team.Enemy, new Vector3Int(1, 0, 0), isRanged: false, reachFeet: 5);

            var reason = ctx.Action.GetAidTargetFailure(helper, enemy);

            Assert.AreEqual(TargetingFailureReason.WrongTeam, reason);
        }

        [Test]
        public void GetAidTargetFailure_AllyOutOfReach_ReturnsOutOfRange()
        {
            using var ctx = new AidActionContext();
            var helper = ctx.Register("Helper", Team.Player, new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var ally = ctx.Register("Ally", Team.Player, new Vector3Int(2, 0, 0), isRanged: false, reachFeet: 5);

            var reason = ctx.Action.GetAidTargetFailure(helper, ally);

            Assert.AreEqual(TargetingFailureReason.OutOfRange, reason);
        }

        [Test]
        public void GetAidTargetFailure_RangedWeapon_StillUsesDefaultFiveFootReach()
        {
            using var ctx = new AidActionContext();
            var helper = ctx.Register("Helper", Team.Player, new Vector3Int(0, 0, 0), isRanged: true, reachFeet: 100);
            var ally = ctx.Register("Ally", Team.Player, new Vector3Int(2, 0, 0), isRanged: false, reachFeet: 5);

            var reason = ctx.Action.GetAidTargetFailure(helper, ally);

            Assert.AreEqual(TargetingFailureReason.OutOfRange, reason);
        }

        [Test]
        public void TryPrepareAid_StoresRecord_AndPublishesLog()
        {
            using var ctx = new AidActionContext();
            var helper = ctx.Register("Helper", Team.Player, new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var ally = ctx.Register("Ally", Team.Player, new Vector3Int(1, 0, 0), isRanged: false, reachFeet: 5);

            var aidService = new AidService();
            int logs = 0;
            CombatLogEntry lastEntry = default;
            ctx.EventBus.OnLogEntry += entry =>
            {
                logs++;
                lastEntry = entry;
            };

            bool prepared = ctx.Action.TryPrepareAid(helper, ally, roundNumber: 1, aidService);

            Assert.IsTrue(prepared);
            Assert.IsTrue(aidService.HasPreparedAidForAlly(ally));
            Assert.AreEqual(1, logs);
            Assert.AreEqual(helper, lastEntry.Actor);
            Assert.AreEqual("prepares Aid for Ally", lastEntry.Message);
            Assert.AreEqual(CombatLogCategory.Turn, lastEntry.Category);
        }

        private sealed class AidActionContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly GameObject root;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject actionGo;
            private readonly List<WeaponDefinition> createdWeapons = new();

            public readonly CombatEventBus EventBus;
            public readonly EntityManager EntityManager;
            public readonly AidAction Action;

            public AidActionContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("AidActionTests_Root");

                eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                SetAutoPropertyBackingField(EntityManager, "Registry", new EntityRegistry());

                actionGo = new GameObject("AidAction");
                actionGo.transform.SetParent(root.transform);
                Action = actionGo.AddComponent<AidAction>();
                SetPrivateField(Action, "entityManager", EntityManager);
                SetPrivateField(Action, "eventBus", EventBus);
                SetPrivateField(Action, "defaultReachFeet", 5);
            }

            public EntityHandle Register(string name, Team team, Vector3Int pos, bool isRanged, int reachFeet)
            {
                var weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
                weaponDef.itemName = $"{name}_Weapon";
                weaponDef.isRanged = isRanged;
                weaponDef.reachFeet = reachFeet;
                createdWeapons.Add(weaponDef);

                var data = new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    Size = CreatureSize.Medium,
                    MaxHP = 10,
                    CurrentHP = 10,
                    GridPosition = pos,
                    EquippedWeapon = new WeaponInstance { def = weaponDef }
                };

                return EntityManager.Registry.Register(data);
            }

            public void Dispose()
            {
                for (int i = 0; i < createdWeapons.Count; i++)
                {
                    if (createdWeapons[i] != null)
                        Object.DestroyImmediate(createdWeapons[i]);
                }

                if (root != null)
                    Object.DestroyImmediate(root);

                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
