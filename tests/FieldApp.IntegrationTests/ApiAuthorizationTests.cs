using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Memberships;
using FieldApp.Infrastructure.Seeding;
using FieldApp.IntegrationTests.Infrastructure;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>
/// Project membership authorization and the 401/403/404 concealment policy, against the seeded synthetic data
/// (read-only). Knowing a project ID must never grant access.
/// </summary>
public sealed class ApiAuthorizationTests(SqlServerContainerFixture sqlServer) : IAsyncDisposable
{
    private static readonly Guid _mosEisley = ProjectId(ProjectNumbers.MosEisley);
    private static readonly Guid _anchorhead = ProjectId(ProjectNumbers.Anchorhead);
    private static readonly Guid _tosche = ProjectId(ProjectNumbers.ToscheStation);

    private FieldAppFactory? _factory;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static TheoryData<string> ProjectScopedPaths => new()
    {
        "/api/v1/projects/{0}",
        "/api/v1/projects/{0}/areas",
        "/api/v1/projects/{0}/trades",
        "/api/v1/projects/{0}/companies",
    };

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/projects")]
    [InlineData("/api/v1/projects/6cdf5bd1-d4b0-801a-9be9-c33f22ba3111/areas")]
    public async Task Unauthenticated_requests_return_401_problem_details(string path)
    {
        using var client = Client(persona: null);

        var response = await client.GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_persona_is_not_authenticated()
    {
        using var client = Client("darth-intruder");

        var response = await client.GetAsync("/api/v1/projects", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_write_returns_401_before_anything_else()
    {
        using var client = Client(persona: null);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "Roof" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Superintendent_sees_only_member_projects()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var projects = await client.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects", FieldAppFactory.Json, CancellationToken);

        Assert.Equal([ProjectNumbers.MosEisley, ProjectNumbers.Anchorhead], projects!.Select(p => p.Number));
        Assert.All(projects!, project => Assert.Equal([ProjectRoles.Superintendent], project.MyRoles));
    }

    [Fact]
    public async Task Provisioned_user_without_memberships_sees_no_projects()
    {
        using var client = Client(PersonaKeys.Unassigned);

        var projects = await client.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects", FieldAppFactory.Json, CancellationToken);

        Assert.Empty(projects!);
    }

    [Theory]
    [MemberData(nameof(ProjectScopedPaths))]
    public async Task Knowing_a_project_id_without_membership_returns_404(string pathFormat)
    {
        using var client = Client(PersonaKeys.Superintendent);

        var response = await client.GetAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, pathFormat, _tosche), CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Concealed_project_is_indistinguishable_from_a_nonexistent_project()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var concealed = await ProblemAsync(await client.GetAsync($"/api/v1/projects/{_tosche}", CancellationToken));
        var missing = await ProblemAsync(await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}", CancellationToken));

        Assert.Equal(missing.Status, concealed.Status);
        Assert.Equal(missing.Title, concealed.Title);
        Assert.Equal(missing.Type, concealed.Type);
    }

    [Fact]
    public async Task Inactive_membership_grants_no_visibility()
    {
        using var client = Client(PersonaKeys.ProjectManager);

        var projects = await client.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects", FieldAppFactory.Json, CancellationToken);
        var response = await client.GetAsync($"/api/v1/projects/{_anchorhead}", CancellationToken);

        Assert.DoesNotContain(projects!, p => p.Id == _anchorhead);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Visible_project_but_forbidden_management_operation_returns_403()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "Roof Deck" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Forbidden_operation_on_an_invisible_project_returns_404_not_403()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_tosche}/areas", new { name = "Roof Deck" }, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Trade_partner_reads_own_project_reference_data_but_cannot_manage_or_see_other_projects()
    {
        using var client = Client(PersonaKeys.TradePartner);

        var trades = await client.GetAsync($"/api/v1/projects/{_mosEisley}/trades", CancellationToken);
        var create = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "Roof Deck" }, CancellationToken);
        var otherProject = await client.GetAsync($"/api/v1/projects/{_anchorhead}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, trades.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherProject.StatusCode);
    }

    [Fact]
    public async Task Member_can_read_project_details_with_roles()
    {
        using var client = Client(PersonaKeys.Administrator);

        var project = await client.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{_tosche}", FieldAppFactory.Json, CancellationToken);

        Assert.Equal("Tosche Station Retrofit", project!.Name);
        Assert.Equal([ProjectRoles.Administrator], project.MyRoles);
    }

    [Fact]
    public async Task Me_returns_the_resolved_application_user()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/v1/me", FieldAppFactory.Json, CancellationToken);

        Assert.Equal(UserId(PersonaKeys.Superintendent), me!.Id);
        Assert.Equal("Owen Lars (Superintendent)", me.DisplayName);
    }

