using Testcontainers.MsSql;

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a disposable SQL Server container. When Docker is unavailable, tests using this fixture are
/// skipped locally; set <c>FIELDAPP_REQUIRE_DOCKER=true</c> (as CI does) to make that a failure instead.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public string? UnavailableReason { get; private set; }

    private static bool DockerRequired =>
        string.Equals(Environment.GetEnvironmentVariable("FIELDAPP_REQUIRE_DOCKER"), "true", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder(Image).Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex) when (!DockerRequired)
        {
            UnavailableReason = $"SQL Server container could not start: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
