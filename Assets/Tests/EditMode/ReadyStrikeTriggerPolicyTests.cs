using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class ReadyStrikeTriggerPolicyTests
    {
        [Test]
        public void DidEnterStrikeRange_Melee_OutsideToInside_ReturnsTrue()
        {
            var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var target = CreateTarget(new Vector3Int(2, 0, 0));

            bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                actor,
                target,
                from: new Vector3Int(2, 0, 0),
                to: new Vector3Int(1, 0, 0));

            Assert.IsTrue(triggered);
        }

        [Test]
        public void DidEnterStrikeRange_Melee_InsideToInsideMovement_ReturnsTrue()
        {
            var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var target = CreateTarget(new Vector3Int(1, 0, 0));

            bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                actor,
                target,
                from: new Vector3Int(1, 0, 0),
                to: new Vector3Int(1, 0, 1));

            Assert.IsTrue(triggered);
        }

        [Test]
        public void DidEnterStrikeRange_Melee_OutsideToOutside_ReturnsFalse()
        {
            var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: false, reachFeet: 5);
            var target = CreateTarget(new Vector3Int(3, 0, 0));

            bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                actor,
                target,
                from: new Vector3Int(3, 0, 0),
                to: new Vector3Int(2, 0, 1));

            Assert.IsFalse(triggered);
        }

        [Test]
        public void DidEnterStrikeRange_Ranged_EnteringFirstIncrement_ReturnsTrue()
        {
            var def = CreateRangedDefinition(incrementFeet: 60);
            try
            {
                var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: true, reachFeet: 5, def);
                var target = CreateTarget(new Vector3Int(13, 0, 0)); // 65 ft

                bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                    actor,
                    target,
                    from: new Vector3Int(13, 0, 0),
                    to: new Vector3Int(12, 0, 0)); // 60 ft

                Assert.IsTrue(triggered);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void DidEnterStrikeRange_Ranged_InsideToInsideMovement_ReturnsTrue()
        {
            var def = CreateRangedDefinition(incrementFeet: 60);
            try
            {
                var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: true, reachFeet: 5, def);
                var target = CreateTarget(new Vector3Int(12, 0, 0)); // 60 ft

                bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                    actor,
                    target,
                    from: new Vector3Int(12, 0, 0),
                    to: new Vector3Int(12, 0, 1)); // still in first increment

                Assert.IsTrue(triggered);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void DidEnterStrikeRange_Ranged_OutsideFirstIncrement_ReturnsFalse()
        {
            var def = CreateRangedDefinition(incrementFeet: 60);
            try
            {
                var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: true, reachFeet: 5, def);
                var target = CreateTarget(new Vector3Int(20, 0, 0)); // 100 ft

                bool triggered = ReadyStrikeTriggerPolicy.DidEnterStrikeRange(
                    actor,
                    target,
                    from: new Vector3Int(20, 0, 0),
                    to: new Vector3Int(16, 0, 0)); // 80 ft, still outside first increment

                Assert.IsFalse(triggered);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void IsWithinReadyStrikeTriggerRange_Ranged_UsesFirstIncrementOnly()
        {
            var def = CreateRangedDefinition(incrementFeet: 60);
            try
            {
                var actor = CreateActor(new Vector3Int(0, 0, 0), isRanged: true, reachFeet: 5, def);
                var targetIn = CreateTarget(new Vector3Int(12, 0, 0)); // 60 ft
                var targetOut = CreateTarget(new Vector3Int(16, 0, 0)); // 80 ft

                Assert.IsTrue(ReadyStrikeTriggerPolicy.IsWithinReadyStrikeTriggerRange(actor, targetIn));
                Assert.IsFalse(ReadyStrikeTriggerPolicy.IsWithinReadyStrikeTriggerRange(actor, targetOut));
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void TryGetRangedReadyTriggerDistanceFeet_ReturnsIncrementForRangedWeapon()
        {
            var def = CreateRangedDefinition(incrementFeet: 70);
            try
            {
                var weapon = new WeaponInstance
                {
                    def = def,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };

                Assert.IsTrue(ReadyStrikeTriggerPolicy.TryGetRangedReadyTriggerDistanceFeet(weapon, out int range));
                Assert.AreEqual(70, range);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void TryGetRangedReadyTriggerDistanceFeet_NonRangedOrMissingIncrement_ReturnsFalse()
        {
            var melee = new WeaponInstance
            {
                def = ScriptableObject.CreateInstance<WeaponDefinition>(),
                potencyBonus = 0,
                strikingRank = StrikingRuneRank.None
            };

            try
            {
                melee.def.isRanged = false;
                melee.def.rangeIncrementFeet = 0;
                Assert.IsFalse(ReadyStrikeTriggerPolicy.TryGetRangedReadyTriggerDistanceFeet(melee, out _));

                melee.def.isRanged = true;
                melee.def.rangeIncrementFeet = 0;
                Assert.IsFalse(ReadyStrikeTriggerPolicy.TryGetRangedReadyTriggerDistanceFeet(melee, out _));
            }
            finally
            {
                Object.DestroyImmediate(melee.def);
            }
        }

        private static EntityData CreateActor(
            Vector3Int position,
            bool isRanged,
            int reachFeet,
            WeaponDefinition rangedDef = null)
        {
            _ = reachFeet;
            var data = new EntityData
            {
                GridPosition = position
            };

            if (isRanged)
            {
                data.EquippedWeapon = new WeaponInstance
                {
                    def = rangedDef,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
            }
            else
            {
                data.EquippedWeapon = new WeaponInstance
                {
                    def = null,
                    potencyBonus = 0,
                    strikingRank = StrikingRuneRank.None
                };
            }

            return data;
        }

        private static EntityData CreateTarget(Vector3Int position)
        {
            return new EntityData
            {
                GridPosition = position
            };
        }

        private static WeaponDefinition CreateRangedDefinition(int incrementFeet)
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.isRanged = true;
            def.rangeIncrementFeet = incrementFeet;
            def.reachFeet = 5;
            def.maxRangeIncrements = 6;
            def.diceCount = 1;
            def.dieSides = 6;
            return def;
        }
    }
}
