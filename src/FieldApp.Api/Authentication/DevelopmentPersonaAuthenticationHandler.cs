using System.Security.Claims;
using System.Text.Encodings.Web;
using FieldApp.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FieldApp.Api.Authentication;

/// <summary>
/// Development-only stand-in for Microsoft Entra ID. The caller names a synthetic persona in the
/// <c>X-Dev-Persona</c> header; there are no passwords. It produces the same identity claims (provider + subject)
/// that the Entra scheme will, so everything downstream is unchanged when it is replaced.
/// </summary>
public sealed class DevelopmentPersonaAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentPersona";
    public const string HeaderName = "X-Dev-Persona";

    private static readonly HashSet<string> _knownPersonas =
        new(DemoData.Personas.Select(persona => persona.Key), StringComparer.Ordinal);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var persona = values.ToString();
        if (!_knownPersonas.Contains(persona))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown development persona."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(FieldAppClaimTypes.IdentityProvider, DemoData.DevelopmentIdentityProvider),
                new Claim(FieldAppClaimTypes.Subject, persona),
            ],
            SchemeName,
            FieldAppClaimTypes.DisplayName,
            roleType: null);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = SchemeName;
        return Task.CompletedTask;
    }
}
