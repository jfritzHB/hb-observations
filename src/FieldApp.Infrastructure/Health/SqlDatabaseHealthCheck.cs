using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldApp.Infrastructure.Health;

/// <summary>
/// Readiness check: the application database connection string is configured, the database answers a trivial
/// query, and every migration this build knows about has been applied (so a new revision is not given traffic
/// before its migration step has run). The registration timeout bounds the check.
/// </summary>
internal sealed class SqlDatabaseHealthCheck(IConfiguration configuration, IMigrationCatalog migrations) : IHealthCheck
{
    public const string ConnectionStringName = "AppDb";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy($"Connection string '{ConnectionStringName}' is not configured.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            // Each probe is a fresh, single attempt: the platform probe does the retrying, and a pooled
            // connection would cache a failure (pool blocking period) after the database has recovered.
            builder = new SqlConnectionStringBuilder(connectionString) { ConnectRetryCount = 0, Pooling = false };
        }
        catch (ArgumentException ex)
        {
            // The connection string is never included in the result.
            return HealthCheckResult.Unhealthy("Connection string is invalid.", ex);
        }

        // Some connection phases (such as DNS resolution) ignore cancellation; WaitAsync guarantees the
        // registration timeout is honoured even then.
        return await ProbeAsync(builder.ConnectionString, migrations.KnownMigrations, cancellationToken).WaitAsync(cancellationToken);
    }

    private static async Task<HealthCheckResult> ProbeAsync(
        string connectionString,
        IReadOnlyCollection<string> knownMigrations,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U')";
            if (await command.ExecuteScalarAsync(cancellationToken) is null or DBNull)
            {
                return knownMigrations.Count == 0
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("Database schema has not been migrated.");
            }

            command.CommandText = "SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory]";
            var applied = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    applied.Add(reader.GetString(0));
                }
            }

            var pending = knownMigrations.Count(migration => !applied.Contains(migration));
            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Database schema is missing {pending} migration(s).");
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
