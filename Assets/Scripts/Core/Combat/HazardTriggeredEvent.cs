using PF2e.Grid;
using UnityEngine;

namespace PF2e.Core
{
    public readonly struct HazardTriggeredEvent
    {
        public readonly EntityHandle target;
        public readonly string hazardName;
        public readonly Vector3Int hazardCell;
        public readonly HazardEffectKind effectKind;
        public readonly DamageType damageType;
        public readonly int rolledDamage;
        public readonly int appliedDamage;
        public readonly SaveType? saveType;
        public readonly CheckResult? saveResult;
        public readonly ConditionType? primaryConditionType;
        public readonly int primaryConditionValue;
        public readonly ConditionType? secondaryConditionType;
        public readonly int secondaryConditionValue;
        public readonly Vector3Int positionBefore;
        public readonly Vector3Int positionAfter;
        public readonly int movedCells;
        public readonly bool pulledTowardOrigin;
        public readonly int hpBefore;
        public readonly int hpAfter;
        public readonly bool targetDefeated;

        public HazardTriggeredEvent(
            EntityHandle target,
            string hazardName,
            Vector3Int hazardCell,
            HazardEffectKind effectKind,
            DamageType damageType,
            int rolledDamage,
            int appliedDamage,
            SaveType? saveType,
            CheckResult? saveResult,
            ConditionType? primaryConditionType,
            int primaryConditionValue,
            ConditionType? secondaryConditionType,
            int secondaryConditionValue,
            Vector3Int positionBefore,
            Vector3Int positionAfter,
            int movedCells,
            bool pulledTowardOrigin,
            int hpBefore,
            int hpAfter,
            bool targetDefeated)
        {
            this.target = target;
            this.hazardName = hazardName;
            this.hazardCell = hazardCell;
            this.effectKind = effectKind;
            this.damageType = damageType;
            this.rolledDamage = rolledDamage;
            this.appliedDamage = appliedDamage;
            this.saveType = saveType;
            this.saveResult = saveResult;
            this.primaryConditionType = primaryConditionType;
            this.primaryConditionValue = primaryConditionValue;
            this.secondaryConditionType = secondaryConditionType;
            this.secondaryConditionValue = secondaryConditionValue;
            this.positionBefore = positionBefore;
            this.positionAfter = positionAfter;
            this.movedCells = movedCells;
            this.pulledTowardOrigin = pulledTowardOrigin;
            this.hpBefore = hpBefore;
            this.hpAfter = hpAfter;
            this.targetDefeated = targetDefeated;
        }
    }
}
