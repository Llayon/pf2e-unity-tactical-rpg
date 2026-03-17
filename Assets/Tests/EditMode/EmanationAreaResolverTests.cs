using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class EmanationAreaResolverTests
    {
        [Test]
        public void Resolve_ThirtyFootRadius_IncludesExpectedBoundaryCells()
        {
            var cells = new List<Vector3Int>();

            EmanationAreaResolver.Resolve(
                Vector3Int.zero,
                radiusFeet: 30,
                outCells: cells);

            CollectionAssert.Contains(cells, Vector3Int.zero);
            CollectionAssert.Contains(cells, new Vector3Int(6, 0, 0));
            CollectionAssert.Contains(cells, new Vector3Int(0, 0, 6));
            CollectionAssert.Contains(cells, new Vector3Int(4, 0, 4));
            CollectionAssert.DoesNotContain(cells, new Vector3Int(5, 0, 5));
            CollectionAssert.DoesNotContain(cells, new Vector3Int(7, 0, 0));
        }

        [Test]
        public void Resolve_WithGridFilter_OmitsMissingCells()
        {
            var gridData = new GridData(cellWorldSize: 1f, heightStepWorld: 1f);
            gridData.SetCell(Vector3Int.zero, CellData.CreateWalkable());
            gridData.SetCell(new Vector3Int(1, 0, 0), CellData.CreateWalkable());

            var cells = new List<Vector3Int>();
            EmanationAreaResolver.Resolve(
                Vector3Int.zero,
                radiusFeet: 30,
                outCells: cells,
                gridData: gridData);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    Vector3Int.zero,
                    new Vector3Int(1, 0, 0)
                },
                cells);
        }
    }
}
