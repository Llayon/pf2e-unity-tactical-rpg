using NUnit.Framework;
using UnityEngine;
using PF2e.TurnSystem;

namespace PF2e.Tests
{
    [TestFixture]
    public class AIMovementScoringTests
    {
        [Test]
        public void IsBetterFallbackApproach_MarginallyLongerButMuchSafer_PrefersSafeCell()
        {
            AIMovementCellScore best = new(
                new Vector3Int(5, 0, 1),
                actionCost: 1,
                distanceToTargetFeet: 10,
                terrainPressure: HazardousTerrainRules.DefaultHazardousTerrainPressure + 60);
            AIMovementCellScore candidate = new(
                new Vector3Int(4, 0, 1),
                actionCost: 1,
                distanceToTargetFeet: 15,
                terrainPressure: 0);

            bool result = AIMovementScoring.IsBetterFallbackApproach(in candidate, in best);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsBetterThreatEscape_LowerThreatCount_WinsBeforeDistance()
        {
            AIMovementCellScore best = new(
                new Vector3Int(3, 0, 0),
                actionCost: 1,
                distanceToTargetFeet: 5,
                terrainPressure: 0,
                hostileThreatCount: 1,
                inMeleeRange: true);
            AIMovementCellScore candidate = new(
                new Vector3Int(3, 0, 2),
                actionCost: 1,
                distanceToTargetFeet: 10,
                terrainPressure: 0,
                hostileThreatCount: 0,
                inMeleeRange: false);

            bool result = AIMovementScoring.IsBetterThreatEscape(in candidate, in best);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsBetterThreatEscape_TieOnThreatCount_PrefersSaferMeleeCell()
        {
            AIMovementCellScore best = new(
                new Vector3Int(3, 0, 1),
                actionCost: 1,
                distanceToTargetFeet: 5,
                terrainPressure: HazardousTerrainRules.DefaultHazardousTerrainPressure,
                hostileThreatCount: 0,
                inMeleeRange: true);
            AIMovementCellScore candidate = new(
                new Vector3Int(3, 0, 2),
                actionCost: 1,
                distanceToTargetFeet: 5,
                terrainPressure: 0,
                hostileThreatCount: 0,
                inMeleeRange: true);

            bool result = AIMovementScoring.IsBetterThreatEscape(in candidate, in best);

            Assert.IsTrue(result);
        }
    }
}
