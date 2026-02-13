using NUnit.Framework;
using PF2e.Core;

[TestFixture]
public class EntityRegistryTests
{
    [Test]
    public void Register_AssignsUniqueHandles()
    {
        var reg = new EntityRegistry();
        var h1 = reg.Register(new EntityData { Name = "A" });
        var h2 = reg.Register(new EntityData { Name = "B" });
        Assert.AreNotEqual(h1, h2);
        Assert.IsTrue(h1.IsValid);
        Assert.IsTrue(h2.IsValid);
    }

    [Test]
    public void Register_SetsHandleOnData()
    {
        var reg = new EntityRegistry();
        var data = new EntityData { Name = "Test" };
        var handle = reg.Register(data);
        Assert.AreEqual(handle, data.Handle);
    }

    [Test]
    public void Get_ReturnsData()
    {
        var reg = new EntityRegistry();
        var data = new EntityData { Name = "Test" };
        var handle = reg.Register(data);
        Assert.AreSame(data, reg.Get(handle));
    }

    [Test]
    public void Get_InvalidHandle_ReturnsNull()
    {
        var reg = new EntityRegistry();
        Assert.IsNull(reg.Get(EntityHandle.None));
        Assert.IsNull(reg.Get(new EntityHandle(999)));
    }

    [Test]
    public void Unregister_RemovesEntity()
    {
        var reg = new EntityRegistry();
        var handle = reg.Register(new EntityData { Name = "Test" });
        reg.Unregister(handle);
        Assert.IsFalse(reg.Exists(handle));
        Assert.AreEqual(0, reg.Count);
    }

    [Test]
    public void GetByTeam_FiltersCorrectly()
    {
        var reg = new EntityRegistry();
        reg.Register(new EntityData { Name = "P1", Team = Team.Player });
        reg.Register(new EntityData { Name = "P2", Team = Team.Player });
        reg.Register(new EntityData { Name = "E1", Team = Team.Enemy });

        var players = reg.GetByTeam(Team.Player);
        Assert.AreEqual(2, players.Count);

        var enemies = reg.GetByTeam(Team.Enemy);
        Assert.AreEqual(1, enemies.Count);
    }

    [Test]
    public void Count_TracksCorrectly()
    {
        var reg = new EntityRegistry();
        Assert.AreEqual(0, reg.Count);

        var h1 = reg.Register(new EntityData { Name = "A" });
        Assert.AreEqual(1, reg.Count);

        reg.Register(new EntityData { Name = "B" });
        Assert.AreEqual(2, reg.Count);

        reg.Unregister(h1);
        Assert.AreEqual(1, reg.Count);
    }
}
