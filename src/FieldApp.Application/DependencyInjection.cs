using FieldApp.Application.Authorization;
using FieldApp.Application.ReferenceData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FieldApp.Application;

public static class DependencyInjection
{
    /// <summary>Registers application-layer use cases. Called by the API composition root.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProjectAuthorizer>();
        services.AddScoped<ProjectQueries>();
        services.AddScoped<AreaService>();
        services.AddScoped<TradeQueries>();

        return services;
    }
}
