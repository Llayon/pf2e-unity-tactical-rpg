using System;
using PF2e.Grid;
using UnityEngine;

namespace PF2e.Core
{
    /// <summary>
    /// Grid-based LoS/Cover resolver for strike preview/runtime (same-elevation MVP).
    /// Uses supercover traversal with a permissive corner rule.
    /// </summary>
    public static class StrikeLineResolver
    {
        private const int StandardCoverBonus = 2;
        private const double CornerEpsilon = 1e-9;

        public static StrikeLineResult ResolveSameElevation(
            GridData gridData,
            OccupancyMap occupancy,
            Vector3Int attackerCell,
            Vector3Int targetCell,
            EntityHandle attacker,
            EntityHandle target)
        {
            if (gridData == null)
                return new StrikeLineResult(false, 0);

            if (attackerCell.x == targetCell.x && attackerCell.z == targetCell.z)
                return new StrikeLineResult(true, 0);

            int currentX = attackerCell.x;
            int currentZ = attackerCell.z;
            int targetX = targetCell.x;
            int targetZ = targetCell.z;
            int y = attackerCell.y;

            int dx = targetX - currentX;
            int dz = targetZ - currentZ;
            int stepX = Math.Sign(dx);
            int stepZ = Math.Sign(dz);

            double absDx = Math.Abs(dx);
            double absDz = Math.Abs(dz);
            double tDeltaX = absDx > 0 ? 1.0d / absDx : double.PositiveInfinity;
            double tDeltaZ = absDz > 0 ? 1.0d / absDz : double.PositiveInfinity;
            double tMaxX = absDx > 0 ? 0.5d / absDx : double.PositiveInfinity;
            double tMaxZ = absDz > 0 ? 0.5d / absDz : double.PositiveInfinity;

            bool hasCover = false;

            while (currentX != targetX || currentZ != targetZ)
            {
                if (Math.Abs(tMaxX - tMaxZ) <= CornerEpsilon)
                {
                    // Supercover corner-touch case: line touches two cardinal cells before entering the diagonal cell.
                    var sideA = new Vector3Int(currentX + stepX, y, currentZ);
                    var sideB = new Vector3Int(currentX, y, currentZ + stepZ);

                    bool sideABlocks = IsLosBlockingCell(gridData, sideA, attackerCell, targetCell);
                    bool sideBBlocks = IsLosBlockingCell(gridData, sideB, attackerCell, targetCell);
                    if (sideABlocks && sideBBlocks)
                        return new StrikeLineResult(false, 0);

                    if (occupancy != null)
                    {
                        hasCover |= GivesCover(occupancy, sideA, attacker, target, attackerCell, targetCell);
                        hasCover |= GivesCover(occupancy, sideB, attacker, target, attackerCell, targetCell);
                    }

                    currentX += stepX;
                    currentZ += stepZ;
                    tMaxX += tDeltaX;
                    tMaxZ += tDeltaZ;

                    var diagonalCell = new Vector3Int(currentX, y, currentZ);
                    if (!IsEndpoint(diagonalCell, attackerCell, targetCell))
                    {
                        if (IsLosBlockingCell(gridData, diagonalCell, attackerCell, targetCell))
                            return new StrikeLineResult(false, 0);

                        if (occupancy != null)
                            hasCover |= GivesCover(occupancy, diagonalCell, attacker, target, attackerCell, targetCell);
                    }

                    continue;
                }

                if (tMaxX < tMaxZ)
                {
                    currentX += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    currentZ += stepZ;
                    tMaxZ += tDeltaZ;
                }

                var cell = new Vector3Int(currentX, y, currentZ);
                if (IsEndpoint(cell, attackerCell, targetCell))
                    continue;

                if (IsLosBlockingCell(gridData, cell, attackerCell, targetCell))
                    return new StrikeLineResult(false, 0);

                if (occupancy != null)
                    hasCover |= GivesCover(occupancy, cell, attacker, target, attackerCell, targetCell);
            }

            return new StrikeLineResult(true, hasCover ? StandardCoverBonus : 0);
        }

        private static bool IsEndpoint(Vector3Int cell, Vector3Int attackerCell, Vector3Int targetCell)
        {
            return cell == attackerCell || cell == targetCell;
        }

        private static bool IsLosBlockingCell(GridData gridData, Vector3Int cell, Vector3Int attackerCell, Vector3Int targetCell)
        {
            if (IsEndpoint(cell, attackerCell, targetCell))
                return false;

            if (!gridData.TryGetCell(cell, out var data))
                return true;

            // MVP coupling: non-walkable cells block line of sight.
            return !data.IsWalkable;
        }

        private static bool GivesCover(
            OccupancyMap occupancy,
            Vector3Int cell,
            EntityHandle attacker,
            EntityHandle target,
            Vector3Int attackerCell,
            Vector3Int targetCell)
        {
            if (IsEndpoint(cell, attackerCell, targetCell))
                return false;

            var occupant = occupancy.GetOccupant(cell);
            if (!occupant.IsValid)
                return false;

            if (occupant == attacker || occupant == target)
                return false;

            return true;
        }
    }
}
