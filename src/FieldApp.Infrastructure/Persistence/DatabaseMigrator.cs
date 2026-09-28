using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FieldApp.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations. Invoked only by the explicit <c>migrate</c> command (a Container Apps Job or
/// pipeline step in Azure, the <c>migrate</c> Compose service locally), never by application startup.
/// </summary>
public sealed partial class DatabaseMigrator(FieldAppDbContext db, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        var pendingList = string.Join(", ", pending);
        LogPending(pending.Count, pendingList);

        // EF Core takes a database-wide migration lock, so concurrent runs are safe.
        await db.Database.MigrateAsync(cancellationToken);

        var latest = db.Database.GetMigrations().LastOrDefault() ?? "(none)";
        LogApplied(latest);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {PendingCount} pending migration(s): {PendingMigrations}")]
    private partial void LogPending(int pendingCount, string pendingMigrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is at migration {LatestMigration}")]
    private partial void LogApplied(string latestMigration);
}
