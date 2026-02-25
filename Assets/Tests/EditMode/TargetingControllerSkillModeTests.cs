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
    public class TargetingControllerSkillModeTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TripMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            EntityHandle confirmed = EntityHandle.None;
            ctx.Controller.BeginTargeting(TargetingMode.Trip, h => { calls++; confirmed = h; });

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(enemy, confirmed);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void DemoralizeMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Demoralize, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void ShoveMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Shove, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

[Test]
        public void GrappleMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Grapple, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void EscapeMode_EnemyClick_InvokesCallback_AndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Escape, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void PreviewEntity_TripMode_MatchesConfirmValidation_ForAllyAndEnemy()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int callbacks = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Trip, _ => callbacks++);

            var previewAlly = ctx.Controller.PreviewEntity(ally);
            var previewEnemy = ctx.Controller.PreviewEntity(enemy);

            Assert.AreEqual(TargetingResult.WrongTeam, previewAlly);
            Assert.AreEqual(TargetingResult.Success, previewEnemy);
            Assert.AreEqual(0, callbacks, "Preview must not invoke callbacks.");
            Assert.AreEqual(TargetingMode.Trip, ctx.Controller.ActiveMode, "Preview must not mutate targeting mode.");

            var confirmAlly = ctx.Controller.TryConfirmEntity(ally);
            Assert.AreEqual(previewAlly, confirmAlly);
            Assert.AreEqual(TargetingMode.Trip, ctx.Controller.ActiveMode, "Invalid confirm keeps mode active.");
        }

        [Test]
        public void PreviewEntity_DoesNotInvokeCallbacks_OrClearMode_OnSuccess()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int callbacks = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Demoralize, _ => callbacks++);

            var preview = ctx.Controller.PreviewEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, preview);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(TargetingMode.Demoralize, ctx.Controller.ActiveMode);
        }

        [Test]
        public void PreviewEntityDetailed_ReturnsFailureReason_AndDoesNotMutateMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);

            ctx.Controller.BeginTargeting(TargetingMode.Trip);

            var detailed = ctx.Controller.PreviewEntityDetailed(ally);

            Assert.AreEqual(TargetingResult.WrongTeam, detailed.result);
            Assert.AreEqual(TargetingFailureReason.WrongTeam, detailed.failureReason);
            Assert.AreEqual(TargetingMode.Trip, ctx.Controller.ActiveMode);
        }

        [Test]
        public void PreviewEntityDetailed_StrikeMode_RangedConcealedTarget_ReturnsSuccessWithConcealmentWarning()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Archer", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);
            ctx.Registry.Get(actor).EquippedWeapon = new WeaponInstance { def = bow };
            ctx.Registry.Get(enemy).Conditions.Add(new ActiveCondition(ConditionType.Concealed));

            ctx.Controller.BeginTargeting(TargetingMode.Strike);

            var detailed = ctx.Controller.PreviewEntityDetailed(enemy);

            Assert.AreEqual(TargetingResult.Success, detailed.result);
            Assert.AreEqual(TargetingFailureReason.None, detailed.failureReason);
            Assert.IsTrue(detailed.HasWarning);
            Assert.AreEqual(TargetingWarningReason.ConcealmentFlatCheck, detailed.warningReason);
            Assert.AreEqual(TargetingMode.Strike, ctx.Controller.ActiveMode);
        }

        [Test]
        public void PreviewEntityDetailed_StrikeMode_MeleeConcealedTarget_ReturnsSuccessWithoutWarning()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            var sword = ctx.CreateWeaponDef(isRanged: false, reachFeet: 5);
            ctx.Registry.Get(actor).EquippedWeapon = new WeaponInstance { def = sword };
            ctx.Registry.Get(enemy).Conditions.Add(new ActiveCondition(ConditionType.Concealed));

            ctx.Controller.BeginTargeting(TargetingMode.Strike);

            var detailed = ctx.Controller.PreviewEntityDetailed(enemy);

            Assert.AreEqual(TargetingResult.Success, detailed.result);
            Assert.AreEqual(TargetingFailureReason.None, detailed.failureReason);
            Assert.IsFalse(detailed.HasWarning);
            Assert.AreEqual(TargetingWarningReason.None, detailed.warningReason);
        }

        [Test]
        public void PreviewEntityDetailed_ModeNone_ReturnsModeNotSupported()
        {
            using var ctx = new TargetingSkillModeContext();
            var target = ctx.RegisterEntity("Goblin", Team.Enemy);

            var detailed = ctx.Controller.PreviewEntityDetailed(target);

            Assert.AreEqual(TargetingResult.ModeNotSupported, detailed.result);
            Assert.AreEqual(TargetingFailureReason.ModeNotSupported, detailed.failureReason);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void BeginTargeting_InvokesOnModeChanged()
        {
            using var ctx = new TargetingSkillModeContext();

            int calls = 0;
            TargetingMode last = TargetingMode.None;
            ctx.Controller.OnModeChanged += mode => { calls++; last = mode; };

            ctx.Controller.BeginTargeting(TargetingMode.Trip);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(TargetingMode.Trip, last);
        }

        [Test]
        public void CancelTargeting_InvokesOnModeChanged_WithNone()
        {
            using var ctx = new TargetingSkillModeContext();

            int calls = 0;
            var seen = new List<TargetingMode>();
            ctx.Controller.OnModeChanged += mode => { calls++; seen.Add(mode); };

            ctx.Controller.BeginTargeting(TargetingMode.Trip);
            ctx.Controller.CancelTargeting();

            Assert.AreEqual(2, calls);
            CollectionAssert.AreEqual(
                new[] { TargetingMode.Trip, TargetingMode.None },
                seen);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }


        [Test]
        public void TripMode_AllyClick_ReturnsWrongTeam_DoesNotInvokeCallback_AndKeepsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);

            int calls = 0;
            ctx.Controller.BeginTargeting(TargetingMode.Trip, _ => calls++);

            var result = ctx.Controller.TryConfirmEntity(ally);

            Assert.AreEqual(TargetingResult.WrongTeam, result);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(TargetingMode.Trip, ctx.Controller.ActiveMode);
        }

        [Test]
        public void CancelTargeting_ClearsTripShoveOrDemoralizeMode()
        {
            using var ctx = new TargetingSkillModeContext();

            ctx.Controller.BeginTargeting(TargetingMode.Trip);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

                        ctx.Controller.BeginTargeting(TargetingMode.Shove);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Grapple);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Escape);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);

            ctx.Controller.BeginTargeting(TargetingMode.Demoralize);
            ctx.Controller.CancelTargeting();
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void Reposition_SelectTarget_InvalidTarget_StaysInSelectTarget()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);

            ctx.Controller.BeginRepositionTargeting(_ => RepositionTargetSelectionResult.Rejected, _ => false);

            var result = ctx.Controller.TryConfirmEntity(ally);

            Assert.AreEqual(TargetingResult.WrongTeam, result);
            Assert.AreEqual(TargetingMode.Reposition, ctx.Controller.ActiveMode);
            Assert.IsTrue(ctx.Controller.IsRepositionSelectingTarget);
            Assert.IsFalse(ctx.Controller.IsRepositionSelectingCell);
        }

        [Test]
        public void Reposition_SelectTarget_ValidTarget_Success_EntersSelectCell()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int targetCalls = 0;
            EntityHandle selected = EntityHandle.None;
            var seenModes = new List<TargetingMode>();
            ctx.Controller.OnModeChanged += mode => seenModes.Add(mode);
            ctx.Controller.BeginRepositionTargeting(
                h =>
                {
                    targetCalls++;
                    selected = h;
                    return RepositionTargetSelectionResult.EnterCellSelection;
                },
                _ => false);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, targetCalls);
            Assert.AreEqual(enemy, selected);
            Assert.AreEqual(TargetingMode.Reposition, ctx.Controller.ActiveMode);
            Assert.IsFalse(ctx.Controller.IsRepositionSelectingTarget);
            Assert.IsTrue(ctx.Controller.IsRepositionSelectingCell);
            CollectionAssert.AreEqual(
                new[] { TargetingMode.Reposition, TargetingMode.Reposition },
                seenModes,
                "Reposition substate transition should re-emit OnModeChanged for UI refresh.");
        }

        [Test]
        public void Reposition_SelectTarget_ValidTarget_Failure_ResolvesAndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int targetCalls = 0;
            ctx.Controller.BeginRepositionTargeting(
                _ =>
                {
                    targetCalls++;
                    return RepositionTargetSelectionResult.ResolvedAndClear;
                },
                _ => false);

            var result = ctx.Controller.TryConfirmEntity(enemy);

            Assert.AreEqual(TargetingResult.Success, result);
            Assert.AreEqual(1, targetCalls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
            Assert.IsFalse(ctx.Controller.IsRepositionSelectingTarget);
            Assert.IsFalse(ctx.Controller.IsRepositionSelectingCell);
        }

        [Test]
        public void Reposition_SelectCell_InvalidCell_StaysInSelectCell()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int cellCalls = 0;
            ctx.Controller.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.EnterCellSelection,
                _ =>
                {
                    cellCalls++;
                    return false;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.IsTrue(ctx.Controller.IsRepositionSelectingCell);

            var result = ctx.Controller.TryConfirmCell(new Vector3Int(5, 0, 5));

            Assert.AreEqual(TargetingResult.InvalidTarget, result);
            Assert.AreEqual(1, cellCalls);
            Assert.AreEqual(TargetingMode.Reposition, ctx.Controller.ActiveMode);
            Assert.IsTrue(ctx.Controller.IsRepositionSelectingCell);
        }

        [Test]
        public void Reposition_SelectCell_ValidCell_ExecutesAndClearsMode()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int cellCalls = 0;
            Vector3Int confirmedCell = default;
            ctx.Controller.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.EnterCellSelection,
                cell =>
                {
                    cellCalls++;
                    confirmedCell = cell;
                    return true;
                });

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            var cellResult = ctx.Controller.TryConfirmCell(new Vector3Int(2, 0, 0));

            Assert.AreEqual(TargetingResult.Success, cellResult);
            Assert.AreEqual(1, cellCalls);
            Assert.AreEqual(new Vector3Int(2, 0, 0), confirmedCell);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void Reposition_EscFromSelectTarget_ClearsToNone_InvokesTargetCancel()
        {
            using var ctx = new TargetingSkillModeContext();

            int cancelCalls = 0;
            int cellCancelCalls = 0;
            ctx.Controller.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.Rejected,
                _ => false,
                onCancelled: () => cancelCalls++,
                onCellPhaseCancelled: () => cellCancelCalls++);

            ctx.Controller.CancelTargeting();

            Assert.AreEqual(1, cancelCalls);
            Assert.AreEqual(0, cellCancelCalls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        [Test]
        public void Reposition_EscFromSelectCell_ClearsToNone_InvokesCellCancel()
        {
            using var ctx = new TargetingSkillModeContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            int cancelCalls = 0;
            int cellCancelCalls = 0;
            ctx.Controller.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.EnterCellSelection,
                _ => false,
                onCancelled: () => cancelCalls++,
                onCellPhaseCancelled: () => cellCancelCalls++);

            Assert.AreEqual(TargetingResult.Success, ctx.Controller.TryConfirmEntity(enemy));
            Assert.IsTrue(ctx.Controller.IsRepositionSelectingCell);

            ctx.Controller.CancelTargeting();

            Assert.AreEqual(0, cancelCalls);
            Assert.AreEqual(1, cellCancelCalls);
            Assert.AreEqual(TargetingMode.None, ctx.Controller.ActiveMode);
        }

        private sealed class TargetingSkillModeContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<ScriptableObject> createdAssets = new();
            private readonly GameObject root;
            private readonly GameObject eventBusGo;
            private readonly GameObject entityManagerGo;
            private readonly GameObject turnManagerGo;
            private readonly GameObject targetingGo;

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public TargetingController Controller { get; }
            public EntityRegistry Registry { get; }

            public TargetingSkillModeContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TargetingSkillModeTests_Root");

                eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());

                targetingGo = new GameObject("TargetingController");
                targetingGo.transform.SetParent(root.transform);
                targetingGo.SetActive(false);
                Controller = targetingGo.AddComponent<TargetingController>();
                SetPrivateField(Controller, "entityManager", EntityManager);
                SetPrivateField(Controller, "turnManager", TurnManager);
                SetPrivateField(Controller, "eventBus", EventBus);
            }

            public EntityHandle RegisterEntity(string name, Team team)
            {
                return Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25,
                    Size = CreatureSize.Medium
                });
            }

            public WeaponDefinition CreateWeaponDef(
                bool isRanged,
                int reachFeet = 5,
                int rangeIncrementFeet = 0,
                int maxRangeIncrements = 0)
            {
                var def = ScriptableObject.CreateInstance<WeaponDefinition>();
                def.itemName = "Test Weapon";
                def.isRanged = isRanged;
                def.reachFeet = reachFeet;
                def.rangeIncrementFeet = rangeIncrementFeet;
                def.maxRangeIncrements = maxRangeIncrements;
                createdAssets.Add(def);
                return def;
            }

            public void SetCurrentActor(EntityHandle actor)
            {
                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = 10,
                        Modifier = 0,
                        IsPlayer = true
                    }
                };

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void Dispose()
            {
                if (root != null) Object.DestroyImmediate(root);
                for (int i = 0; i < createdAssets.Count; i++)
                {
                    if (createdAssets[i] != null)
                        Object.DestroyImmediate(createdAssets[i]);
                }
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
