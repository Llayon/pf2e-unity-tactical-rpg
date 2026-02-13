using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using PF2e.Core;
using PF2e.Grid;

[TestFixture]
public class MovementZoneOccupancyTests
{
    private GridData grid;
    private GridPathfinding pathfinding;
    private EntityRegistry registry;
    private OccupancyMap occupancy;
    private Dictionary<Vector3Int, int> zone;
    private MovementProfile profile;

    [SetUp]
    public void SetUp()
    {
        grid = new GridData(1.0f, 3.0f, 16);
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
                grid.SetCell(new Vector3Int(x, 0, z), CellData.CreateWalkable());

        pathfinding = new GridPathfinding();
        registry = new EntityRegistry();
        occupancy = new OccupancyMap(registry);
        zone = new Dictionary<Vector3Int, int>();

        profile = new MovementProfile
        {
            moveType = MovementType.Walk,
            speedFeet = 30,
            creatureSizeCells = 1,
            ignoresDifficultTerrain = false
        };
    }

    private EntityHandle CreateAndPlace(Team team, Vector3Int pos)
    {
        var handle = registry.Register(new EntityData
        {
            Name = $"{team}_{pos}",
            Team = team,
            Size = CreatureSize.Medium,
            MaxHP = 20,
            CurrentHP = 20
        });
        occupancy.Place(handle, pos);
        return handle;
    }

    [Test]
    public void Zone_NullOccupancy_MatchesOriginal()
    {
        int budget = 30;
        var origin = new Vector3Int(4, 0, 4);

        var originalZone = new Dictionary<Vector3Int, int>();
        pathfinding.GetMovementZone(grid, origin, profile, budget, originalZone);

        pathfinding.GetMovementZone(grid, origin, profile, budget,
            EntityHandle.None, null, zone);

        Assert.AreEqual(originalZone.Count, zone.Count);

        foreach (var kvp in originalZone)
        {
            Assert.IsTrue(zone.ContainsKey(kvp.Key), $"Missing {kvp.Key}");
            Assert.AreEqual(kvp.Value, zone[kvp.Key], $"Cost mismatch at {kvp.Key}");
        }
    }

    [Test]
    public void Zone_OccupancyWithOnlyMover_MatchesOriginal()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        int budget = 30;

        var originalZone = new Dictionary<Vector3Int, int>();
        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4), profile, budget, originalZone);

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, budget, mover, occupancy, zone);

        Assert.AreEqual(originalZone.Count, zone.Count);
        foreach (var kvp in originalZone)
        {
            Assert.IsTrue(zone.ContainsKey(kvp.Key), $"Missing {kvp.Key}");
            Assert.AreEqual(kvp.Value, zone[kvp.Key], $"Cost mismatch at {kvp.Key}");
        }
    }

    [Test]
    public void Zone_EnemyBlocks_CellNotInZone()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsFalse(zone.ContainsKey(new Vector3Int(5, 0, 4)));
    }

    [Test]
    public void Zone_EnemyWall_BlocksCellsBehind_WithLimitedBudget()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 3));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 5));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 15, mover, occupancy, zone);

        Assert.IsFalse(zone.ContainsKey(new Vector3Int(6, 0, 4)));
    }

    [Test]
    public void Zone_EnemyOnSide_CanGoAround()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(new Vector3Int(5, 0, 5)));
        Assert.IsFalse(zone.ContainsKey(new Vector3Int(5, 0, 4)));
    }

    [Test]
    public void Zone_AllyTransit_CanReachBeyond()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Player, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(new Vector3Int(6, 0, 4)));
    }

    [Test]
    public void Zone_AllyCellNotInResult()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Player, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsFalse(zone.ContainsKey(new Vector3Int(5, 0, 4)));
    }

    [Test]
    public void Zone_AllyChain_CanTraverseMultiple_AlliesNotStoppable()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(2, 0, 4));
        CreateAndPlace(Team.Player, new Vector3Int(3, 0, 4));
        CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(2, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(new Vector3Int(5, 0, 4)));
        Assert.IsFalse(zone.ContainsKey(new Vector3Int(3, 0, 4)));
        Assert.IsFalse(zone.ContainsKey(new Vector3Int(4, 0, 4)));
    }

    [Test]
    public void Zone_CostThroughAlly_SameAsEmpty()
    {
        var mover1 = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover1, occupancy, zone);
        int costWithout = zone[new Vector3Int(6, 0, 4)];

        registry = new EntityRegistry();
        occupancy = new OccupancyMap(registry);
        var mover2 = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Player, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover2, occupancy, zone);
        int costWith = zone[new Vector3Int(6, 0, 4)];

        Assert.AreEqual(costWithout, costWith);
    }

    [Test]
    public void Zone_OriginAlwaysIncluded()
    {
        var origin = new Vector3Int(4, 0, 4);
        var mover = CreateAndPlace(Team.Player, origin);

        pathfinding.GetMovementZone(grid, origin,
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(origin));
        Assert.AreEqual(0, zone[origin]);
    }

    [Test]
    public void Zone_SurroundedByEnemies_OnlyOrigin()
    {
        var origin = new Vector3Int(4, 0, 4);
        var mover = CreateAndPlace(Team.Player, origin);

        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0)
                    CreateAndPlace(Team.Enemy, new Vector3Int(4 + dx, 0, 4 + dz));

        pathfinding.GetMovementZone(grid, origin,
            profile, 30, mover, occupancy, zone);

        Assert.AreEqual(1, zone.Count);
        Assert.IsTrue(zone.ContainsKey(origin));
    }

    [Test]
    public void Zone_NeutralEntity_TraversableNotStoppable()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Neutral, new Vector3Int(5, 0, 4));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(new Vector3Int(6, 0, 4)));
        Assert.IsFalse(zone.ContainsKey(new Vector3Int(5, 0, 4)));
    }

    [Test]
    public void Zone_BudgetRespected_WithOccupancy()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(0, 0, 0));

        pathfinding.GetMovementZone(grid, new Vector3Int(0, 0, 0),
            profile, 10, mover, occupancy, zone);

        Assert.IsFalse(zone.ContainsKey(new Vector3Int(3, 0, 0)));
        Assert.IsTrue(zone.ContainsKey(new Vector3Int(2, 0, 0)));
    }

    [Test]
    public void Zone_DiagonalBetweenEnemies_CurrentlyAllowed()
    {
        var mover = CreateAndPlace(Team.Player, new Vector3Int(4, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(5, 0, 4));
        CreateAndPlace(Team.Enemy, new Vector3Int(4, 0, 5));

        pathfinding.GetMovementZone(grid, new Vector3Int(4, 0, 4),
            profile, 30, mover, occupancy, zone);

        Assert.IsTrue(zone.ContainsKey(new Vector3Int(5, 0, 5)));
    }
}
