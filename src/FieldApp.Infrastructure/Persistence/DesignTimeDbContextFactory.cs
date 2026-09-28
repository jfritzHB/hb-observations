using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FieldApp.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations and scripts. It never connects to a database for those
/// commands; <c>FIELDAPP_DESIGN_CONNECTION</c> is only needed for commands such as <c>dotnet ef database update</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FieldAppDbContext>
{
    public FieldAppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FIELDAPP_DESIGN_CONNECTION");
        var options = new DbContextOptionsBuilder<FieldAppDbContext>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseSqlServer();
        }
        else
        {
            options.UseSqlServer(connectionString);
        }

        return new FieldAppDbContext(options.Options);
    }
}
