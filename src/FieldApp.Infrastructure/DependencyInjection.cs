using FieldApp.Application.Authorization;
using FieldApp.Application.Identity;
using FieldApp.Application.Items;
using FieldApp.Application.ReferenceData;
using FieldApp.Infrastructure.Health;
using FieldApp.Infrastructure.Imaging;
using FieldApp.Infrastructure.Persistence;
using FieldApp.Infrastructure.Seeding;
using FieldApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldApp.Infrastructure;

public static class DependencyInjection
{
    private static readonly TimeSpan _readinessTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Registers infrastructure adapters. Called by the API composition root.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // The connection string is read when a context is created, so a missing value surfaces through
        // readiness (and failed requests) instead of preventing startup.
        services.AddDbContext<FieldAppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString(SqlDatabaseHealthCheck.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(sql => sql.EnableRetryOnFailure());
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            }
        });

        services.AddScoped<ReferenceDataReader>();
        services.AddScoped<IReferenceDataReader>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<IProjectMembershipReader>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<IApplicationUserResolver>(provider => provider.GetRequiredService<ReferenceDataReader>());

        services.AddScoped<EfWriteStore>();
        services.AddScoped<IAreaStore>(provider => provider.GetRequiredService<EfWriteStore>());
        services.AddScoped<IAuditLog>(provider => provider.GetRequiredService<EfWriteStore>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<EfWriteStore>());
        services.AddScoped<IFieldItemStore>(provider => provider.GetRequiredService<EfWriteStore>());

        services.AddSingleton<PhotoContainerProvider>();
        services.AddSingleton<IPhotoStorage, AzureBlobPhotoStorage>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<DemoDataSeeder>();

        services.AddScoped<IMigrationCatalog, EfMigrationCatalog>();
        services.AddHealthChecks()
            .AddCheck<SqlDatabaseHealthCheck>("database", tags: [HealthCheckTags.Ready], timeout: _readinessTimeout)
            .AddCheck<PhotoStorageHealthCheck>("photo-storage", tags: [HealthCheckTags.Ready], timeout: _readinessTimeout);

        return services;
    }
}