    [Fact]
    public async Task Capture_trades_include_responsible_company_and_exclude_unavailable_mappings()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var trades = await client.GetFromJsonAsync<List<CaptureTradeDto>>($"/api/v1/projects/{_mosEisley}/trades", FieldAppFactory.Json, CancellationToken);

        Assert.Equal(
            ["Doors & Hardware", "Drywall", "Electrical", "Flooring", "HVAC", "Painting", "Plumbing"],
            trades!.Select(trade => trade.Name));

        var drywall = trades!.Single(trade => trade.Code == "DRY");
        Assert.Equal(TradeId("DRY"), drywall.TradeId);
        Assert.Equal(new ResponsibleCompanyDto(CompanyId(Companies.DuneSeaDrywall), Companies.DuneSeaDrywall), drywall.ResponsibleCompany);

        // Roofing (unmapped), Concrete (disabled on project), Glazing (inactive company), Fireproofing (inactive trade).
        Assert.DoesNotContain(trades!, trade => trade.Code is "ROOF" or "CONC" or "GLZ" or "FIRE");
    }

    [Fact]
    public async Task Companies_filtered_by_trade_return_the_single_responsible_company()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var companies = await client.GetFromJsonAsync<List<ProjectCompanyDto>>(
            $"/api/v1/projects/{_mosEisley}/companies?tradeId={TradeId("DRY")}", FieldAppFactory.Json, CancellationToken);

        var company = Assert.Single(companies!);
        Assert.Equal(Companies.DuneSeaDrywall, company.Name);
    }

    [Fact]
    public async Task Areas_are_hierarchical_and_exclude_inactive_branches()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var areas = await client.GetFromJsonAsync<List<AreaDto>>($"/api/v1/projects/{_mosEisley}/areas", FieldAppFactory.Json, CancellationToken);

        var paths = areas!.Select(area => area.Path).ToList();
        Assert.Equal(13, paths.Count);
        Assert.Equal("Building A", paths[0]);
        Assert.Equal("Building A / Level 1", paths[1]);
        Assert.Equal("Building A / Level 1 / Lobby", paths[2]);
        Assert.True(paths.IndexOf("Building A / Level 2 / Office 201") < paths.IndexOf("Building B"));
        Assert.DoesNotContain(paths, path => path.Contains("Storage", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.StartsWith("Building C", StringComparison.Ordinal));
        Assert.All(areas!, area => Assert.True(area.IsActive));
    }

    [Fact]
    public async Task Area_search_matches_all_terms_in_the_full_path()
    {
        using var client = Client(PersonaKeys.Superintendent);

        var areas = await client.GetFromJsonAsync<List<AreaDto>>(
            $"/api/v1/projects/{_mosEisley}/areas?search=office%2020&recent=true", FieldAppFactory.Json, CancellationToken);

        Assert.Equal(
            ["Building A / Level 2 / Office 201", "Building A / Level 2 / Office 202"],
            areas!.Select(area => area.Path));
    }

    [Fact]
    public async Task Get_area_of_another_project_is_not_found()
    {
        using var client = Client(PersonaKeys.Administrator);
        var anchorheadArea = AreaId(ProjectNumbers.Anchorhead, "Clarifier 1");

        var ownProject = await client.GetAsync($"/api/v1/projects/{_anchorhead}/areas/{anchorheadArea}", CancellationToken);
        var wrongProject = await client.GetAsync($"/api/v1/projects/{_mosEisley}/areas/{anchorheadArea}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ownProject.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongProject.StatusCode);
    }

    private HttpClient Client(string? persona)
    {
        sqlServer.SkipIfUnavailable();
        _factory ??= new FieldAppFactory { AppDbConnectionString = sqlServer.SeededConnectionString! };
        return _factory.CreateClientAs(persona);
    }

    private static async Task<(int Status, string? Title, string? Type)> ProblemAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var root = problem.RootElement;
        return (root.GetProperty("status").GetInt32(), root.GetProperty("title").GetString(), root.GetProperty("type").GetString());
    }
}
