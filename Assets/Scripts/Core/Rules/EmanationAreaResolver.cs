using System.Collections.Generic;
using UnityEngine;
using PF2e.Grid;

namespace PF2e.Core
{
    /// <summary>
    /// Minimal PF2e square-grid emanation helper.
    /// Includes every same-elevation cell within the supplied PF2e distance radius.
    /// </summary>
    public static class EmanationAreaResolver
    {
        public static void Resolve(
            Vector3Int originCell,
            int radiusFeet,
            List<Vector3Int> outCells,
            GridData gridData = null)
        {
            outCells?.Clear();
            if (outCells == null || radiusFeet < 0)
                return;

            int maxOffsetCells = Mathf.CeilToInt(radiusFeet / 5f);
            for (int dz = -maxOffsetCells; dz <= maxOffsetCells; dz++)
            {
                for (int dx = -maxOffsetCells; dx <= maxOffsetCells; dx++)
                {
                    var cell = new Vector3Int(originCell.x + dx, originCell.y, originCell.z + dz);
                    if (GridDistancePF2e.DistanceFeetXZ(originCell, cell) > radiusFeet)
                        continue;
                    if (gridData != null && !gridData.HasCell(cell))
                        continue;

                    outCells.Add(cell);
                }
            }
        }
    }
}
