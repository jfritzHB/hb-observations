using System.Net;
using System.Net.Http.Json;
using FieldApp.Api.Endpoints;
using FieldApp.IntegrationTests.Infrastructure;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>The development persona stub must never be usable outside the Development environment.</summary>
public sealed class DevelopmentAuthenticationSafetyTests(SqlServerContainerFixture sqlServer)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Production_ignores_the_persona_header()
    {
        sqlServer.SkipIfUnavailable();
        await using var factory = new FieldAppFactory
        {
            EnvironmentName = "Production",
            AppDbConnectionString = sqlServer.SeededConnectionString!,
        };
        using var client = factory.CreateClientAs(PersonaKeys.Administrator);

        var response = await client.GetAsync("/api/v1/projects", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_does_not_expose_the_persona_list()
    {
        await using var factory = new FieldAppFactory { EnvironmentName = "Production" };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dev/personas", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Configuring_persona_authentication_outside_development_prevents_startup(string environment)
    {
        using var factory = new FieldAppFactory
        {
            EnvironmentName = environment,
            Settings = new Dictionary<string, string?> { ["Authentication:Mode"] = "Development" },
        };

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("only permitted in the Development environment", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_lists_the_synthetic_personas_anonymously()
    {
        await using var factory = new FieldAppFactory();
        using var client = factory.CreateClient();

        var personas = await client.GetFromJsonAsync<List<DevelopmentPersonaDto>>("/api/v1/dev/personas", FieldAppFactory.Json, CancellationToken);

        Assert.Equal(
            [PersonaKeys.Superintendent, PersonaKeys.ProjectManager, PersonaKeys.Administrator, PersonaKeys.TradePartner, PersonaKeys.Unassigned],
            personas!.Select(persona => persona.Key));
    }

    [Fact]
    public async Task Authenticated_identity_without_an_application_user_is_forbidden()
    {
        sqlServer.SkipIfUnavailable();

        // Migrated but not seeded: the persona authenticates, but no application user is linked to it.
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("NoUsers"), seedDemoData: false, CancellationToken);
        await using var factory = new FieldAppFactory { AppDbConnectionString = connectionString };
        using var client = factory.CreateClientAs(PersonaKeys.Superintendent);

        var response = await client.GetAsync("/api/v1/projects", CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
