using FieldApp.Api.Database;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>
/// One disposable SQL Server container and one Azurite (Blob Storage emulator) container for the whole test
/// assembly, with a database migrated and seeded with the synthetic demo data through the real
/// <c>migrate --seed-demo-data</c> command. Tests that write data create their own database (and photo container) so
/// read-only tests stay deterministic under parallel execution.
/// When Docker is unavailable, dependent tests are skipped locally; set <c>FIELDAPP_REQUIRE_DOCKER=true</c>
/// (as CI does) to make that a failure instead.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:latest";
    private const string SeededPhotoContainer = "photos-seeded";

    private MsSqlContainer? _sql;
    private AzuriteContainer? _azurite;

    public string? ServerConnectionString { get; private set; }

    /// <summary>Migrated and seeded; treat as read-only.</summary>
    public string? SeededConnectionString { get; private set; }

    public string? PhotoStorageConnectionString { get; private set; }

    public string? UnavailableReason { get; private set; }

    private static bool DockerRequired =>
        string.Equals(Environment.GetEnvironmentVariable("FIELDAPP_REQUIRE_DOCKER"), "true", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _sql = new MsSqlBuilder(SqlImage).Build();
            _azurite = new AzuriteBuilder(AzuriteImage).WithInMemoryPersistence().WithCommand("--skipApiVersionCheck").Build();
            await Task.WhenAll(_sql.StartAsync(), _azurite.StartAsync());
            ServerConnectionString = _sql.GetConnectionString();
            PhotoStorageConnectionString = _azurite.GetConnectionString();
        }
        catch (Exception ex) when (!DockerRequired)
        {
            UnavailableReason = $"Test containers could not start: {ex.Message}";
            ServerConnectionString = null;
            return;
        }

        SeededConnectionString = await CreateMigratedDatabaseAsync("FieldAppSeeded", seedDemoData: true, CancellationToken.None, SeededPhotoContainer);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sql is not null)
        {
            await _sql.DisposeAsync();
        }

        if (_azurite is not null)
        {
            await _azurite.DisposeAsync();
        }
    }

    public void SkipIfUnavailable() =>
        Assert.SkipWhen(ServerConnectionString is null, UnavailableReason ?? "SQL Server unavailable.");

    public string ConnectionStringFor(string databaseName) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = databaseName }.ConnectionString;

    public static string UniqueDatabaseName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>An API host for a database (and its photo container, if any) backed by the shared containers.</summary>
    public FieldAppFactory Factory(string databaseConnectionString, string? photoContainer = null, string environment = "Development") => new()
    {
        AppDbConnectionString = databaseConnectionString,
        PhotoStorageConnectionString = photoContainer is null ? string.Empty : PhotoStorageConnectionString ?? string.Empty,
        PhotoContainer = photoContainer ?? "field-item-photos",
        EnvironmentName = environment,
    };

    /// <summary>A factory for the shared seeded database and its photo container.</summary>
    public FieldAppFactory SeededFactory() => Factory(SeededConnectionString!, SeededPhotoContainer);

    /// <summary>
    /// Creates a database and applies migrations (and optionally the demo data) via the migrate command, which also
    /// provisions <paramref name="photoContainer"/> when given.
    /// </summary>
    public async Task<string> CreateMigratedDatabaseAsync(string databaseName, bool seedDemoData, CancellationToken cancellationToken, string? photoContainer = null)
    {
        var connectionString = ConnectionStringFor(databaseName);

        await using var factory = Factory(connectionString, photoContainer);
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

    public static string UniqueContainerName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(63, prefix.Length + 33)];
}
