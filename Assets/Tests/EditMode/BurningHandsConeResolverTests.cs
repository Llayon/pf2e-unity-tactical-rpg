using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

namespace PF2e.Tests
{
    [TestFixture]
    public class BurningHandsConeResolverTests
    {
        [Test]
        public void TryResolve_NorthDirection_ReturnsExpectedSevenCellCone()
        {
            var cells = new List<Vector3Int>();

            bool resolved = BurningHandsConeResolver.TryResolve(
                casterCell: Vector3Int.zero,
                aimCell: new Vector3Int(0, 0, 1),
                outCells: cells,
                out int directionIndex);

            Assert.IsTrue(resolved);
            Assert.AreEqual(2, directionIndex);
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector3Int(0, 0, 1),
                    new Vector3Int(-1, 0, 2),
                    new Vector3Int(0, 0, 2),
                    new Vector3Int(1, 0, 2),
                    new Vector3Int(-1, 0, 3),
                    new Vector3Int(0, 0, 3),
                    new Vector3Int(1, 0, 3)
                },
                cells);
        }

        [Test]
        public void TryResolve_NorthEastDirection_ReturnsExpectedSevenCellCone()
        {
            var cells = new List<Vector3Int>();

            bool resolved = BurningHandsConeResolver.TryResolve(
                casterCell: Vector3Int.zero,
                aimCell: new Vector3Int(1, 0, 1),
                outCells: cells,
                out int directionIndex);

            Assert.IsTrue(resolved);
            Assert.AreEqual(1, directionIndex);
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector3Int(1, 0, 1),
                    new Vector3Int(1, 0, 2),
                    new Vector3Int(2, 0, 1),
                    new Vector3Int(2, 0, 2),
                    new Vector3Int(2, 0, 3),
                    new Vector3Int(3, 0, 2),
                    new Vector3Int(3, 0, 3)
                },
                cells);
        }
    }
}
