using System.Text.Json;
using FieldApp.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldApp.Api.Health;

public static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c> runs no checks and never touches external dependencies.
    /// <c>/health/ready</c> runs checks tagged <see cref="HealthCheckTags.Ready"/> (configuration and database).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = WriteSummaryAsync,
        });

        return endpoints;
    }

    // Names and statuses only: descriptions and exceptions stay in server logs.
    private static Task WriteSummaryAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), context.RequestAborted);
    }
}
