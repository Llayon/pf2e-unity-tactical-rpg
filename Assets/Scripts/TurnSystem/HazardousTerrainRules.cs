using System.Collections.Generic;
using PF2e.Core;
using PF2e.Grid;
using PF2e.Managers;
using UnityEngine;

namespace PF2e.TurnSystem
{
    /// <summary>
    /// Current hazardous-terrain entry rule for authored trap cells.
    /// Applies when a creature ends a committed move in a Hazardous cell.
    /// Deliberately narrow: final-cell only, with support for flat damage, save-based damage,
    /// prone-on-entry variants, persistent fire/acid, and authored push/pull displacement
    /// with optional per-step elevation change.
    /// </summary>
    public static class HazardousTerrainRules
    {
        public const int HazardousEntryDamage = 2;
        public const string HazardousTerrainActionName = "Hazardous terrain";
        public const int DefaultDifficultTerrainPressure = 10;
        public const int DefaultGreaterDifficultTerrainPressure = 20;
        public const int DefaultHazardousTerrainPressure = 100;
        private static readonly ConditionService ConditionService = new();

        public static int TryApplyEntryEffect(
            EntityHandle mover,
            Vector3Int destinationCell,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng = null,
            Vector3Int? originCell = null)
        {
            if (!mover.IsValid || entityManager == null || entityManager.Registry == null)
                return 0;

            var gridData = entityManager.GridData;
            if (gridData == null || !gridData.TryGetCell(destinationCell, out var cellData))
                return 0;
            if (cellData.terrain != CellTerrain.Hazardous)
                return 0;

            var moverData = entityManager.Registry.Get(mover);
            if (moverData == null || !moverData.IsAlive)
                return 0;

            if (entityManager.GridManager != null
                && entityManager.GridManager.TryGetHazard(destinationCell, out var authoredHazard))
            {
                return ResolveAuthoredHazard(
                    mover,
                    moverData,
                    destinationCell,
                    originCell,
                    authoredHazard,
                    entityManager,
                    eventBus,
                    rng);
            }

            int entryDamage = HazardousEntryDamage;
            DamageType damageType = DamageType.Bludgeoning;
            string actionName = HazardousTerrainActionName;

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

        private static int ResolveAuthoredHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            string actionName = string.IsNullOrWhiteSpace(hazard.displayName)
                ? HazardousTerrainActionName
                : hazard.displayName;

            switch (hazard.effectKind)
            {
                case HazardEffectKind.BasicSaveDamage:
                    return ResolveSaveDamageHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        applyProneOnFailure: false,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneOnEntry:
                    ApplyProne(
                        moverData,
                        eventBus);
                    return 0;

                case HazardEffectKind.PersistentFireOnEntry:
                    ApplyPersistentFire(
                        moverData,
                        GetPersistentFireDamage(hazard),
                        eventBus);
                    return 0;

                case HazardEffectKind.PersistentFireOnFailedSave:
                    return ResolvePersistentFireOnFailedSaveHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        eventBus,
                        rng);

                case HazardEffectKind.PersistentAcidOnFailedSave:
                    return ResolvePersistentAcidOnFailedSaveHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure:
                    return ResolveSaveDamageAndPersistentAcidHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneAndPersistentAcidOnFailedSave:
                    return ResolveProneAndPersistentAcidHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndPersistentFireOnFailure:
                    return ResolveSaveDamageAndPersistentFireHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneAndPersistentFireOnFailedSave:
                    return ResolveProneAndPersistentFireHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        eventBus,
                        rng);

                case HazardEffectKind.PushOnFailedSave:
                    return ResolvePushOnFailedSaveHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndPushOnFailedSave:
                    return ResolveSaveDamageAndPushHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave:
                    return ResolveSaveDamageAndProneAndPushHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave:
                    return ResolveProneAndPushAndPersistentFireHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.PullOnFailedSave:
                    return ResolvePullOnFailedSaveHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndPullOnFailedSave:
                    return ResolveSaveDamageAndPullHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave:
                    return ResolveSaveDamageAndProneAndPullHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneAndPullOnFailedSave:
                    return ResolveProneAndPullHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.PullAndPersistentFireOnFailedSave:
                    return ResolvePullAndPersistentFireHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave:
                    return ResolveProneAndPullAndPersistentFireHazard(
                        mover,
                        moverData,
                        destinationCell,
                        originCell,
                        actionName,
                        hazard,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.DamageAndProneOnFailure:
                    return ResolveSaveDamageHazard(
                        mover,
                        moverData,
                        destinationCell,
                        actionName,
                        hazard,
                        applyProneOnFailure: true,
                        entityManager,
                        eventBus,
                        rng);

                case HazardEffectKind.FlatDamage:
                default:
                    return DamageApplicationService.ApplyDamage(
                        source: EntityHandle.None,
                        target: mover,
                        amount: hazard.entryDamage,
                        damageType: hazard.damageType,
                        sourceActionName: actionName,
                        isCritical: false,
                        entityManager: entityManager,
                        eventBus: eventBus);
            }
        }

