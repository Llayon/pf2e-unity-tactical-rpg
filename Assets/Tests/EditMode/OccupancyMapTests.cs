using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

[TestFixture]
public class OccupancyMapTests
{
    private EntityRegistry _registry;
    private OccupancyMap _occupancy;

    [SetUp]
    public void SetUp()
    {
        _registry = new EntityRegistry();
        _occupancy = new OccupancyMap(_registry);
    }

    private EntityHandle CreateEntity(Team team, CreatureSize size = CreatureSize.Medium)
    {
        return _registry.Register(new EntityData
        {
            Name = "Test",
            Team = team,
            Size = size,
            MaxHP = 20,
            CurrentHP = 20
        });
    }

    [Test]
    public void Place_Medium_OccupiesOneCell()
    {
        var entity = CreateEntity(Team.Player);
        var pos = new Vector3Int(3, 0, 3);

        Assert.IsTrue(_occupancy.Place(entity, pos));
        Assert.IsTrue(_occupancy.IsOccupied(pos));
        Assert.AreEqual(entity, _occupancy.GetOccupant(pos));
    }

    [Test]
    public void Place_Large_OccupiesFourCells()
    {
        var entity = CreateEntity(Team.Player, CreatureSize.Large);
        var anchor = new Vector3Int(2, 0, 2);

        Assert.IsTrue(_occupancy.Place(entity, anchor, 2));
        Assert.IsTrue(_occupancy.IsOccupied(new Vector3Int(2, 0, 2)));
        Assert.IsTrue(_occupancy.IsOccupied(new Vector3Int(3, 0, 2)));
        Assert.IsTrue(_occupancy.IsOccupied(new Vector3Int(2, 0, 3)));
        Assert.IsTrue(_occupancy.IsOccupied(new Vector3Int(3, 0, 3)));
        Assert.AreEqual(4, _occupancy.OccupiedCellCount);
    }

    [Test]
    public void Place_OnOccupied_ReturnsFalse()
    {
        var e1 = CreateEntity(Team.Player);
        var e2 = CreateEntity(Team.Player);
        var pos = new Vector3Int(1, 0, 1);

        _occupancy.Place(e1, pos);
        Assert.IsFalse(_occupancy.Place(e2, pos));
        Assert.AreEqual(e1, _occupancy.GetOccupant(pos));
    }

    [Test]
    public void Place_SameEntity_ReplacesPosition()
    {
        var entity = CreateEntity(Team.Player);
        var pos1 = new Vector3Int(1, 0, 1);
        var pos2 = new Vector3Int(5, 0, 5);

        _occupancy.Place(entity, pos1);
        _occupancy.Place(entity, pos2);

        Assert.IsFalse(_occupancy.IsOccupied(pos1));
        Assert.IsTrue(_occupancy.IsOccupied(pos2));
    }

    [Test]
    public void Remove_FreesAllCells()
    {
        var entity = CreateEntity(Team.Player, CreatureSize.Large);
        _occupancy.Place(entity, new Vector3Int(0, 0, 0), 2);

        _occupancy.Remove(entity);

        Assert.AreEqual(0, _occupancy.OccupiedCellCount);
    }

    [Test]
    public void Remove_NonExistent_NoError()
    {
        var entity = CreateEntity(Team.Player);
        Assert.DoesNotThrow(() => _occupancy.Remove(entity));
    }

    [Test]
    public void Move_UpdatesPosition()
    {
        var entity = CreateEntity(Team.Player);
        var from = new Vector3Int(1, 0, 1);
        var to = new Vector3Int(4, 0, 4);

        _occupancy.Place(entity, from);
        Assert.IsTrue(_occupancy.Move(entity, to));

        Assert.IsFalse(_occupancy.IsOccupied(from));
        Assert.IsTrue(_occupancy.IsOccupied(to));
    }

    [Test]
    public void Move_ToOccupied_RollsBack()
    {
        var e1 = CreateEntity(Team.Player);
        var e2 = CreateEntity(Team.Player);
        var pos1 = new Vector3Int(1, 0, 1);
        var pos2 = new Vector3Int(3, 0, 3);

        _occupancy.Place(e1, pos1);
        _occupancy.Place(e2, pos2);

        Assert.IsFalse(_occupancy.Move(e1, pos2));

        Assert.AreEqual(e1, _occupancy.GetOccupant(pos1));
        Assert.AreEqual(e2, _occupancy.GetOccupant(pos2));
    }

    [Test]
    public void CanTraverse_EmptyCell_True()
    {
        var mover = CreateEntity(Team.Player);
        Assert.IsTrue(_occupancy.CanTraverse(new Vector3Int(5, 0, 5), mover));
    }

    [Test]
    public void CanTraverse_SelfCell_True()
    {
        var entity = CreateEntity(Team.Player);
        _occupancy.Place(entity, new Vector3Int(3, 0, 3));
        Assert.IsTrue(_occupancy.CanTraverse(new Vector3Int(3, 0, 3), entity));
    }

