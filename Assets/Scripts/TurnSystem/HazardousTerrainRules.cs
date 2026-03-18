using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Minimal hazardous-terrain entry rule for current slice.
    /// Applies when a creature ends a committed move in a Hazardous cell.
    /// Deliberately narrow: final-cell only, flat damage, no resistances/authoring overrides yet.
    /// </summary>
    public static class HazardousTerrainRules
    {
        public const int HazardousEntryDamage = 2;
        public const string HazardousTerrainActionName = "Hazardous terrain";
        public const int DefaultDifficultTerrainPressure = 10;
        public const int DefaultGreaterDifficultTerrainPressure = 20;
        public const int DefaultHazardousTerrainPressure = 100;

        public static int TryApplyEntryEffect(
            EntityHandle mover,
            Vector3Int destinationCell,
            EntityManager entityManager,
            CombatEventBus eventBus)
        {
            if (!mover.IsValid || entityManager == null || entityManager.Registry == null)
                return 0;

            var gridData = entityManager.GridData;
            if (gridData == null || !gridData.TryGetCell(destinationCell, out var cellData))
                return 0;
            if (cellData.terrain != CellTerrain.Hazardous)
                return 0;

            int entryDamage = HazardousEntryDamage;
            DamageType damageType = DamageType.Bludgeoning;
            string actionName = HazardousTerrainActionName;

            if (entityManager.GridManager != null
                && entityManager.GridManager.TryGetHazard(destinationCell, out var authoredHazard))
            {
                entryDamage = authoredHazard.entryDamage;
                damageType = authoredHazard.damageType;
                actionName = string.IsNullOrWhiteSpace(authoredHazard.displayName)
                    ? HazardousTerrainActionName
                    : authoredHazard.displayName;
            }

            return DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: entryDamage,
                damageType: damageType,
                sourceActionName: actionName,
                isCritical: false,
                entityManager: entityManager,
                eventBus: eventBus);
        }

        public static int GetTerrainPressureScore(GridManager gridManager, Vector3Int cell)
        {
            if (gridManager == null || gridManager.Data == null)
                return 0;

            if (gridManager.TryGetHazard(cell, out var hazard))
                return hazard.aiPressure;
            if (!gridManager.Data.TryGetCell(cell, out var cellData))
                return 0;

            return cellData.terrain switch
            {
                CellTerrain.Hazardous => DefaultHazardousTerrainPressure,
                CellTerrain.GreaterDifficult => DefaultGreaterDifficultTerrainPressure,
                CellTerrain.Difficult => DefaultDifficultTerrainPressure,
                _ => 0
            };
        }
    }
}
