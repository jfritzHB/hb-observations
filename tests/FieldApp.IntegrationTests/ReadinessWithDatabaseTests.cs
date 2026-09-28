using System.Diagnostics;
using System.Net;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace FieldApp.IntegrationTests;

public sealed class ReadinessWithDatabaseTests(SqlServerContainerFixture sqlServer) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task Ready_returns_healthy_when_sql_server_is_reachable()
    {
        Assert.SkipWhen(sqlServer.ConnectionString is null, sqlServer.UnavailableReason ?? "SQL Server unavailable.");

        await using var factory = new FieldAppFactory { AppDbConnectionString = sqlServer.ConnectionString! };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_503_promptly_when_application_database_does_not_exist()
    {
        Assert.SkipWhen(sqlServer.ConnectionString is null, sqlServer.UnavailableReason ?? "SQL Server unavailable.");

        var missingDatabase = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { InitialCatalog = "FieldAppMissing" };
        await using var factory = new FieldAppFactory { AppDbConnectionString = missingDatabase.ConnectionString };
        using var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Fails fast rather than waiting out the 5 second check timeout on an internal connect retry.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"Readiness took {stopwatch.Elapsed}.");
    }
}
