using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using FieldApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FieldApp.Infrastructure.Persistence;

public sealed class FieldAppDbContext(DbContextOptions<FieldAppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Area> Areas => Set<Area>();

    public DbSet<Trade> Trades => Set<Trade>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<ProjectTrade> ProjectTrades => Set<ProjectTrade>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FieldAppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Text columns are Unicode and explicitly sized in each configuration.
        configurationBuilder.Properties<string>().AreUnicode();
    }
}
