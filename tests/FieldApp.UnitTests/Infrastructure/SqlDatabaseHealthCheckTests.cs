using FieldApp.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldApp.UnitTests.Infrastructure;

public sealed class SqlDatabaseHealthCheckTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_connection_string_is_unhealthy(string? connectionString)
    {
        var check = CreateCheck(connectionString);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not configured", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_connection_string_is_unhealthy_and_not_echoed()
    {
        const string malformed = "this is not a connection string; Password=do-not-echo";
        var check = CreateCheck(malformed);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("do-not-echo", result.Description, StringComparison.Ordinal);
    }

    private static SqlDatabaseHealthCheck CreateCheck(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlDatabaseHealthCheck.ConnectionStringName}"] = connectionString,
            })
            .Build();

        return new SqlDatabaseHealthCheck(configuration);
    }
}
