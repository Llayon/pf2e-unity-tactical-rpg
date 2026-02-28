using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Data;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.Presentation.Entity;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class TargetingFeedbackControllerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ModeChanged_Trip_HighlightsEligibleTargetsOnly()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            Assert.AreEqual(TargetingTintState.None, ctx.GetTintState(actor));
            Assert.AreEqual(TargetingTintState.None, ctx.GetTintState(ally));
            Assert.AreEqual(TargetingTintState.Eligible, ctx.GetTintState(enemy));
        }

        [Test]
        public void HoverValid_AppliesHoverValidState_OverEligible()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Demoralize);

            ctx.GridManager.SetHoveredEntity(enemy);

            Assert.AreEqual(TargetingTintState.HoverValid, ctx.GetTintState(enemy));

            ctx.GridManager.SetHoveredEntity(null);
            Assert.AreEqual(TargetingTintState.Eligible, ctx.GetTintState(enemy));
        }

        [Test]
        public void HoverInvalid_AppliesHoverInvalidState()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            ctx.GridManager.SetHoveredEntity(ally);

            Assert.AreEqual(TargetingTintState.HoverInvalid, ctx.GetTintState(ally));
            Assert.AreEqual(TargetingTintState.Eligible, ctx.GetTintState(enemy));
        }

        [Test]
        public void CancelTargeting_ClearsAllTints()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            ctx.GridManager.SetHoveredEntity(enemy);

            ctx.TargetingController.CancelTargeting();

            Assert.AreEqual(TargetingTintState.None, ctx.GetTintState(enemy));
        }

        [Test]
        public void TurnEnded_ClearsAllTints()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            ctx.GridManager.SetHoveredEntity(enemy);

            ctx.EventBus.PublishTurnEnded(actor);

            Assert.AreEqual(TargetingMode.None, ctx.TargetingController.ActiveMode);
            Assert.AreEqual(TargetingTintState.None, ctx.GetTintState(enemy));
        }

        [Test]
        public void Reposition_CellPhase_ShowsDestinationCellHighlights_AndClearsEntityTints()
        {
            using var ctx = new TargetingFeedbackTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.SetPendingRepositionDestinations(new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0));

            ctx.TargetingController.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.EnterCellSelection,
                _ => false);

            Assert.AreEqual(TargetingResult.Success, ctx.TargetingController.TryConfirmEntity(enemy));
            Assert.IsTrue(ctx.TargetingController.IsRepositionSelectingCell);

            Assert.AreEqual(TargetingTintState.None, ctx.GetTintState(enemy));
            Assert.AreEqual(2, ctx.GetActiveCellHighlightCount());
        }

        private sealed class TargetingFeedbackTestContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<ScriptableObject> createdAssets = new();
            private readonly GameObject root;
            private readonly Dictionary<EntityHandle, EntityView> views;

            public CombatEventBus EventBus { get; }
            public GridManager GridManager { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor ActionExecutor { get; }
            public TargetingController TargetingController { get; }
            public TargetingFeedbackController FeedbackController { get; }
            public CellHighlightPool CellHighlightPool { get; }
            public EntityRegistry Registry { get; }

            public TargetingFeedbackTestContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TargetingFeedbackTests_Root");
                root.SetActive(false);

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var gridGo = new GameObject("GridManager");
                gridGo.transform.SetParent(root.transform);
                GridManager = gridGo.AddComponent<GridManager>();
                CellHighlightPool = gridGo.AddComponent<CellHighlightPool>();
                var gridConfig = ScriptableObject.CreateInstance<GridConfig>();
                createdAssets.Add(gridConfig);
                SetPrivateField(GridManager, "gridConfig", gridConfig);
                SetPrivateField(GridManager, "createTestGrid", false);

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);
                SetPrivateField(EntityManager, "gridManager", GridManager);
                SetPrivateField(EntityManager, "eventBus", EventBus);

                var viewsField = typeof(EntityManager).GetField("views", InstanceNonPublic);
                Assert.IsNotNull(viewsField);
                views = viewsField.GetValue(EntityManager) as Dictionary<EntityHandle, EntityView>;
                Assert.IsNotNull(views, "Failed to access EntityManager views dictionary.");

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());

                var executorGo = new GameObject("PlayerActionExecutor");
                executorGo.transform.SetParent(root.transform);
                ActionExecutor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(ActionExecutor, "turnManager", TurnManager);
                SetPrivateField(ActionExecutor, "entityManager", EntityManager);

                var targetingGo = new GameObject("TargetingController");
                targetingGo.transform.SetParent(root.transform);
                targetingGo.SetActive(false);
                TargetingController = targetingGo.AddComponent<TargetingController>();
                SetPrivateField(TargetingController, "actionExecutor", ActionExecutor);
                SetPrivateField(TargetingController, "entityManager", EntityManager);
                SetPrivateField(TargetingController, "turnManager", TurnManager);
                SetPrivateField(TargetingController, "eventBus", EventBus);

                var feedbackGo = new GameObject("TargetingFeedbackController");
                feedbackGo.transform.SetParent(root.transform);
                feedbackGo.SetActive(false);
                FeedbackController = feedbackGo.AddComponent<TargetingFeedbackController>();
                SetPrivateField(FeedbackController, "eventBus", EventBus);
                SetPrivateField(FeedbackController, "entityManager", EntityManager);
                SetPrivateField(FeedbackController, "gridManager", GridManager);
                SetPrivateField(FeedbackController, "targetingController", TargetingController);
                SetPrivateField(FeedbackController, "actionExecutor", ActionExecutor);
                SetPrivateField(FeedbackController, "cellHighlightPool", CellHighlightPool);

                root.SetActive(true);
                if (GridManager.Data == null)
                    InvokePrivate(GridManager, "Awake");
                targetingGo.SetActive(true);
                feedbackGo.SetActive(true);

                InvokePrivate(TargetingController, "OnEnable");
                InvokePrivate(FeedbackController, "OnEnable");
            }

            public EntityHandle RegisterEntity(string name, Team team)
            {
                var handle = Registry.Register(new EntityData
                {
                    Name = name,
                    Team = team,
                    Level = 1,
                    MaxHP = 10,
                    CurrentHP = 10,
                    Speed = 25,
                    Size = CreatureSize.Medium,
                });

                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = $"View_{name}_{handle.Id}";
                capsule.transform.SetParent(root.transform, false);
                var view = capsule.AddComponent<EntityView>();
                view.Initialize(handle, team == Team.Player ? Color.green : Color.red);
                views[handle] = view;
                return handle;
            }

            public void SetCurrentActor(EntityHandle actor)
            {
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = 3;

                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = new CheckRoll(10, 0, CheckSource.Perception()),
                        IsPlayer = actorData.Team == Team.Player
                    }
                };

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public TargetingTintState GetTintState(EntityHandle handle)
            {
                Assert.IsTrue(views.TryGetValue(handle, out var view));
                var tint = view.GetComponent<TargetingTintController>();
                if (tint == null) return TargetingTintState.None;
                return tint.CurrentState;
            }

            public int GetActiveCellHighlightCount()
            {
                int count = 0;
                for (int i = 0; i < CellHighlightPool.transform.childCount; i++)
                {
                    var child = CellHighlightPool.transform.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                        count++;
                }
                return count;
            }

            public void SetPendingRepositionDestinations(params Vector3Int[] cells)
            {
                SetPrivateField(ActionExecutor, "hasPendingRepositionSelection", true);
                var field = typeof(PlayerActionExecutor).GetField("pendingRepositionDestinations", InstanceNonPublic);
                Assert.IsNotNull(field, "Failed to access pendingRepositionDestinations list.");
                var list = field.GetValue(ActionExecutor) as List<Vector3Int>;
                Assert.IsNotNull(list, "pendingRepositionDestinations is not a List<Vector3Int>.");
                list.Clear();
                list.AddRange(cells);
            }

            public void Dispose()
            {
                if (root != null) Object.DestroyImmediate(root);
                foreach (var asset in createdAssets)
                {
                    if (asset != null) Object.DestroyImmediate(asset);
                }
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstanceNonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
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
