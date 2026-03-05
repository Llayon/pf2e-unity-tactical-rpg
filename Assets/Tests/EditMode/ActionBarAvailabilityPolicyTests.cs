using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;
using UnityEngine;

namespace PF2e.Tests
{
    [TestFixture]
    public class ActionBarAvailabilityPolicyTests
    {
        [Test]
        public void BuildForActor_MapsWeaponTraitsConditionsAndShieldState()
        {
            var policy = new ActionBarAvailabilityPolicy();

            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10
            };

            var weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();

            try
            {
                weaponDef.traits = WeaponTraitFlags.Trip | WeaponTraitFlags.Grapple;
                actor.EquippedWeapon = new WeaponInstance { def = weaponDef };

                var shield = ShieldInstance.CreateEquipped(shieldDef);
                shield.isRaised = false;
                actor.EquippedShield = shield;

                actor.Conditions.Add(new ActiveCondition(ConditionType.Grabbed, 1));
                actor.Conditions.Add(new ActiveCondition(ConditionType.Prone));

                var state = policy.BuildForActor(actor);

                Assert.IsTrue(state.strikeInteractable);
                Assert.IsTrue(state.tripInteractable);
                Assert.IsFalse(state.shoveInteractable);
                Assert.IsTrue(state.grappleInteractable);
                Assert.IsTrue(state.repositionInteractable);
                Assert.IsTrue(state.demoralizeInteractable);
                Assert.IsTrue(state.escapeInteractable);
                Assert.IsTrue(state.aidInteractable);
                Assert.IsTrue(state.readyInteractable);
                Assert.IsFalse(state.castSpellInteractable);
                Assert.IsTrue(state.raiseShieldInteractable);
                Assert.IsTrue(state.guardVisible);
                Assert.IsTrue(state.standInteractable);
                Assert.IsTrue(state.standVisible);
            }
            finally
            {
                Object.DestroyImmediate(weaponDef);
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void BuildForActor_ShieldRaised_DisablesRaiseShield()
        {
            var policy = new ActionBarAvailabilityPolicy();
            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10
            };

            var shieldDef = ScriptableObject.CreateInstance<ShieldDefinition>();
            try
            {
                var shield = ShieldInstance.CreateEquipped(shieldDef);
                shield.isRaised = true;
                actor.EquippedShield = shield;

                var state = policy.BuildForActor(actor);

                Assert.IsFalse(state.raiseShieldInteractable);
            }
            finally
            {
                Object.DestroyImmediate(shieldDef);
            }
        }

        [Test]
        public void BuildForActor_GlassShieldCantripAvailable_EnablesCastSpellSlot()
        {
            var policy = new ActionBarAvailabilityPolicy();
            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                KnowsGlassShieldCantrip = true,
                GlassShieldCooldownRoundsRemaining = 0
            };

            var state = policy.BuildForActor(actor);
            Assert.IsFalse(state.raiseShieldInteractable);
            Assert.IsTrue(state.castSpellInteractable);
            Assert.IsTrue(state.guardVisible);
            Assert.IsFalse(state.standVisible);
        }

        [Test]
        public void BuildForActor_StandardShieldCantripAvailable_EnablesCastSpellSlot()
        {
            var policy = new ActionBarAvailabilityPolicy();
            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                KnowsStandardShieldCantrip = true,
                StandardShieldCooldownRoundsRemaining = 0
            };

            var state = policy.BuildForActor(actor);
            Assert.IsFalse(state.raiseShieldInteractable);
            Assert.IsTrue(state.castSpellInteractable);
            Assert.IsTrue(state.guardVisible);
            Assert.IsFalse(state.standVisible);
        }

        [Test]
        public void BuildForActor_StandardShieldAlreadyRaised_DisablesCastSpellSlot()
        {
            var policy = new ActionBarAvailabilityPolicy();
            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                KnowsStandardShieldCantrip = true,
                StandardShieldCooldownRoundsRemaining = 0
            };
            Assert.IsTrue(actor.ActivateStandardShield(acBonus: 1, hardness: 5, maxHP: 1));

            var state = policy.BuildForActor(actor);
            Assert.IsFalse(state.castSpellInteractable);
            Assert.IsFalse(state.raiseShieldInteractable);
            Assert.IsTrue(state.guardVisible);
            Assert.IsFalse(state.standVisible);
        }

        [Test]
        public void BuildForActor_NoShieldAndNoShieldCantrips_HidesGuard()
        {
            var policy = new ActionBarAvailabilityPolicy();
            var actor = new EntityData
            {
                Team = Team.Player,
                CurrentHP = 10,
                MaxHP = 10,
                EquippedShield = default,
                KnowsStandardShieldCantrip = false,
                KnowsGlassShieldCantrip = false
            };

            var state = policy.BuildForActor(actor);
            Assert.IsFalse(state.guardVisible);
            Assert.IsFalse(state.standVisible);
        }

        [Test]
        public void TryEvaluate_NullDependencies_ReturnsFalse()
        {
            var policy = new ActionBarAvailabilityPolicy();

            bool ok = policy.TryEvaluate(
                turnManager: null,
                actionExecutor: null,
                registry: null,
                out var state);

            Assert.IsFalse(ok);
            Assert.IsFalse(state.strikeInteractable);
        }

    }
}
