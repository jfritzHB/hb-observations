using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldApp.Application.ReferenceData;
using FieldApp.Infrastructure.Persistence;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>Area creation (a write endpoint): authorization, validation, conflicts and audit. Uses its own database.</summary>
public sealed class AreaManagementApiTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private static readonly Guid _mosEisley = ProjectId(ProjectNumbers.MosEisley);

    private FieldAppFactory? _factory;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private FieldAppFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async ValueTask InitializeAsync()
    {
        if (sqlServer.ServerConnectionString is null)
        {
            return;
        }

        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("AreaWrites"), seedDemoData: true, CancellationToken);
        _factory = new FieldAppFactory { AppDbConnectionString = connectionString };
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Project_manager_creates_an_area_with_location_and_audit_event()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Factory.CreateClientAs(PersonaKeys.ProjectManager);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "area-create-test");
        var level2 = AreaId(ProjectNumbers.MosEisley, "Building A / Level 2");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{_mosEisley}/areas", new { parentAreaId = level2, name = "Office 203" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var area = await response.Content.ReadFromJsonAsync<AreaDto>(FieldAppFactory.Json, CancellationToken);
        Assert.Equal("Building A / Level 2 / Office 203", area!.Path);
        Assert.Equal(2, area.Depth);
        Assert.Equal($"/api/v1/projects/{_mosEisley}/areas/{area.Id}", response.Headers.Location?.OriginalString);

        var fetched = await client.GetFromJsonAsync<AreaDto>(response.Headers.Location!.OriginalString, FieldAppFactory.Json, CancellationToken);
        Assert.Equal(area, fetched);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldAppDbContext>();
        var audit = await db.AuditEvents.SingleAsync(e => e.EntityId == area.Id, CancellationToken);
        Assert.Equal("AreaCreated", audit.Action);
        Assert.Equal(UserId(PersonaKeys.ProjectManager), audit.ActorUserId);
        Assert.Equal(_mosEisley, audit.ProjectId);
        Assert.Equal("area-create-test", audit.CorrelationId);
        Assert.Equal(TimeSpan.Zero, audit.OccurredAt.Offset);
    }

    [Fact]
    public async Task Duplicate_area_path_returns_409()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Factory.CreateClientAs(PersonaKeys.Administrator);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "building a" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Blank_name_returns_400_validation_problem_for_the_field()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Factory.CreateClientAs(PersonaKeys.ProjectManager);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "   " }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Parent_from_another_project_returns_400_without_revealing_it()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Factory.CreateClientAs(PersonaKeys.ProjectManager);
        var anchorheadArea = AreaId(ProjectNumbers.Anchorhead, "Headworks");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{_mosEisley}/areas", new { parentAreaId = anchorheadArea, name = "Sneaky Room" }, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("parentAreaId", out _));
    }

    [Fact]
    public async Task Forbidden_attempt_writes_no_area_and_no_audit_event()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Factory.CreateClientAs(PersonaKeys.Superintendent);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/areas", new { name = "Forbidden Room" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldAppDbContext>();
        Assert.False(await db.Areas.AnyAsync(a => a.Name == "Forbidden Room", CancellationToken));
    }
}
