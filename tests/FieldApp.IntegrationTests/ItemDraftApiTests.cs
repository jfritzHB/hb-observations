using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldApp.Application.Items;
using FieldApp.Domain.FieldItems;
using FieldApp.Infrastructure.Persistence;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>Server draft creation: authorization, validation, server-resolved company, idempotency and concurrency.</summary>
public sealed class ItemDraftApiTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private static readonly Guid _mosEisley = ProjectId(ProjectNumbers.MosEisley);
    private static readonly Guid _drywall = TradeId("DRY");

    private FieldAppFactory? _factory;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private FieldAppFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async ValueTask InitializeAsync()
    {
        if (sqlServer.ServerConnectionString is null)
        {
            return;
        }

        var container = SqlServerContainerFixture.UniqueContainerName("items");
        var database = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("ItemDrafts"), seedDemoData: true, CancellationToken, container);
        _factory = sqlServer.Factory(database, container);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Superintendent_creates_a_draft_with_server_resolved_company_and_audit()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);
        var draftId = Guid.NewGuid();

        // A smuggled responsibleCompanyId is not part of the contract and must be ignored.
        var response = await PostAsync(client, draftId.ToString(), new
        {
            clientDraftId = draftId,
            type = "PunchList",
            areaId = AreaId(ProjectNumbers.MosEisley, "Building A / Level 2 / Office 201"),
            locationDetail = "  North wall ",
            tradeId = _drywall,
            responsibleCompanyId = CompanyId(Companies.JawaGlass),
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken);
        Assert.Equal($"/api/v1/items/{item!.Id}", response.Headers.Location?.OriginalString);
        Assert.NotNull(response.Headers.ETag);
        Assert.Equal(LifecycleState.Draft, item.LifecycleState);
        Assert.Equal(CompanyId(Companies.DuneSeaDrywall), item.ResponsibleCompany.Id);
        Assert.Equal(Companies.DuneSeaDrywall, item.ResponsibleCompany.Name);
        Assert.Equal("Drywall", item.TradeName);
        Assert.Equal("Building A / Level 2 / Office 201", item.AreaPath);
        Assert.Equal("North wall", item.LocationDetail);
        Assert.Equal(ItemPriority.Normal, item.Priority);
        Assert.Empty(item.Photos);
        Assert.Equal($"/api/v1/items/{item.Id}/photos/uploads", item.Media.PhotoUploadsUrl);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldAppDbContext>();
        var audit = await db.AuditEvents.SingleAsync(e => e.EntityId == item.Id, CancellationToken);
        Assert.Equal("ItemDraftCreated", audit.Action);
        Assert.Equal(UserId(PersonaKeys.Superintendent), audit.ActorUserId);
    }

    [Fact]
    public async Task Location_detail_alone_is_accepted_and_creates_no_area()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);
        var areasBefore = await AreaCountAsync();

        var response = await PostAsync(client, Guid.NewGuid().ToString(), Body(Guid.NewGuid(), detail: "Unit 214"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(areasBefore, await AreaCountAsync());
    }

    [Fact]
    public async Task Missing_idempotency_key_is_rejected()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{_mosEisley}/items", Body(Guid.NewGuid()), CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Retried_creation_returns_the_same_item_and_key_reuse_for_another_request_conflicts()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);
        var draftId = Guid.NewGuid();
        var key = draftId.ToString();

        var first = await PostAsync(client, key, Body(draftId, detail: "Unit 214"));
        var retry = await PostAsync(client, key, Body(draftId, detail: "Unit 214"));
        var misuse = await PostAsync(client, key, Body(draftId, detail: "Unit 999"));
        var newKeySameDraft = await PostAsync(client, Guid.NewGuid().ToString(), Body(draftId, detail: "Unit 214"));

        var firstItem = await first.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken);
        var retryItem = await retry.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal("true", Assert.Single(retry.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(firstItem!.Id, retryItem!.Id);
        Assert.Equal(HttpStatusCode.Conflict, misuse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newKeySameDraft.StatusCode);
        Assert.Equal(1, await ItemCountAsync(draftId));
    }

    [Fact]
    public async Task Concurrent_duplicate_creation_produces_exactly_one_item()
    {
        sqlServer.SkipIfUnavailable();
        var draftId = Guid.NewGuid();
        var clients = Enumerable.Range(0, 8).Select(_ => Client(PersonaKeys.Superintendent)).ToList();

        var responses = await Task.WhenAll(clients.Select(client => PostAsync(client, draftId.ToString(), Body(draftId, detail: "Unit 214"))));
        var ids = await Task.WhenAll(responses.Select(async response =>
            (await response.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken))!.Id));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, await ItemCountAsync(draftId));
        clients.ForEach(client => client.Dispose());
    }

    [Theory]
    [InlineData("ROOF", "unmapped")]
    [InlineData("CONC", "disabled on the project")]
    [InlineData("GLZ", "mapped to an inactive company")]
    [InlineData("FIRE", "inactive trade")]
    public async Task Trades_not_available_for_capture_are_rejected(string tradeCode, string reason)
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);

        var response = await PostAsync(client, Guid.NewGuid().ToString(), Body(Guid.NewGuid(), detail: "Unit 214", tradeId: TradeId(tradeCode)));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{tradeCode} ({reason}) should be rejected.");
        await AssertValidationErrorAsync(response, "tradeId");
    }

    [Theory]
    [InlineData(ProjectNumbers.MosEisley, "Building B / Level 1 / Storage (Closed)")]
    [InlineData(ProjectNumbers.MosEisley, "Building C (Future Phase) / Level 1")]
    [InlineData(ProjectNumbers.Anchorhead, "Headworks")]
    public async Task Inactive_or_foreign_areas_are_rejected(string projectNumber, string path)
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);

        var response = await PostAsync(client, Guid.NewGuid().ToString(), Body(Guid.NewGuid(), areaId: AreaId(projectNumber, path)));

        await AssertValidationErrorAsync(response, "areaId");
    }

    [Fact]
    public async Task Location_detail_longer_than_120_characters_is_rejected()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client(PersonaKeys.Superintendent);

        var response = await PostAsync(client, Guid.NewGuid().ToString(), Body(Guid.NewGuid(), detail: new string('x', 121)));

        await AssertValidationErrorAsync(response, "locationDetail");
    }

    [Fact]
    public async Task Creation_applies_the_401_403_404_policy()
    {
        sqlServer.SkipIfUnavailable();
        using var anonymous = Client(null);
        using var tradePartner = Client(PersonaKeys.TradePartner);
        using var superintendent = Client(PersonaKeys.Superintendent);

        var unauthenticated = await PostAsync(anonymous, "k1", Body(Guid.NewGuid()));
        var forbidden = await PostAsync(tradePartner, "k2", Body(Guid.NewGuid()));
        var concealed = await superintendent.PostAsJsonAsync(
            $"/api/v1/projects/{ProjectId(ProjectNumbers.ToscheStation)}/items", Body(Guid.NewGuid()), CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
    }

    [Fact]
    public async Task A_draft_is_visible_only_to_its_creator()
    {
        sqlServer.SkipIfUnavailable();
        using var creator = Client(PersonaKeys.Superintendent);
        using var colleague = Client(PersonaKeys.ProjectManager);
        var created = await PostAsync(creator, Guid.NewGuid().ToString(), Body(Guid.NewGuid()));
        var item = await created.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken);

        var own = await creator.GetAsync($"/api/v1/items/{item!.Id}", CancellationToken);
        var other = await colleague.GetAsync($"/api/v1/items/{item.Id}", CancellationToken);
        var random = await creator.GetAsync($"/api/v1/items/{Guid.NewGuid()}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, random.StatusCode);
    }

    private HttpClient Client(string? persona) => Factory.CreateClientAs(persona);

    private static object Body(Guid draftId, string? detail = "Unit 214", Guid? tradeId = null, Guid? areaId = null) =>
        new { clientDraftId = draftId, type = "PunchList", areaId, locationDetail = detail, tradeId = tradeId ?? _drywall };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/projects/{_mosEisley}/items") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, CancellationToken);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"Expected a validation error for {field}.");
    }

    private async Task<int> ItemCountAsync(Guid clientDraftId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FieldAppDbContext>().FieldItems.CountAsync(item => item.ClientDraftId == clientDraftId, CancellationToken);
    }

    private async Task<int> AreaCountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FieldAppDbContext>().Areas.CountAsync(CancellationToken);
    }
}
