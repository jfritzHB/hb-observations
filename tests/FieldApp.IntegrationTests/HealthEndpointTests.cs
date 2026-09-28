using System.Net;
using System.Net.Http.Json;
using FieldApp.IntegrationTests.Infrastructure;

namespace FieldApp.IntegrationTests;

public sealed class HealthEndpointTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Live_returns_healthy_without_any_database_configuration()
    {
        await using var factory = new FieldAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task Ready_returns_503_when_database_is_not_configured()
    {
        await using var factory = new FieldAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthSummary>(CancellationToken);
        Assert.Equal("Unhealthy", body?.Status);
        Assert.Contains(body!.Checks, check => check is { Name: "database", Status: "Unhealthy" });
    }

    [Fact]
    public async Task Ready_returns_503_when_database_is_unreachable()
    {
        await using var factory = new FieldAppFactory
        {
            AppDbConnectionString = "Server=127.0.0.1,1;Database=FieldApp;User Id=sa;Password=unused;Connect Timeout=2;Encrypt=False",
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }

    internal sealed record HealthSummary(string Status, IReadOnlyList<HealthCheckSummary> Checks);

    internal sealed record HealthCheckSummary(string Name, string Status);
}