        private static int ResolveSaveDamageHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            bool applyProneOnFailure,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (applyProneOnFailure
                && moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyProne(
                    moverData,
                    eventBus);
            }

            return appliedDamage;
        }

        private static int ResolveProneAndPersistentFireHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyProne(moverData, eventBus);
                ApplyPersistentFire(moverData, GetPersistentFireDamage(hazard), eventBus);
            }

            return 0;
        }

        private static int ResolveSaveDamageAndPushHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPush(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
            }

            return appliedDamage;
        }

        private static int ResolvePushOnFailedSaveHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPush(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
            }

            return 0;
        }

        private static int ResolveProneAndPushAndPersistentFireHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPush(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyProne(moverData, eventBus);
                ApplyPersistentFire(moverData, GetPersistentFireDamage(hazard), eventBus);
            }

            return 0;
        }

        private static int ResolveSaveDamageAndProneAndPushHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPush(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyProne(moverData, eventBus);
            }

            return appliedDamage;
        }

        private static int ResolvePullOnFailedSaveHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
            }

            return 0;
        }

        private static int ResolveSaveDamageAndPullHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
            }

            return appliedDamage;
        }

        private static int ResolveProneAndPullHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyProne(moverData, eventBus);
            }

            return 0;
        }

        private static int ResolveSaveDamageAndProneAndPullHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyProne(moverData, eventBus);
            }

            return appliedDamage;
        }

        private static int ResolvePullAndPersistentFireHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyPersistentFire(moverData, GetPersistentFireDamage(hazard), eventBus);
            }

            return 0;
        }

        private static int ResolveProneAndPullAndPersistentFireHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                TryApplyForcedPull(
                    mover,
                    destinationCell,
                    originCell,
                    Mathf.Max(1, hazard.forcedMoveCells),
                    hazard.forcedMoveElevationPerCell,
                    entityManager);
                ApplyProne(moverData, eventBus);
                ApplyPersistentFire(moverData, GetPersistentFireDamage(hazard), eventBus);
            }

            return 0;
        }

        private static int ResolvePersistentFireOnFailedSaveHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyPersistentFire(
                    moverData,
                    GetPersistentFireDamage(hazard),
                    eventBus);
            }

            return 0;
        }

        private static int ResolvePersistentAcidOnFailedSaveHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyPersistentAcid(
                    moverData,
                    GetPersistentAcidDamage(hazard),
                    eventBus);
            }

            return 0;
        }

        private static int ResolveSaveDamageAndPersistentFireHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyPersistentFire(
                    moverData,
                    GetPersistentFireDamage(hazard),
                    eventBus);
            }

            return appliedDamage;
        }

        private static int ResolveSaveDamageAndPersistentAcidHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            EntityManager entityManager,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            int resolvedDamage = CheckResolver.ApplyBasicSaveDamage(hazard.entryDamage, save.degree);

            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            int appliedDamage = DamageApplicationService.ApplyDamage(
                source: EntityHandle.None,
                target: mover,
                amount: resolvedDamage,
                damageType: hazard.damageType,
                sourceActionName: actionName,
                isCritical: save.degree == DegreeOfSuccess.CriticalFailure,
                entityManager: entityManager,
                eventBus: eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyPersistentAcid(
                    moverData,
                    GetPersistentAcidDamage(hazard),
                    eventBus);
            }

            return appliedDamage;
        }

        private static int ResolveProneAndPersistentAcidHazard(
            EntityHandle mover,
            EntityData moverData,
            Vector3Int destinationCell,
            string actionName,
            in GridHazardInfo hazard,
            CombatEventBus eventBus,
            IRng rng)
        {
            var save = CheckResolver.RollSave(moverData, hazard.saveType, hazard.saveDc, rng);
            PublishSaveLog(mover, actionName, destinationCell, hazard.saveType, save, eventBus);

            if (moverData.IsAlive
                && (save.degree == DegreeOfSuccess.Failure || save.degree == DegreeOfSuccess.CriticalFailure))
            {
                ApplyProne(moverData, eventBus);
                ApplyPersistentAcid(
                    moverData,
                    GetPersistentAcidDamage(hazard),
                    eventBus);
            }

            return 0;
        }

        private static void PublishSaveLog(
            EntityHandle mover,
            string actionName,
            Vector3Int destinationCell,
            SaveType saveType,
            in CheckResult save,
            CombatEventBus eventBus)
        {
            if (eventBus == null)
                return;

            string message =
                $"triggers {actionName} at {destinationCell}, rolls {saveType} {save.total} vs DC {save.dc} - {FormatDegree(save.degree)}.";
            eventBus.Publish(mover, message, CombatLogCategory.ActionResult);
        }

        private static void ApplyProne(
            EntityData moverData,
            CombatEventBus eventBus)
        {
            if (moverData == null || !moverData.IsAlive)
                return;

            var conditionDeltaBuffer = new List<ConditionDelta>(1);
            ConditionService.AddOrRefresh(moverData, ConditionType.Prone, value: 0, rounds: -1, conditionDeltaBuffer);
            PublishConditionDeltas(conditionDeltaBuffer, eventBus);
        }

        private static void ApplyPersistentFire(
            EntityData moverData,
            int persistentDamage,
            CombatEventBus eventBus)
        {
            ApplyPersistentDamageCondition(
                moverData,
                ConditionType.PersistentFire,
                persistentDamage,
                eventBus);
        }

        private static void ApplyPersistentAcid(
            EntityData moverData,
            int persistentDamage,
            CombatEventBus eventBus)
        {
            ApplyPersistentDamageCondition(
                moverData,
                ConditionType.PersistentAcid,
                persistentDamage,
                eventBus);
        }

        private static void ApplyPersistentDamageCondition(
            EntityData moverData,
            ConditionType conditionType,
            int persistentDamage,
            CombatEventBus eventBus)
        {
            if (moverData == null || !moverData.IsAlive)
                return;

            int damagePerTick = Mathf.Max(1, persistentDamage);
            var conditionDeltaBuffer = new List<ConditionDelta>(1);
            ConditionService.AddOrRefresh(
                moverData,
                conditionType,
                value: damagePerTick,
                rounds: -1,
                conditionDeltaBuffer);
            PublishConditionDeltas(conditionDeltaBuffer, eventBus);
        }

        private static int GetPersistentFireDamage(in GridHazardInfo hazard)
        {
            return Mathf.Max(1, hazard.persistentDamage > 0 ? hazard.persistentDamage : hazard.entryDamage);
        }

        private static int GetPersistentAcidDamage(in GridHazardInfo hazard)
        {
            return Mathf.Max(1, hazard.persistentDamage > 0 ? hazard.persistentDamage : hazard.entryDamage);
        }

        private static int TryApplyForcedPush(
            EntityHandle mover,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            int maxCells,
            int elevationPerStep,
            EntityManager entityManager)
        {
            return TryApplyForcedDisplacement(
                mover,
                destinationCell,
                originCell,
                maxCells,
                elevationPerStep,
                entityManager,
                towardOrigin: false);
        }

        private static int TryApplyForcedPull(
            EntityHandle mover,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            int maxCells,
            int elevationPerStep,
            EntityManager entityManager)
        {
            return TryApplyForcedDisplacement(
                mover,
                destinationCell,
                originCell,
                maxCells,
                elevationPerStep,
                entityManager,
                towardOrigin: true);
        }

        private static int TryApplyForcedDisplacement(
            EntityHandle mover,
            Vector3Int destinationCell,
            Vector3Int? originCell,
            int maxCells,
            int elevationPerStep,
            EntityManager entityManager,
            bool towardOrigin)
        {
            if (!originCell.HasValue || entityManager == null || maxCells <= 0)
                return 0;

            Vector3Int step = towardOrigin
                ? originCell.Value - destinationCell
                : destinationCell - originCell.Value;
            step.y = 0;
            step.x = Mathf.Clamp(step.x, -1, 1);
            step.z = Mathf.Clamp(step.z, -1, 1);
            step.y = Mathf.Clamp(elevationPerStep, -1, 1);

            if (step == Vector3Int.zero)
                return 0;

            // Current authored trap slice supports an optional authored per-step elevation delta
            // and still stops on the first blocked step.
            int movedCells = 0;
            Vector3Int current = destinationCell;
            for (int i = 0; i < maxCells; i++)
            {
                Vector3Int next = current + step;
                if (!entityManager.TryMoveEntityImmediate(mover, next))
                    break;

                current = next;
                movedCells++;
            }

            return movedCells;
        }

        private static void PublishConditionDeltas(List<ConditionDelta> conditionDeltaBuffer, CombatEventBus eventBus)
        {
            if (eventBus == null || conditionDeltaBuffer == null || conditionDeltaBuffer.Count == 0)
                return;

            for (int i = 0; i < conditionDeltaBuffer.Count; i++)
            {
                var delta = conditionDeltaBuffer[i];
                eventBus.PublishConditionChanged(
                    delta.entity,
                    delta.type,
                    delta.changeType,
                    delta.oldValue,
                    delta.newValue,
                    delta.oldRemainingRounds,
                    delta.newRemainingRounds);
            }
        }

        private static string FormatDegree(DegreeOfSuccess degree)
        {
            return degree switch
            {
                DegreeOfSuccess.CriticalSuccess => "Critical Success",
                DegreeOfSuccess.Success => "Success",
                DegreeOfSuccess.Failure => "Failure",
                DegreeOfSuccess.CriticalFailure => "Critical Failure",
                _ => degree.ToString()
            };
        }
    }
}
