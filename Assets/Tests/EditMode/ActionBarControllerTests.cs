using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Managers;
using PF2e.Presentation;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ActionBarControllerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RefreshAvailability_PlayerTurn_WithActions_EnablesStrikeAndDemoralize()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);

            ctx.RefreshAvailability();

            Assert.IsTrue(ctx.StrikeButton.interactable);
            Assert.IsTrue(ctx.DemoralizeButton.interactable);
            Assert.IsFalse(ctx.TripButton.interactable);
            Assert.IsFalse(ctx.ShoveButton.interactable);
            Assert.IsFalse(ctx.GrappleButton.interactable);
        }

        [Test]
        public void RefreshAvailability_NotPlayerTurn_DisablesAll()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Goblin", Team.Enemy);
            ctx.SetCurrentActor(actor, TurnState.EnemyTurn, actionsRemaining: 3);

            ctx.RefreshAvailability();

            ctx.AssertAllButtonsDisabled();
        }

        [Test]
        public void RefreshAvailability_NoActions_DisablesAll()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 0);

            ctx.RefreshAvailability();

            ctx.AssertAllButtonsDisabled();
        }

        [Test]
        public void RefreshAvailability_WeaponWithTripTrait_EnablesTrip()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.SetWeaponTraits(actor, WeaponTraitFlags.Trip);

            ctx.RefreshAvailability();

            Assert.IsTrue(ctx.TripButton.interactable);
            Assert.IsFalse(ctx.ShoveButton.interactable);
            Assert.IsFalse(ctx.GrappleButton.interactable);
        }

        [Test]
        public void RefreshAvailability_WeaponWithoutTripTrait_DisablesTrip()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.SetWeaponTraits(actor, WeaponTraitFlags.Shove);

            ctx.RefreshAvailability();

            Assert.IsFalse(ctx.TripButton.interactable);
            Assert.IsTrue(ctx.ShoveButton.interactable);
        }

        [Test]
        public void RefreshAvailability_ActorProne_EnablesStand()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.AddCondition(actor, new ActiveCondition(ConditionType.Prone));

            ctx.RefreshAvailability();

            Assert.IsTrue(ctx.StandButton.interactable);
        }

        [Test]
        public void RefreshAvailability_ActorGrabbed_EnablesEscape()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.AddCondition(actor, new ActiveCondition(ConditionType.Grabbed, 1));

            ctx.RefreshAvailability();

            Assert.IsTrue(ctx.EscapeButton.interactable);
        }

        [Test]
        public void RefreshAvailability_ShieldEquipped_EnablesRaiseShield()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.EquipShield(actor, isRaised: false, broken: false);

            ctx.RefreshAvailability();

            Assert.IsTrue(ctx.RaiseShieldButton.interactable);
        }

        [Test]
        public void RefreshAvailability_ShieldAlreadyRaised_DisablesRaiseShield()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.EquipShield(actor, isRaised: true, broken: false);

            ctx.RefreshAvailability();

            Assert.IsFalse(ctx.RaiseShieldButton.interactable);
        }

        [Test]
        public void HandleModeChanged_HighlightsCorrectButton()
        {
            using var ctx = new ActionBarTestContext();

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            Assert.IsTrue(ctx.TripHighlight.gameObject.activeSelf);
            Assert.IsFalse(ctx.ShoveHighlight.gameObject.activeSelf);
            Assert.IsFalse(ctx.GrappleHighlight.gameObject.activeSelf);
            Assert.IsFalse(ctx.DemoralizeHighlight.gameObject.activeSelf);
            Assert.IsFalse(ctx.EscapeHighlight.gameObject.activeSelf);
        }

        [Test]
        public void HandleModeChanged_None_ClearsAllHighlights()
        {
            using var ctx = new ActionBarTestContext();

            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);
            ctx.TargetingController.CancelTargeting();

            ctx.AssertNoHighlights();
        }

        [Test]
        public void OnTurnEnded_CancelsTargeting_AndClearsHighlight()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.EventBus.PublishCombatStarted();
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            ctx.EventBus.PublishTurnEnded(actor);

            Assert.AreEqual(TargetingMode.None, ctx.TargetingController.ActiveMode);
            ctx.AssertNoHighlights();
            ctx.AssertAllButtonsDisabled();
        }

        [Test]
        public void OnCombatEnded_CancelsTargeting_HidesBar_AndDisablesButtons()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.EventBus.PublishCombatStarted();
            ctx.TargetingController.BeginTargeting(TargetingMode.Trip);

            ctx.EventBus.PublishCombatEnded(EncounterResult.Aborted);

            Assert.AreEqual(TargetingMode.None, ctx.TargetingController.ActiveMode);
            Assert.AreEqual(0f, ctx.CanvasGroup.alpha);
            Assert.IsFalse(ctx.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(ctx.CanvasGroup.interactable);
            ctx.AssertNoHighlights();
            ctx.AssertAllButtonsDisabled();
        }

        [Test]
        public void ReenableController_ClickInvokesActionOnce_NoDuplicateListeners()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.SetWeaponTraits(actor, WeaponTraitFlags.Trip);
            ctx.EventBus.PublishCombatStarted();
            ctx.RefreshAvailability();

            int modeChangedCalls = 0;
            ctx.TargetingController.OnModeChanged += _ => modeChangedCalls++;

            ctx.ActionBarGameObject.SetActive(false);
            ctx.ActionBarGameObject.SetActive(true);

            ctx.TargetingController.CancelTargeting();
            modeChangedCalls = 0;

            ctx.TripButton.onClick.Invoke();

            Assert.AreEqual(1, modeChangedCalls);
            Assert.AreEqual(TargetingMode.Trip, ctx.TargetingController.ActiveMode);
        }

        [Test]
        public void ClickingActiveStrikeButton_TogglesTargetingOff()
        {
            using var ctx = new ActionBarTestContext();
            var actor = ctx.RegisterEntity("Fighter", Team.Player);
            ctx.SetCurrentActor(actor, TurnState.PlayerTurn, actionsRemaining: 3);
            ctx.EventBus.PublishCombatStarted();
            ctx.RefreshAvailability();

            ctx.StrikeButton.onClick.Invoke();
            Assert.AreEqual(TargetingMode.Strike, ctx.TargetingController.ActiveMode);
            Assert.IsTrue(ctx.StrikeHighlight.gameObject.activeSelf);

            ctx.StrikeButton.onClick.Invoke();
            Assert.AreEqual(TargetingMode.None, ctx.TargetingController.ActiveMode);
            Assert.IsFalse(ctx.StrikeHighlight.gameObject.activeSelf);
        }

        private sealed class ActionBarTestContext : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly GameObject ActionBarGameObject;

            private readonly bool oldIgnoreLogs;
            private readonly List<ScriptableObject> createdAssets = new();

            public CombatEventBus EventBus { get; }
            public EntityManager EntityManager { get; }
            public TurnManager TurnManager { get; }
            public PlayerActionExecutor ActionExecutor { get; }
            public TargetingController TargetingController { get; }
            public ActionBarController ActionBar { get; }
            public EntityRegistry Registry { get; }
            public CanvasGroup CanvasGroup { get; }

            public Button StrikeButton { get; }
            public Button TripButton { get; }
            public Button ShoveButton { get; }
            public Button GrappleButton { get; }
            public Button DemoralizeButton { get; }
            public Button EscapeButton { get; }
            public Button RaiseShieldButton { get; }
            public Button StandButton { get; }

            public Image StrikeHighlight { get; }
            public Image TripHighlight { get; }
            public Image ShoveHighlight { get; }
            public Image GrappleHighlight { get; }
            public Image DemoralizeHighlight { get; }
            public Image EscapeHighlight { get; }
            public Image RaiseShieldHighlight { get; }
            public Image StandHighlight { get; }

            public ActionBarTestContext()
            {
                oldIgnoreLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;

                Root = new GameObject("ActionBarTests_Root");
                Root.SetActive(false);

                var eventBusGo = new GameObject("EventBus");
                eventBusGo.transform.SetParent(Root.transform);
                EventBus = eventBusGo.AddComponent<CombatEventBus>();

                var entityManagerGo = new GameObject("EntityManager");
                entityManagerGo.transform.SetParent(Root.transform);
                EntityManager = entityManagerGo.AddComponent<EntityManager>();
                Registry = new EntityRegistry();
                SetAutoPropertyBackingField(EntityManager, "Registry", Registry);

                var turnManagerGo = new GameObject("TurnManager");
                turnManagerGo.transform.SetParent(Root.transform);
                TurnManager = turnManagerGo.AddComponent<TurnManager>();
                SetPrivateField(TurnManager, "entityManager", EntityManager);
                SetPrivateField(TurnManager, "eventBus", EventBus);
                SetPrivateField(TurnManager, "state", TurnState.Inactive);
                SetPrivateField(TurnManager, "initiativeOrder", new List<InitiativeEntry>());

                var executorGo = new GameObject("PlayerActionExecutor");
                executorGo.transform.SetParent(Root.transform);
                ActionExecutor = executorGo.AddComponent<PlayerActionExecutor>();
                SetPrivateField(ActionExecutor, "turnManager", TurnManager);
                SetPrivateField(ActionExecutor, "entityManager", EntityManager);

                var targetingGo = new GameObject("TargetingController");
                targetingGo.transform.SetParent(Root.transform);
                targetingGo.SetActive(false);
                TargetingController = targetingGo.AddComponent<TargetingController>();
                SetPrivateField(TargetingController, "actionExecutor", ActionExecutor);
                SetPrivateField(TargetingController, "entityManager", EntityManager);
                SetPrivateField(TargetingController, "turnManager", TurnManager);
                SetPrivateField(TargetingController, "eventBus", EventBus);

                ActionBarGameObject = new GameObject("ActionBar");
                ActionBarGameObject.transform.SetParent(Root.transform);
                ActionBarGameObject.SetActive(false);
                ActionBarGameObject.AddComponent<RectTransform>();
                CanvasGroup = ActionBarGameObject.AddComponent<CanvasGroup>();
                ActionBar = ActionBarGameObject.AddComponent<ActionBarController>();

                StrikeButton = CreateButton("StrikeButton", ActionBarGameObject.transform, out var strikeHl);
                TripButton = CreateButton("TripButton", ActionBarGameObject.transform, out var tripHl);
                ShoveButton = CreateButton("ShoveButton", ActionBarGameObject.transform, out var shoveHl);
                GrappleButton = CreateButton("GrappleButton", ActionBarGameObject.transform, out var grappleHl);
                DemoralizeButton = CreateButton("DemoralizeButton", ActionBarGameObject.transform, out var demoralizeHl);
                EscapeButton = CreateButton("EscapeButton", ActionBarGameObject.transform, out var escapeHl);
                RaiseShieldButton = CreateButton("RaiseShieldButton", ActionBarGameObject.transform, out var raiseShieldHl);
                StandButton = CreateButton("StandButton", ActionBarGameObject.transform, out var standHl);

                StrikeHighlight = strikeHl;
                TripHighlight = tripHl;
                ShoveHighlight = shoveHl;
                GrappleHighlight = grappleHl;
                DemoralizeHighlight = demoralizeHl;
                EscapeHighlight = escapeHl;
                RaiseShieldHighlight = raiseShieldHl;
                StandHighlight = standHl;

                SetPrivateField(ActionBar, "eventBus", EventBus);
                SetPrivateField(ActionBar, "entityManager", EntityManager);
                SetPrivateField(ActionBar, "turnManager", TurnManager);
                SetPrivateField(ActionBar, "actionExecutor", ActionExecutor);
                SetPrivateField(ActionBar, "targetingController", TargetingController);
                SetPrivateField(ActionBar, "canvasGroup", CanvasGroup);
                SetPrivateField(ActionBar, "strikeButton", StrikeButton);
                SetPrivateField(ActionBar, "tripButton", TripButton);
                SetPrivateField(ActionBar, "shoveButton", ShoveButton);
                SetPrivateField(ActionBar, "grappleButton", GrappleButton);
                SetPrivateField(ActionBar, "demoralizeButton", DemoralizeButton);
                SetPrivateField(ActionBar, "escapeButton", EscapeButton);
                SetPrivateField(ActionBar, "raiseShieldButton", RaiseShieldButton);
                SetPrivateField(ActionBar, "standButton", StandButton);
                SetPrivateField(ActionBar, "strikeHighlight", StrikeHighlight);
                SetPrivateField(ActionBar, "tripHighlight", TripHighlight);
                SetPrivateField(ActionBar, "shoveHighlight", ShoveHighlight);
                SetPrivateField(ActionBar, "grappleHighlight", GrappleHighlight);
                SetPrivateField(ActionBar, "demoralizeHighlight", DemoralizeHighlight);
                SetPrivateField(ActionBar, "escapeHighlight", EscapeHighlight);
                SetPrivateField(ActionBar, "raiseShieldHighlight", RaiseShieldHighlight);
                SetPrivateField(ActionBar, "standHighlight", StandHighlight);

                Root.SetActive(true);
                targetingGo.SetActive(true);
                ActionBarGameObject.SetActive(true);

                // EditMode lifecycle can be inconsistent across Unity versions for inactive-parent setup.
                // Call OnEnable explicitly to guarantee subscriptions for deterministic tests.
                InvokePrivate(TargetingController, "OnEnable");
                InvokePrivate(ActionBar, "OnEnable");

                Assert.IsTrue(TargetingController.isActiveAndEnabled, "TargetingController failed to enable in test context.");
                Assert.IsTrue(ActionBar.isActiveAndEnabled, "ActionBarController failed to enable in test context.");
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
                    Strength = 16,
                    Dexterity = 14,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 12
                });
            }

            public void SetCurrentActor(EntityHandle actor, TurnState state, int actionsRemaining)
            {
                var actorData = Registry.Get(actor);
                Assert.IsNotNull(actorData);
                actorData.ActionsRemaining = actionsRemaining;

                var order = new List<InitiativeEntry>
                {
                    new InitiativeEntry
                    {
                        Handle = actor,
                        Roll = 10,
                        Modifier = 0,
                        IsPlayer = actorData.Team == Team.Player
                    }
                };

                SetPrivateField(TurnManager, "initiativeOrder", order);
                SetPrivateField(TurnManager, "currentIndex", 0);
                SetPrivateField(TurnManager, "state", state);
            }

            public void SetWeaponTraits(EntityHandle actor, WeaponTraitFlags traits)
            {
                var data = Registry.Get(actor);
                Assert.IsNotNull(data);

                var def = ScriptableObject.CreateInstance<WeaponDefinition>();
                createdAssets.Add(def);
                def.traits = traits;
                def.reachFeet = 5;
                def.isRanged = false;
                data.EquippedWeapon = new WeaponInstance { def = def };
            }

            public void EquipShield(EntityHandle actor, bool isRaised, bool broken)
            {
                var data = Registry.Get(actor);
                Assert.IsNotNull(data);

                var def = ScriptableObject.CreateInstance<ShieldDefinition>();
                createdAssets.Add(def);
                def.acBonus = 2;
                def.hardness = 3;
                def.maxHP = 20;

                var shield = ShieldInstance.CreateEquipped(def);
                shield.isRaised = isRaised;
                if (broken) shield.currentHP = 0;
                data.EquippedShield = shield;
            }

            public void AddCondition(EntityHandle actor, ActiveCondition condition)
            {
                var data = Registry.Get(actor);
                Assert.IsNotNull(data);
                data.Conditions.Add(condition);
            }

            public void RefreshAvailability()
            {
                InvokePrivate(ActionBar, "RefreshAvailability");
            }

            public void AssertAllButtonsDisabled()
            {
                Assert.IsFalse(StrikeButton.interactable);
                Assert.IsFalse(TripButton.interactable);
                Assert.IsFalse(ShoveButton.interactable);
                Assert.IsFalse(GrappleButton.interactable);
                Assert.IsFalse(DemoralizeButton.interactable);
                Assert.IsFalse(EscapeButton.interactable);
                Assert.IsFalse(RaiseShieldButton.interactable);
                Assert.IsFalse(StandButton.interactable);
            }

            public void AssertNoHighlights()
            {
                Assert.IsFalse(StrikeHighlight.gameObject.activeSelf);
                Assert.IsFalse(TripHighlight.gameObject.activeSelf);
                Assert.IsFalse(ShoveHighlight.gameObject.activeSelf);
                Assert.IsFalse(GrappleHighlight.gameObject.activeSelf);
                Assert.IsFalse(DemoralizeHighlight.gameObject.activeSelf);
                Assert.IsFalse(EscapeHighlight.gameObject.activeSelf);
                Assert.IsFalse(RaiseShieldHighlight.gameObject.activeSelf);
                Assert.IsFalse(StandHighlight.gameObject.activeSelf);
            }

            public void Dispose()
            {
                foreach (var asset in createdAssets)
                {
                    if (asset != null) Object.DestroyImmediate(asset);
                }

                if (Root != null) Object.DestroyImmediate(Root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static Button CreateButton(string name, Transform parent, out Image highlight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var button = go.GetComponent<Button>();

            var highlightGo = new GameObject($"{name}_Highlight", typeof(RectTransform), typeof(Image));
            highlightGo.transform.SetParent(go.transform, false);
            highlightGo.SetActive(false);
            highlight = highlightGo.GetComponent<Image>();
            return button;
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
