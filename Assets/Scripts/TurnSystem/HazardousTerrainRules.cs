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

            return DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: HazardousEntryDamage,
                damageType: DamageType.Bludgeoning,
                sourceActionName: HazardousTerrainActionName,
                isCritical: false,
                entityManager: entityManager,
                eventBus: eventBus);
        }
    }
}
