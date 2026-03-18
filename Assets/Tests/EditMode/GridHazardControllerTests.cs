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
