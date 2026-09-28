using FieldApp.Infrastructure.Health;
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

        services.AddHealthChecks()
            .AddCheck<SqlDatabaseHealthCheck>("database", tags: [HealthCheckTags.Ready], timeout: _readinessTimeout);

        return services;
    }
}
