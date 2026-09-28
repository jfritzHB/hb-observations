using FieldApp.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace FieldApp.Api.Authentication;

/// <summary>
/// Selects the authentication scheme from <c>Authentication:Mode</c>:
/// <list type="bullet">
/// <item><c>Development</c>: synthetic persona header. Allowed only when the host environment is Development.</item>
/// <item>unset / <c>None</c>: no identity provider; protected endpoints return 401.</item>
/// </list>
/// Microsoft Entra ID (Slice 6) will be a third mode behind the same claims contract.
/// </summary>
public static class AuthenticationSetup
{
    public const string ModeKey = "Authentication:Mode";
    public const string DevelopmentMode = "Development";

    public static IServiceCollection AddFieldAppAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IClaimsTransformation, ApplicationUserClaimsTransformation>();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, NoAuthenticationHandler>(NoAuthenticationHandler.SchemeName, null)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentPersonaAuthenticationHandler>(DevelopmentPersonaAuthenticationHandler.SchemeName, null);

        // Resolved at runtime from final configuration so tests and hosts can override the mode.
        services.AddOptions<AuthenticationOptions>().Configure<IConfiguration>((options, configuration) =>
            options.DefaultScheme = IsDevelopmentMode(configuration)
                ? DevelopmentPersonaAuthenticationHandler.SchemeName
                : NoAuthenticationHandler.SchemeName);

        services.AddAuthorizationBuilder()
            .AddPolicy(FieldAppPolicies.ApplicationUser, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(FieldAppClaimTypes.ApplicationUserId));

        return services;
    }

    public static bool IsDevelopmentMode(IConfiguration configuration) =>
        string.Equals(configuration[ModeKey], DevelopmentMode, StringComparison.OrdinalIgnoreCase);

    /// <summary>Fails startup if persona authentication is configured outside the Development environment.</summary>
    public static void EnsureSafeConfiguration(IHostEnvironment environment, IConfiguration configuration)
    {
        if (IsDevelopmentMode(configuration) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{ModeKey}={DevelopmentMode} is only permitted in the Development environment " +
                $"(current environment: {environment.EnvironmentName}). Refusing to start.");
        }
    }
}
