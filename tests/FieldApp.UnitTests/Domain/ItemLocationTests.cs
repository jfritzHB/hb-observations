using FieldApp.Domain.Areas;
using FieldApp.Domain.Capture;
using FieldApp.Domain.Common;

namespace FieldApp.UnitTests.Domain;

public sealed class ItemLocationTests
{
    private static readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public void Structured_area_alone_is_a_meaningful_location_and_snapshots_the_path()
    {
        var tree = new AreaTree(_projectId, []);
        var level = tree.Add(Guid.NewGuid(), tree.Add(Guid.NewGuid(), null, "Building A").Id, "Level 2");

        var location = ItemLocation.Create(level, null);

        Assert.Equal(level.Id, location.AreaId);
        Assert.Equal("Building A / Level 2", location.AreaPathSnapshot);
        Assert.Null(location.LocationDetail);
        Assert.True(location.IsMeaningful);
    }

    [Fact]
    public void Location_detail_alone_is_a_meaningful_location()
    {
        var location = ItemLocation.Create(null, "  Unit 214 ");

        Assert.Null(location.AreaId);
        Assert.Equal("Unit 214", location.LocationDetail);
        Assert.True(location.IsMeaningful);
    }

    [Fact]
    public void Area_and_detail_can_be_combined()
    {
        var office = Area.Create(Guid.NewGuid(), _projectId, null, "Office 201", 10);

        var location = ItemLocation.Create(office, "North wall");

        Assert.Equal(office.Id, location.AreaId);
        Assert.Equal("North wall", location.LocationDetail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_area_and_blank_detail_is_allowed_for_a_draft_but_not_meaningful(string? detail)
    {
        var location = ItemLocation.Create(null, detail);

        Assert.False(location.IsMeaningful);
        Assert.Throws<DomainException>(() => location.EnsureMeaningful());
    }

    [Fact]
    public void Detail_longer_than_120_characters_is_rejected()
    {
        Assert.Equal("x".PadRight(120, 'x'), ItemLocation.Create(null, new string('x', 120)).LocationDetail);

        var ex = Assert.Throws<DomainException>(() => ItemLocation.Create(null, new string('x', 121)));

        Assert.Equal(nameof(ItemLocation.LocationDetail), ex.Field);
    }

    [Fact]
    public void Inactive_area_is_rejected()
    {
        var closed = Area.Create(Guid.NewGuid(), _projectId, null, "Storage (Closed)", 10);
        closed.Deactivate();

        Assert.Throws<DomainException>(() => ItemLocation.Create(closed, null));
    }

    [Fact]
    public void Creating_a_location_from_detail_never_touches_the_area_hierarchy()
    {
        var tree = new AreaTree(_projectId, []);
        tree.Add(Guid.NewGuid(), null, "Building A");

        ItemLocation.Create(null, "Unit 214");

        Assert.Single(tree.Areas);
    }
}
