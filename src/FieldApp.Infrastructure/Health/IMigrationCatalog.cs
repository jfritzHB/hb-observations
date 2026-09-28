using FieldApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldApp.Infrastructure.Health;

/// <summary>The migrations compiled into this build (read from assembly metadata; no database access).</summary>
internal interface IMigrationCatalog
{
    IReadOnlyCollection<string> KnownMigrations { get; }
}

internal sealed class EfMigrationCatalog(FieldAppDbContext db) : IMigrationCatalog
{
    public IReadOnlyCollection<string> KnownMigrations { get; } = [.. db.Database.GetMigrations()];
}
