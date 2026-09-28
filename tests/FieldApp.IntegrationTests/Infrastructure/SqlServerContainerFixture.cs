using FieldApp.Api.Database;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>
/// One disposable SQL Server container for the whole test assembly, with a database migrated and seeded with the
/// synthetic demo data through the real <c>migrate --seed-demo-data</c> command. Tests that write data create
/// their own database so read-only tests stay deterministic under parallel execution.
/// When Docker is unavailable, dependent tests are skipped locally; set <c>FIELDAPP_REQUIRE_DOCKER=true</c>
/// (as CI does) to make that a failure instead.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;

    public string? ServerConnectionString { get; private set; }

    /// <summary>Migrated and seeded; treat as read-only.</summary>
    public string? SeededConnectionString { get; private set; }

    public string? UnavailableReason { get; private set; }

    private static bool DockerRequired =>
        string.Equals(Environment.GetEnvironmentVariable("FIELDAPP_REQUIRE_DOCKER"), "true", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder(Image).Build();
            await _container.StartAsync();
            ServerConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex) when (!DockerRequired)
        {
            UnavailableReason = $"SQL Server container could not start: {ex.Message}";
            return;
        }

        SeededConnectionString = await CreateMigratedDatabaseAsync("FieldAppSeeded", seedDemoData: true, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public void SkipIfUnavailable() =>
        Assert.SkipWhen(ServerConnectionString is null, UnavailableReason ?? "SQL Server unavailable.");

    public string ConnectionStringFor(string databaseName) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = databaseName }.ConnectionString;

    public static string UniqueDatabaseName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>Creates a database and applies migrations (and optionally the demo data) via the migrate command.</summary>
    public async Task<string> CreateMigratedDatabaseAsync(string databaseName, bool seedDemoData, CancellationToken cancellationToken)
    {
        var connectionString = ConnectionStringFor(databaseName);

        await using var factory = new FieldAppFactory { AppDbConnectionString = connectionString };
        var exitCode = await new DatabaseCommand(seedDemoData).RunAsync(factory.Services, cancellationToken);

        return exitCode == 0
            ? connectionString
            : throw new InvalidOperationException($"The migrate command failed for database {databaseName} (exit code {exitCode}).");
    }

    /// <summary>Creates an empty database with no schema.</summary>
    public async Task<string> CreateEmptyDatabaseAsync(string databaseName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ServerConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return ConnectionStringFor(databaseName);
    }
}
