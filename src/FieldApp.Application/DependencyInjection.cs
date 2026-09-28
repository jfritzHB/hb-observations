using Microsoft.Extensions.DependencyInjection;

namespace FieldApp.Application;

public static class DependencyInjection
{
    /// <summary>Registers application-layer use cases. Called by the API composition root.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
