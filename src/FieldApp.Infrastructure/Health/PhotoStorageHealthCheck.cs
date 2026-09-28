using Azure;
using FieldApp.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldApp.Infrastructure.Health;

/// <summary>Readiness: photo storage is configured and its private container exists and answers.</summary>
internal sealed class PhotoStorageHealthCheck(PhotoContainerProvider containers) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!containers.IsConfigured)
        {
            return HealthCheckResult.Unhealthy("Photo storage is not configured.");
        }

        try
        {
            var exists = await containers.Container.ExistsAsync(cancellationToken).WaitAsync(cancellationToken);
            return exists.Value
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The photo container does not exist; run the migrate command.");
        }
        catch (RequestFailedException ex)
        {
            return HealthCheckResult.Unhealthy("Photo storage is unreachable.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or AggregateException)
        {
            return HealthCheckResult.Unhealthy("Photo storage is unreachable.", ex);
        }
    }
}
