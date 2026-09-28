using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldApp.Infrastructure.Health;

/// <summary>
/// Readiness check: the application database connection string is configured and the
/// database answers a trivial query. The registration timeout bounds the check.
/// </summary>
internal sealed class SqlDatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
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
        return await ProbeAsync(builder.ConnectionString, cancellationToken).WaitAsync(cancellationToken);
    }

    private static async Task<HealthCheckResult> ProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