    [Test]
    public void CanTraverse_AlliedCell_True()
    {
        var ally1 = CreateEntity(Team.Player);
        var ally2 = CreateEntity(Team.Player);
        _occupancy.Place(ally1, new Vector3Int(3, 0, 3));

        Assert.IsTrue(_occupancy.CanTraverse(new Vector3Int(3, 0, 3), ally2));
    }

    [Test]
    public void CanTraverse_NeutralCell_True()
    {
        var player = CreateEntity(Team.Player);
        var neutral = CreateEntity(Team.Neutral);
        _occupancy.Place(neutral, new Vector3Int(3, 0, 3));

        Assert.IsTrue(_occupancy.CanTraverse(new Vector3Int(3, 0, 3), player));
    }

    [Test]
    public void CanTraverse_EnemyCell_False()
    {
        var player = CreateEntity(Team.Player);
        var enemy = CreateEntity(Team.Enemy);
        _occupancy.Place(enemy, new Vector3Int(3, 0, 3));

        Assert.IsFalse(_occupancy.CanTraverse(new Vector3Int(3, 0, 3), player));
    }

    [Test]
    public void CanOccupy_EmptyCell_True()
    {
        var mover = CreateEntity(Team.Player);
        Assert.IsTrue(_occupancy.CanOccupy(new Vector3Int(5, 0, 5), mover));
    }

    [Test]
    public void CanOccupy_SelfCell_True()
    {
        var entity = CreateEntity(Team.Player);
        _occupancy.Place(entity, new Vector3Int(3, 0, 3));
        Assert.IsTrue(_occupancy.CanOccupy(new Vector3Int(3, 0, 3), entity));
    }

    [Test]
    public void CanOccupy_AlliedCell_False()
    {
        var ally1 = CreateEntity(Team.Player);
        var ally2 = CreateEntity(Team.Player);
        _occupancy.Place(ally1, new Vector3Int(3, 0, 3));

        Assert.IsFalse(_occupancy.CanOccupy(new Vector3Int(3, 0, 3), ally2));
    }

    [Test]
    public void CanOccupy_EnemyCell_False()
    {
        var player = CreateEntity(Team.Player);
        var enemy = CreateEntity(Team.Enemy);
        _occupancy.Place(enemy, new Vector3Int(3, 0, 3));

        Assert.IsFalse(_occupancy.CanOccupy(new Vector3Int(3, 0, 3), player));
    }

    [Test]
    public void GetFootprint_Medium_OneCell()
    {
        var cells = OccupancyMap.GetFootprint(new Vector3Int(3, 0, 3), 1);
        Assert.AreEqual(1, cells.Count);
        Assert.Contains(new Vector3Int(3, 0, 3), cells);
    }

    [Test]
    public void GetFootprint_Large_FourCells()
    {
        var cells = OccupancyMap.GetFootprint(new Vector3Int(2, 0, 2), 2);
        Assert.AreEqual(4, cells.Count);
        Assert.Contains(new Vector3Int(2, 0, 2), cells);
        Assert.Contains(new Vector3Int(3, 0, 2), cells);
        Assert.Contains(new Vector3Int(2, 0, 3), cells);
        Assert.Contains(new Vector3Int(3, 0, 3), cells);
    }

    [Test]
    public void GetFootprint_Huge_NineCells()
    {
        var cells = OccupancyMap.GetFootprint(new Vector3Int(0, 0, 0), 3);
        Assert.AreEqual(9, cells.Count);
    }

    [Test]
    public void CanOccupyFootprint_AllFree_True()
    {
        var entity = CreateEntity(Team.Player, CreatureSize.Large);
        Assert.IsTrue(_occupancy.CanOccupyFootprint(new Vector3Int(0, 0, 0), 2, entity));
    }

    [Test]
    public void CanOccupyFootprint_PartiallyBlocked_False()
    {
        var e1 = CreateEntity(Team.Player);
        var e2 = CreateEntity(Team.Player, CreatureSize.Large);
        _occupancy.Place(e1, new Vector3Int(1, 0, 1));

        Assert.IsFalse(_occupancy.CanOccupyFootprint(new Vector3Int(0, 0, 0), 2, e2));
    }

    [Test]
    public void GetOccupant_EmptyCell_ReturnsNone()
    {
        var result = _occupancy.GetOccupant(new Vector3Int(9, 0, 9));
        Assert.AreEqual(EntityHandle.None, result);
        Assert.IsFalse(result.IsValid);
    }

    [Test]
    public void GetOccupiedCells_ReturnsCorrectCells()
    {
        var entity = CreateEntity(Team.Player, CreatureSize.Large);
        _occupancy.Place(entity, new Vector3Int(1, 0, 1), 2);
        var cells = _occupancy.GetOccupiedCells(entity);
        Assert.AreEqual(4, cells.Count);
    }

    [Test]
    public void GetOccupiedCells_NotPlaced_ReturnsEmpty()
    {
        var entity = CreateEntity(Team.Player);
        var cells = _occupancy.GetOccupiedCells(entity);
        Assert.AreEqual(0, cells.Count);
    }
}
