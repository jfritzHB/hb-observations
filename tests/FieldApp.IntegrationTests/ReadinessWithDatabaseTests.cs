using System.Diagnostics;
using System.Net;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace FieldApp.IntegrationTests;

public sealed class ReadinessWithDatabaseTests(SqlServerContainerFixture sqlServer)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ready_returns_healthy_when_database_is_migrated_and_photo_storage_is_available()
    {
        sqlServer.SkipIfUnavailable();

        await using var factory = sqlServer.SeededFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_503_when_database_exists_but_is_not_migrated()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateEmptyDatabaseAsync(SqlServerContainerFixture.UniqueDatabaseName("Unmigrated"), CancellationToken);

        await using var factory = new FieldAppFactory { AppDbConnectionString = connectionString };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_503_promptly_when_application_database_does_not_exist()
    {
        sqlServer.SkipIfUnavailable();

        var missingDatabase = new SqlConnectionStringBuilder(sqlServer.ServerConnectionString) { InitialCatalog = "FieldAppMissing" };
        await using var factory = new FieldAppFactory { AppDbConnectionString = missingDatabase.ConnectionString };
        using var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/health/ready", CancellationToken);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Fails fast rather than waiting out the 5 second check timeout on an internal connect retry.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"Readiness took {stopwatch.Elapsed}.");
    }
}
