using FieldApp.Api.Authentication;
using FieldApp.Infrastructure.Seeding;

namespace FieldApp.Api.Endpoints;

public static class DevelopmentPersonaEndpoints
{
    /// <summary>
    /// Lists the synthetic personas for the in-app switcher. Mapped only when persona authentication is active,
    /// so the route does not exist (404) in any other configuration.
    /// </summary>
    public static void MapDevelopmentPersonaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/dev/personas", () => TypedResults.Ok(DemoData.Personas.Select(persona =>
                new DevelopmentPersonaDto(persona.Key, persona.DisplayName, persona.RoleLabel, persona.Description))))
            .AllowAnonymous()
            .WithName("ListDevelopmentPersonas")
            .WithTags("Development")
            .WithSummary($"Development only. Send the chosen key in the {DevelopmentPersonaAuthenticationHandler.HeaderName} header.");
    }
}

public sealed record DevelopmentPersonaDto(string Key, string DisplayName, string RoleLabel, string Description);
