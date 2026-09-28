using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using Microsoft.Extensions.Time.Testing;

namespace FieldApp.UnitTests.Application;

public sealed class ReferenceDataUseCaseTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly InMemoryReferenceData _data = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Capture_trades_resolve_responsible_company_and_exclude_unavailable_mappings()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);
        var active = Company.Create(Guid.NewGuid(), "Synthetic Drywall Co.");
        var inactive = Company.Create(Guid.NewGuid(), "Synthetic Glass Co.");
        inactive.Deactivate();

        AddMapping(projectId, "DRY", "Drywall", active);
        AddMapping(projectId, "ROOF", "Roofing", company: null);
        AddMapping(projectId, "GLZ", "Glazing", inactive);
        AddMapping(projectId, "CONC", "Concrete", active, disable: true);
        AddMapping(projectId, "FIRE", "Fireproofing", active, deactivateTrade: true);

        var result = await Trades().ListForCaptureAsync(projectId, CancellationToken);

        var trade = Assert.Single(result.Value!);
        Assert.Equal("Drywall", trade.Name);
        Assert.Equal(new ResponsibleCompanyDto(active.Id, "Synthetic Drywall Co."), trade.ResponsibleCompany);
    }

    [Fact]
    public async Task Companies_can_be_filtered_by_trade()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);
        var drywall = Company.Create(Guid.NewGuid(), "Synthetic Drywall Co.");
        var paint = Company.Create(Guid.NewGuid(), "Synthetic Paint Co.");
        var drywallTrade = AddMapping(projectId, "DRY", "Drywall", drywall);
        AddMapping(projectId, "PNT", "Painting", paint);

        var all = await Trades().ListCompaniesAsync(projectId, null, CancellationToken);
        var filtered = await Trades().ListCompaniesAsync(projectId, drywallTrade.Id, CancellationToken);

        Assert.Equal(2, all.Value!.Count);
        var company = Assert.Single(filtered.Value!);
        Assert.Equal(drywall.Id, company.Id);
        Assert.Equal([drywallTrade.Id], company.TradeIds);
    }

    [Fact]
    public async Task Reference_data_of_an_invisible_project_is_not_found()
    {
        var projectId = _data.AddProjectWithMember(Guid.NewGuid(), ProjectRoles.Administrator);

        Assert.Equal(ErrorKind.NotFound, (await Trades().ListForCaptureAsync(projectId, CancellationToken)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Areas().ListSelectableAsync(projectId, null, CancellationToken)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Projects().GetAsync(projectId, CancellationToken)).Error!.Kind);
    }

    [Fact]
    public async Task Selectable_areas_exclude_inactive_branches_and_match_every_search_term()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);
        var tree = new AreaTree(projectId, []);
        var buildingA = tree.Add(Guid.NewGuid(), null, "Building A");
        var level2 = tree.Add(Guid.NewGuid(), buildingA.Id, "Level 2");
        tree.Add(Guid.NewGuid(), level2.Id, "Office 201");
        tree.Add(Guid.NewGuid(), level2.Id, "Office 202");
        var closed = tree.Add(Guid.NewGuid(), level2.Id, "Office 203");
        closed.Deactivate();
        var buildingC = tree.Add(Guid.NewGuid(), null, "Building C");
        tree.Add(Guid.NewGuid(), buildingC.Id, "Office 301");
        buildingC.Deactivate();
        _data.Areas.AddRange(tree.Areas);

        var all = await Areas().ListSelectableAsync(projectId, null, CancellationToken);
        var searched = await Areas().ListSelectableAsync(projectId, "  level   OFFICE 20 ", CancellationToken);

        Assert.Equal(
            ["Building A", "Building A / Level 2", "Building A / Level 2 / Office 201", "Building A / Level 2 / Office 202"],
            all.Value!.Select(area => area.Path));
        Assert.Equal(
            ["Building A / Level 2 / Office 201", "Building A / Level 2 / Office 202"],
            searched.Value!.Select(area => area.Path));
        Assert.Equal(2, searched.Value![0].Depth);
    }

    [Fact]
    public async Task Overlong_search_is_a_validation_error()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);

        var result = await Areas().ListSelectableAsync(projectId, new string('a', AreaService.SearchMaxLength + 1), CancellationToken);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task Creating_an_area_persists_it_with_an_audit_event()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.ProjectManager);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        var result = await Areas(clock).CreateAsync(projectId, new CreateAreaRequest(null, "Parking Garage", null), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Parking Garage", Assert.Single(_data.Areas).Path);
        var audit = Assert.Single(_data.AuditEvents);
        Assert.Equal("AreaCreated", audit.Action);
        Assert.Equal(_userId, audit.ActorUserId);
        Assert.Equal(projectId, audit.ProjectId);
        Assert.Equal(result.Value!.Id, audit.EntityId);
        Assert.Equal("unit-test-correlation", audit.CorrelationId);
        Assert.Equal(clock.GetUtcNow(), audit.OccurredAt);
        Assert.Contains("Parking Garage", audit.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Superintendent_cannot_create_areas()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);

        var result = await Areas().CreateAsync(projectId, new CreateAreaRequest(null, "Roof", null), CancellationToken);

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Empty(_data.Areas);
        Assert.Empty(_data.AuditEvents);
        Assert.Equal(0, _data.SaveCount);
    }

    [Theory]
    [InlineData(null, "name")]
    [InlineData("", "name")]
    public async Task Invalid_area_name_is_a_validation_error_on_the_field(string? name, string field)
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Administrator);

        var result = await Areas().CreateAsync(projectId, new CreateAreaRequest(null, name, null), CancellationToken);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.True(result.Error.ValidationErrors!.ContainsKey(field));
    }

    [Fact]
    public async Task Unknown_parent_is_a_validation_error_on_parent_area_id()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Administrator);

        var result = await Areas().CreateAsync(projectId, new CreateAreaRequest(Guid.NewGuid(), "Room", null), CancellationToken);

        Assert.True(result.Error!.ValidationErrors!.ContainsKey("parentAreaId"));
    }

    [Fact]
    public async Task Duplicate_area_path_is_a_conflict()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.ProjectManager);
        await Areas().CreateAsync(projectId, new CreateAreaRequest(null, "Roof", null), CancellationToken);

        var result = await Areas().CreateAsync(projectId, new CreateAreaRequest(null, "roof", null), CancellationToken);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task Concurrent_duplicate_detected_by_the_database_is_a_conflict()
    {
        var projectId = _data.AddProjectWithMember(_userId, ProjectRoles.ProjectManager);
        _data.FailNextSaveWithDuplicateKey = true;

        var result = await Areas().CreateAsync(projectId, new CreateAreaRequest(null, "Roof", null), CancellationToken);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task Project_list_contains_only_member_projects_with_their_roles()
    {
        _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent | ProjectRoles.ProjectManager);
        _data.AddProjectWithMember(Guid.NewGuid(), ProjectRoles.Administrator);

        var projects = await Projects().ListMineAsync(CancellationToken);

        var project = Assert.Single(projects);
        Assert.Equal([ProjectRoles.Superintendent, ProjectRoles.ProjectManager], project.MyRoles);
    }

    private Trade AddMapping(Guid projectId, string code, string name, Company? company, bool disable = false, bool deactivateTrade = false)
    {
        var trade = Trade.Create(Guid.NewGuid(), code, name);
        if (deactivateTrade)
        {
            trade.Deactivate();
        }

        var projectTrade = ProjectTrade.Create(Guid.NewGuid(), projectId, trade.Id, company?.Id);
        if (disable)
        {
            projectTrade.Disable();
        }

        _data.ProjectTrades.Add(new ProjectTradeMapping(projectTrade, trade, company));
        return trade;
    }

    private ProjectAuthorizer Authorizer() => new(_data, new FakeCurrentUser(_userId));

    private TradeQueries Trades() => new(_data, Authorizer());

    private ProjectQueries Projects() => new(_data, Authorizer(), new FakeCurrentUser(_userId));

    private AreaService Areas(TimeProvider? clock = null) =>
        new(_data, _data, _data, _data, Authorizer(), new FakeCurrentUser(_userId), new FakeCorrelation(), clock ?? TimeProvider.System);
}
