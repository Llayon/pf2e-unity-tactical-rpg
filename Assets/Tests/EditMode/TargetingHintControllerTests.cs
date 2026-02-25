using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PF2e.Core;
using PF2e.Data;
using PF2e.Grid;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class TargetingHintControllerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ModeChanged_None_HidesPanel()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            Assert.Greater(ctx.CanvasGroup.alpha, 0f);

            ctx.TargetingController.CancelTargeting();

            Assert.AreEqual(0f, ctx.CanvasGroup.alpha);
            Assert.AreEqual(string.Empty, ctx.HintTextValue);
        }

        [Test]
        public void ModeChanged_Trip_NoHover_ShowsPrompt()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            AssertVisible(ctx);
            Assert.AreEqual("Trip: choose an enemy in reach", ctx.HintTextValue);
            Assert.AreEqual(new Color(0.9f, 0.9f, 0.95f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void Reposition_CellPhase_ShowsDestinationCellPrompt()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginRepositionTargeting(
                _ => RepositionTargetSelectionResult.EnterCellSelection,
                _ => false);

            Assert.AreEqual(TargetingResult.Success, ctx.TargetingController.TryConfirmEntity(enemy));

            AssertVisible(ctx);
            Assert.AreEqual("Reposition: choose destination (Esc = skip move, action spent)", ctx.HintTextValue);
            Assert.AreEqual(new Color(0.9f, 0.9f, 0.95f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void HoverValid_ShowsValidTextAndColor()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Demoralize);
            ctx.GridManager.SetHoveredEntity(enemy);

            AssertVisible(ctx);
            Assert.AreEqual("Demoralize: valid target (Intimidation vs Will DC)", ctx.HintTextValue);
            Assert.AreEqual(new Color(0.45f, 1f, 0.55f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void HoverStrikeConcealed_ShowsWarningTextAndColor()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Archer", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);
            ctx.Registry.Get(actor).EquippedWeapon = new WeaponInstance { def = bow };
            ctx.Registry.Get(enemy).Conditions.Add(new ActiveCondition(ConditionType.Concealed));

            // Use TargetingController fallback validation path in this lightweight UI harness.
            SetPrivateField(ctx.TargetingController, "actionExecutor", null);

            ctx.TargetingController.BeginTargeting(TargetingMode.Strike);
            ctx.GridManager.SetHoveredEntity(enemy);

            AssertVisible(ctx);
            Assert.AreEqual("Strike: valid target (concealed: DC 5 flat check)", ctx.HintTextValue);
            Assert.AreEqual(new Color(1f, 0.85f, 0.35f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void HoverStrikeCovered_ShowsWarningTextAndColor()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Archer", Team.Player);
            var blocker = ctx.RegisterEntity("Crate", Team.Neutral);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            var bow = ctx.CreateWeaponDef(isRanged: true, rangeIncrementFeet: 60, maxRangeIncrements: 6);
            ctx.Registry.Get(actor).EquippedWeapon = new WeaponInstance { def = bow };

            ctx.WireEntityManagerGridForStrikePreview();
            ctx.SetWalkableCells(
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 0),
                new Vector3Int(2, 0, 0));
            ctx.PlaceEntity(actor, new Vector3Int(0, 0, 0));
            ctx.PlaceEntity(blocker, new Vector3Int(1, 0, 0));
            ctx.PlaceEntity(enemy, new Vector3Int(2, 0, 0));

            // Use TargetingController fallback validation path in this lightweight UI harness.
            SetPrivateField(ctx.TargetingController, "actionExecutor", null);

            ctx.TargetingController.BeginTargeting(TargetingMode.Strike);
            ctx.GridManager.SetHoveredEntity(enemy);

            AssertVisible(ctx);
            Assert.AreEqual("Strike: valid target (cover: +2 AC)", ctx.HintTextValue);
            Assert.AreEqual(new Color(1f, 0.85f, 0.35f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void HoverInvalid_ShowsInvalidTextAndColor()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            ctx.GridManager.SetHoveredEntity(ally);

            AssertVisible(ctx);
            Assert.AreEqual("Trip: choose an enemy", ctx.HintTextValue);
            Assert.AreEqual(new Color(1f, 0.55f, 0.5f, 1f), ctx.HintTextColor);
        }

        [Test]
        public void HoverExit_RestoresModePrompt()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var enemy = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor);

            ctx.TargetingController.BeginTargeting(TargetingMode.Demoralize);
            ctx.GridManager.SetHoveredEntity(enemy);
            Assert.AreEqual("Demoralize: valid target (Intimidation vs Will DC)", ctx.HintTextValue);

            ctx.GridManager.SetHoveredEntity(null);

            AssertVisible(ctx);
            Assert.AreEqual("Demoralize: choose an enemy within 30 ft", ctx.HintTextValue);
        }

        [Test]
        public void TurnEnded_HidesPanel()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            ctx.EventBus.PublishTurnEnded(actor);

            Assert.AreEqual(TargetingMode.None, ctx.TargetingController.ActiveMode);
            AssertHidden(ctx);
        }

        [Test]
        public void CombatEnded_HidesPanel()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            ctx.EventBus.PublishCombatEnded(EncounterResult.Aborted);

            AssertHidden(ctx);
        }

        [Test]
        public void ActionsChanged_RecomputesHoverReason_WhenModeActive()
        {
            using var ctx = new TargetingHintTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            var ally = ctx.RegisterEntity("Wizard", Team.Player);
            ctx.SetCurrentActor(actor);
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            ctx.GridManager.SetHoveredEntity(ally);
            Assert.AreEqual("Trip: choose an enemy", ctx.HintTextValue);

            ctx.EventBus.PublishActionsChanged(actor, 2);

            AssertVisible(ctx);
            Assert.AreEqual("Trip: choose an enemy", ctx.HintTextValue);
        }

        private static void AssertVisible(TargetingHintTestContext ctx)
        {
            Assert.AreEqual(1f, ctx.CanvasGroup.alpha);
            Assert.IsFalse(ctx.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(ctx.CanvasGroup.interactable);
        }

        private static void AssertHidden(TargetingHintTestContext ctx)
        {
            Assert.AreEqual(0f, ctx.CanvasGroup.alpha);
            Assert.IsFalse(ctx.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(ctx.CanvasGroup.interactable);
            Assert.AreEqual(string.Empty, ctx.HintTextValue);
        }

        private sealed class TargetingHintTestContext : System.IDisposable
        {
            private readonly bool oldIgnoreLogs;
            private readonly List<ScriptableObject> createdAssets = new();
            private readonly GameObject root;

            public CombatEventBus EventBus { get; }
            public GridManager GridManager { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor ActionExecutor { get; }
            public TargetingController TargetingController { get; }
            public TargetingHintController HintController { get; }
            public EntityRegistry Registry { get; }
            public CanvasGroup CanvasGroup { get; }
            public Component HintTextComponent { get; }
            public string HintTextValue => (string)GetComponentProperty(HintTextComponent, "text");
            public Color HintTextColor => (Color)GetComponentProperty(HintTextComponent, "color");

            public TargetingHintTestContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                root = new GameObject("TargetingHintTests_Root");
                root.SetActive(false);

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var gridGo = new GameObject("GridManager");
                gridGo.transform.SetParent(root.transform);
                GridManager = gridGo.AddComponent<GridManager>();
                var gridConfig = ScriptableObject.CreateInstance<GridConfig>();
                createdAssets.Add(gridConfig);
                SetPrivateField(GridManager, "gridConfig", gridConfig);
                SetPrivateField(GridManager, "createTestGrid", false);

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

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

                var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                canvasGo.transform.SetParent(root.transform, false);
                var panelGo = new GameObject("TargetingHintPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                panelGo.transform.SetParent(canvasGo.transform, false);
                CanvasGroup = panelGo.GetComponent<CanvasGroup>();

                var tmpTextType = ResolveType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.IsNotNull(tmpTextType, "Unable to resolve TMPro.TextMeshProUGUI type. Add Unity.TextMeshPro reference to test asmdef or keep reflection path valid.");

                var textGo = new GameObject("HintText", typeof(RectTransform), typeof(CanvasRenderer));
                textGo.transform.SetParent(panelGo.transform, false);
                HintTextComponent = textGo.AddComponent(tmpTextType);

                HintController = panelGo.AddComponent<TargetingHintController>();
                SetPrivateField(HintController, "eventBus", EventBus);
                SetPrivateField(HintController, "turnManager", TurnManager);
                SetPrivateField(HintController, "gridManager", GridManager);
                SetPrivateField(HintController, "targetingController", TargetingController);
                SetPrivateField(HintController, "canvasGroup", CanvasGroup);
                SetPrivateField(HintController, "hintText", HintTextComponent);

                root.SetActive(true);
                targetingGo.SetActive(true);
                panelGo.SetActive(true);

                InvokePrivate(TargetingController, "OnEnable");
                InvokePrivate(HintController, "OnEnable");
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
                    Size = CreatureSize.Medium,
                    GridPosition = Vector3Int.zero
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
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = 3;

                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = 10,
                        Modifier = 0,
                        IsPlayer = true
                    }
                });
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", TurnState.PlayerTurn);
            }

            public void WireEntityManagerGridForStrikePreview()
            {
                SetPrivateField(EntityManager, "gridManager", GridManager);

                if (EntityManager.Occupancy == null)
                    SetAutoPropertyBackingField(EntityManager, "Occupancy", new OccupancyMap(Registry));
            }

            public void SetWalkableCells(params Vector3Int[] cells)
            {
                if (GridManager.Data == null)
                    SetAutoPropertyBackingField(GridManager, "Data", new GridData(1f, 1f));

                for (int i = 0; i < cells.Length; i++)
                    GridManager.Data.SetCell(cells[i], CellData.CreateWalkable());
            }

            public void PlaceEntity(EntityHandle handle, Vector3Int cell)
            {
                var data = Registry.Get(handle);
                Assert.IsNotNull(data);

                data.GridPosition = cell;
                Assert.IsNotNull(EntityManager.Occupancy, "EntityManager.Occupancy should exist in test context.");
                Assert.IsTrue(
                    EntityManager.Occupancy.Place(handle, cell, data.SizeCells),
                    $"Failed to place {data.Name} at {cell}");
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

        private static object GetComponentProperty(Component component, string propertyName)
        {
            Assert.IsNotNull(component);
            var prop = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(prop, $"Missing property '{propertyName}' on {component.GetType().Name}");
            return prop.GetValue(component);
        }

        private static System.Type ResolveType(string assemblyQualifiedName)
        {
            return System.Type.GetType(assemblyQualifiedName);
        }
    }
}
