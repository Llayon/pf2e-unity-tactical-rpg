using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PF2e.Core;
using PF2e.Grid;

namespace PF2e.Tests
{
    [TestFixture]
    public class GridHazardControllerTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ApplyHazardsNow_AuthoredHazard_MarksCellAndBuildsLookupAndTelegraph()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_Root");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Acid Trap",
                            new Vector3Int(2, 0, 1),
                            entryDamage: 6,
                            damageType: DamageType.Fire,
                            aiPressure: 240,
                            telegraphColor: new Color(1f, 0.25f, 0.1f, 0.3f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridData.TryGetCell(new Vector3Int(2, 0, 1), out var cellData));
                Assert.AreEqual(CellTerrain.Hazardous, cellData.terrain);
                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 1), out var hazard));
                Assert.AreEqual("Acid Trap", hazard.displayName);
                Assert.AreEqual(6, hazard.entryDamage);
                Assert.AreEqual(240, hazard.aiPressure);
                Assert.AreEqual(1, root.transform.childCount, "Expected one telegraph quad to be created for the authored hazard.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_ProneOnlyHazard_RemainsValidAndBuildsLookup()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_ProneRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Oil Slick",
                            new Vector3Int(1, 0, 2),
                            HazardEffectKind.ProneOnEntry,
                            entryDamage: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 0,
                            aiPressure: 140,
                            telegraphColor: new Color(0.9f, 0.85f, 0.2f, 0.3f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(1, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.ProneOnEntry, hazard.effectKind);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_PullHazard_NormalizesForcedMoveCellsAndBuildsLookup()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PullRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Hook Chain",
                            new Vector3Int(2, 0, 2),
                            HazardEffectKind.PullOnFailedSave,
                            entryDamage: 0,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 175,
                            telegraphColor: new Color(0.55f, 0.75f, 0.95f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.PullOnFailedSave, hazard.effectKind);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_ProneAndPullHazard_RemainsValidAndNormalizesForcedMoveCells()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PronePullRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Snag Net",
                            new Vector3Int(1, 0, 1),
                            HazardEffectKind.ProneAndPullOnFailedSave,
                            entryDamage: 0,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 185,
                            telegraphColor: new Color(0.65f, 0.8f, 0.9f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(1, 0, 1), out var hazard));
                Assert.AreEqual(HazardEffectKind.ProneAndPullOnFailedSave, hazard.effectKind);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_PullAndPersistentFireHazard_NormalizesPersistentDamageAndForcedMoveCells()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PullFireRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Hook Ember",
                            new Vector3Int(3, 0, 1),
                            HazardEffectKind.PullAndPersistentFireOnFailedSave,
                            entryDamage: 2,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Fire,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 205,
                            telegraphColor: new Color(0.95f, 0.5f, 0.2f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(3, 0, 1), out var hazard));
                Assert.AreEqual(HazardEffectKind.PullAndPersistentFireOnFailedSave, hazard.effectKind);
                Assert.AreEqual(2, hazard.persistentDamage);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_PronePullAndPersistentFireHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PronePullFireRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Hook Inferno",
                            new Vector3Int(3, 0, 2),
                            HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave,
                            entryDamage: 2,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Fire,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 225,
                            telegraphColor: new Color(1f, 0.48f, 0.18f, 0.36f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(3, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.ProneAndPullAndPersistentFireOnFailedSave, hazard.effectKind);
                Assert.AreEqual(2, hazard.persistentDamage);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_PronePushAndPersistentFireHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PronePushFireRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Blast Inferno",
                            new Vector3Int(3, 0, 3),
                            HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave,
                            entryDamage: 2,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Fire,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 225,
                            telegraphColor: new Color(1f, 0.56f, 0.18f, 0.36f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(3, 0, 3), out var hazard));
                Assert.AreEqual(HazardEffectKind.ProneAndPushAndPersistentFireOnFailedSave, hazard.effectKind);
                Assert.AreEqual(2, hazard.persistentDamage);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_DisplacementHazard_PreservesForcedMoveDepthAboveOne()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_DisplacementDepthRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Depth Pusher",
                            new Vector3Int(1, 0, 1),
                            HazardEffectKind.PushOnFailedSave,
                            entryDamage: 0,
                            persistentDamage: 0,
                            forcedMoveCells: 2,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 180,
                            telegraphColor: new Color(0.8f, 0.7f, 0.2f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(1, 0, 1), out var hazard));
                Assert.AreEqual(2, hazard.forcedMoveCells);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_DisplacementHazard_ClampsElevationPerStepToOne()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_DisplacementElevationRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Lift Pusher",
                            new Vector3Int(1, 0, 1),
                            HazardEffectKind.PushOnFailedSave,
                            entryDamage: 0,
                            persistentDamage: 0,
                            forcedMoveCells: 2,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 180,
                            telegraphColor: new Color(0.8f, 0.7f, 0.2f, 0.35f),
                            forcedMoveElevationPerCell: 3)
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(1, 0, 1), out var hazard));
                Assert.AreEqual(2, hazard.forcedMoveCells);
                Assert.AreEqual(1, hazard.forcedMoveElevationPerCell);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_SaveDamagePronePushHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_SavePronePushRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Launch Plate",
                            new Vector3Int(2, 0, 2),
                            HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave,
                            entryDamage: 5,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 235,
                            telegraphColor: new Color(0.92f, 0.82f, 0.3f, 0.35f),
                            forcedMoveElevationPerCell: 2)
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.BasicSaveDamageAndProneAndPushOnFailedSave, hazard.effectKind);
                Assert.AreEqual(5, hazard.entryDamage);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.AreEqual(1, hazard.forcedMoveElevationPerCell);
                Assert.AreEqual(0, hazard.persistentDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_SaveDamagePronePullHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_SavePronePullRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Snare Plate",
                            new Vector3Int(2, 0, 2),
                            HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave,
                            entryDamage: 5,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Slashing,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 235,
                            telegraphColor: new Color(0.72f, 0.84f, 0.95f, 0.35f),
                            forcedMoveElevationPerCell: -2)
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.BasicSaveDamageAndProneAndPullOnFailedSave, hazard.effectKind);
                Assert.AreEqual(5, hazard.entryDamage);
                Assert.AreEqual(1, hazard.forcedMoveCells);
                Assert.AreEqual(-1, hazard.forcedMoveElevationPerCell);
                Assert.AreEqual(0, hazard.persistentDamage);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_PersistentAcidHazard_ForcesAcidDamageTypeAndNormalizesPersistentDamage()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_PersistentAcidRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Acid Vent",
                            new Vector3Int(2, 0, 3),
                            HazardEffectKind.PersistentAcidOnFailedSave,
                            entryDamage: 3,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 195,
                            telegraphColor: new Color(0.7f, 0.95f, 0.35f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 3), out var hazard));
                Assert.AreEqual(HazardEffectKind.PersistentAcidOnFailedSave, hazard.effectKind);
                Assert.AreEqual(3, hazard.persistentDamage);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.AreEqual(DamageType.Acid, hazard.damageType);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_SaveDamageAndPersistentAcidHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_SaveAcidRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Acid Burst Vent",
                            new Vector3Int(1, 0, 3),
                            HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure,
                            entryDamage: 5,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 225,
                            telegraphColor: new Color(0.68f, 0.94f, 0.38f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(1, 0, 3), out var hazard));
                Assert.AreEqual(HazardEffectKind.BasicSaveDamageAndPersistentAcidOnFailure, hazard.effectKind);
                Assert.AreEqual(5, hazard.entryDamage);
                Assert.AreEqual(5, hazard.persistentDamage);
                Assert.AreEqual(DamageType.Acid, hazard.damageType);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        [Test]
        public void ApplyHazardsNow_ProneAndPersistentAcidHazard_RemainsValidAndNormalizesPayload()
        {
            bool oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("GridHazardTests_ProneAcidRoot");
            try
            {
                var gridManager = root.AddComponent<GridManager>();
                var gridData = new GridData(1f, 1f);
                SeedWalkableGrid(gridData, 4, 4);
                SetAutoPropertyBackingField(gridManager, "Data", gridData);

                var hazardController = root.AddComponent<GridHazardController>();
                SetPrivateField(hazardController, "gridManager", gridManager);
                SetPrivateField(
                    hazardController,
                    "hazards",
                    new List<GridHazardDefinition>
                    {
                        new GridHazardDefinition(
                            "Acid Slick",
                            new Vector3Int(2, 0, 2),
                            HazardEffectKind.ProneAndPersistentAcidOnFailedSave,
                            entryDamage: 2,
                            persistentDamage: 0,
                            forcedMoveCells: 0,
                            damageType: DamageType.Bludgeoning,
                            saveType: SaveType.Reflex,
                            saveDc: 16,
                            aiPressure: 215,
                            telegraphColor: new Color(0.7f, 0.96f, 0.4f, 0.35f))
                    });

                hazardController.ApplyHazardsNow();

                Assert.IsTrue(gridManager.TryGetHazard(new Vector3Int(2, 0, 2), out var hazard));
                Assert.AreEqual(HazardEffectKind.ProneAndPersistentAcidOnFailedSave, hazard.effectKind);
                Assert.AreEqual(0, hazard.entryDamage);
                Assert.AreEqual(2, hazard.persistentDamage);
                Assert.AreEqual(DamageType.Acid, hazard.damageType);
                Assert.IsTrue(hazard.IsValid);
            }
            finally
            {
                Object.DestroyImmediate(root);
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            }
        }

        private static void SeedWalkableGrid(GridData gridData, int sizeX, int sizeZ)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                    gridData.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
