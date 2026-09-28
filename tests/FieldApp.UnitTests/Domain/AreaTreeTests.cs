using FieldApp.Domain.Areas;
using FieldApp.Domain.Common;

namespace FieldApp.UnitTests.Domain;

public sealed class AreaTreeTests
{
    private static readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public void Child_path_is_flattened_from_ancestors()
    {
        var tree = new AreaTree(_projectId, []);
        var building = tree.Add(Guid.NewGuid(), null, "Building A");
        var level = tree.Add(Guid.NewGuid(), building.Id, "Level 2");
        var room = tree.Add(Guid.NewGuid(), level.Id, "  Office 201  ");

        Assert.Equal("Building A / Level 2 / Office 201", room.Path);
        Assert.Equal("Office 201", room.Name);
        Assert.Equal(2, tree.DepthOf(room.Id));
        Assert.Equal(level.Id, room.ParentAreaId);
    }

    [Fact]
    public void Moving_an_area_beneath_its_own_descendant_is_rejected()
    {
        var tree = new AreaTree(_projectId, []);
        var building = tree.Add(Guid.NewGuid(), null, "Building A");
        var level = tree.Add(Guid.NewGuid(), building.Id, "Level 1");
        var room = tree.Add(Guid.NewGuid(), level.Id, "Lobby");

        var ex = Assert.Throws<DomainException>(() => tree.Move(building.Id, room.Id));

        Assert.Contains("descendants", ex.Message, StringComparison.Ordinal);
        Assert.Null(building.ParentAreaId);
    }

    [Fact]
    public void Moving_an_area_beneath_itself_is_rejected()
    {
        var tree = new AreaTree(_projectId, []);
        var building = tree.Add(Guid.NewGuid(), null, "Building A");

        Assert.Throws<DomainException>(() => tree.Move(building.Id, building.Id));
    }

    [Fact]
    public void Moving_an_area_recomputes_the_whole_subtree_path()
    {
        var tree = new AreaTree(_projectId, []);
        var buildingA = tree.Add(Guid.NewGuid(), null, "Building A");
        var buildingB = tree.Add(Guid.NewGuid(), null, "Building B");
        var level = tree.Add(Guid.NewGuid(), buildingA.Id, "Level 1");
        var room = tree.Add(Guid.NewGuid(), level.Id, "Lobby");

        tree.Move(level.Id, buildingB.Id);

        Assert.Equal("Building B / Level 1", level.Path);
        Assert.Equal("Building B / Level 1 / Lobby", room.Path);
    }

    [Fact]
    public void Loading_areas_that_form_a_cycle_is_rejected()
    {
        // Two areas that are each other's parent can only come from corrupt stored data.
        var projectId = Guid.NewGuid();
        var firstAsRoot = Area.Create(Guid.NewGuid(), projectId, null, "First", 10);
        var second = Area.Create(Guid.NewGuid(), projectId, firstAsRoot, "Second", 20);
        var firstUnderSecond = Area.Create(firstAsRoot.Id, projectId, second, "First", 10);

        var ex = Assert.Throws<DomainException>(() => new AreaTree(projectId, [firstUnderSecond, second]));

        Assert.Contains("cycle", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Loading_an_area_whose_parent_is_missing_is_rejected()
    {
        var projectId = Guid.NewGuid();
        var parent = Area.Create(Guid.NewGuid(), projectId, null, "Building A", 10);
        var child = Area.Create(Guid.NewGuid(), projectId, parent, "Level 1", 10);

        Assert.Throws<DomainException>(() => new AreaTree(projectId, [child]));
    }

    [Fact]
    public void A_parent_from_another_project_is_rejected()
    {
        var otherProjectParent = Area.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Elsewhere", 10);

        Assert.Throws<DomainException>(() => Area.Create(Guid.NewGuid(), _projectId, otherProjectParent, "Room", 10));
    }

    [Fact]
    public void Adding_under_an_unknown_parent_is_rejected()
    {
        var tree = new AreaTree(_projectId, []);

        var ex = Assert.Throws<DomainException>(() => tree.Add(Guid.NewGuid(), Guid.NewGuid(), "Room"));

        Assert.Equal("ParentAreaId", ex.Field);
    }

    [Fact]
    public void Duplicate_full_path_is_a_conflict_case_insensitively()
    {
        var tree = new AreaTree(_projectId, []);
        var building = tree.Add(Guid.NewGuid(), null, "Building A");
        tree.Add(Guid.NewGuid(), building.Id, "Lobby");

        Assert.Throws<DomainConflictException>(() => tree.Add(Guid.NewGuid(), building.Id, "LOBBY"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Line\nbreak")]
    public void Invalid_names_are_rejected(string name)
    {
        var tree = new AreaTree(_projectId, []);

        var ex = Assert.Throws<DomainException>(() => tree.Add(Guid.NewGuid(), null, name));

        Assert.Equal("Name", ex.Field);
    }

    [Fact]
    public void Name_longer_than_limit_is_rejected()
    {
        var tree = new AreaTree(_projectId, []);

        Assert.Throws<DomainException>(() => tree.Add(Guid.NewGuid(), null, new string('x', Area.NameMaxLength + 1)));
    }

    [Fact]
    public void Selectable_requires_the_area_and_every_ancestor_to_be_active()
    {
        var tree = new AreaTree(_projectId, []);
        var building = tree.Add(Guid.NewGuid(), null, "Building C");
        var level = tree.Add(Guid.NewGuid(), building.Id, "Level 1");
        var room = tree.Add(Guid.NewGuid(), level.Id, "Room 1");

        Assert.True(tree.IsSelectable(room.Id));

        building.Deactivate();

        Assert.False(tree.IsSelectable(building.Id));
        Assert.False(tree.IsSelectable(level.Id));
        Assert.False(tree.IsSelectable(room.Id));
        Assert.False(tree.IsSelectable(Guid.NewGuid()));
    }

    [Fact]
    public void Display_order_is_depth_first_by_sort_order_then_name()
    {
        var tree = new AreaTree(_projectId, []);
        var buildingB = tree.Add(Guid.NewGuid(), null, "Building B", sortOrder: 20);
        var buildingA = tree.Add(Guid.NewGuid(), null, "Building A", sortOrder: 10);
        tree.Add(Guid.NewGuid(), buildingA.Id, "Zeta", sortOrder: 5);
        tree.Add(Guid.NewGuid(), buildingA.Id, "Alpha", sortOrder: 5);
        tree.Add(Guid.NewGuid(), buildingB.Id, "Level 1");

        var paths = tree.InDisplayOrder().Select(area => area.Path).ToList();

        Assert.Equal(
            ["Building A", "Building A / Alpha", "Building A / Zeta", "Building B", "Building B / Level 1"],
            paths);
    }

    [Fact]
    public void Default_sort_order_follows_existing_siblings()
    {
        var tree = new AreaTree(_projectId, []);
        tree.Add(Guid.NewGuid(), null, "First", sortOrder: 40);

        var next = tree.Add(Guid.NewGuid(), null, "Second");

        Assert.Equal(50, next.SortOrder);
    }
}
