using FieldApp.Application.Authorization;
using FieldApp.Application.Items;
using FieldApp.Application.ReferenceData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FieldApp.Application;

public static class DependencyInjection
{
    /// <summary>Registers application-layer use cases. Called by the API composition root.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, PhotoPolicy? photoPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(photoPolicy ?? new PhotoPolicy());
        services.AddScoped<ProjectAuthorizer>();
        services.AddScoped<ProjectQueries>();
        services.AddScoped<AreaService>();
        services.AddScoped<TradeQueries>();
        services.AddScoped<ItemAccess>();
        services.AddScoped<FieldItemService>();
        services.AddScoped<PhotoService>();

        return services;
    }
}
