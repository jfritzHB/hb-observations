using System.Globalization;
using System.Security.Claims;
using FieldApp.Application.Identity;
using Microsoft.AspNetCore.Authentication;

namespace FieldApp.Api.Authentication;

/// <summary>
/// Scheme-independent: maps the authenticated identity (provider + subject) to an active application user and
/// adds <see cref="FieldAppClaimTypes.ApplicationUserId"/>. An identity with no application user gets no such
/// claim and is therefore refused by the <see cref="FieldAppPolicies.ApplicationUser"/> policy.
/// </summary>
public sealed class ApplicationUserClaimsTransformation(IApplicationUserResolver resolver, IHttpContextAccessor httpContextAccessor)
    : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(claim => claim.Type == FieldAppClaimTypes.ApplicationUserId))
        {
            return principal;
        }

        var provider = principal.FindFirstValue(FieldAppClaimTypes.IdentityProvider);
        var subject = principal.FindFirstValue(FieldAppClaimTypes.Subject);
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(subject))
        {
            return principal;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var user = await resolver.ResolveAsync(provider, subject, cancellationToken);
        if (user is null)
        {
            return principal;
        }

        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity(
            [
                new Claim(FieldAppClaimTypes.ApplicationUserId, user.UserId.ToString("D", CultureInfo.InvariantCulture)),
                new Claim(FieldAppClaimTypes.DisplayName, user.DisplayName),
            ]));
        return transformed;
    }
}
