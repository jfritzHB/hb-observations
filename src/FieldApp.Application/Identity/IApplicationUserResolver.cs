namespace FieldApp.Application.Identity;

/// <summary>
/// Maps an authenticated external identity (provider + subject) to an active application user. Used by the host
/// for every authentication scheme, so replacing the development stub with Entra ID does not change authorization.
/// </summary>
public interface IApplicationUserResolver
{
    Task<ResolvedApplicationUser?> ResolveAsync(string identityProvider, string subject, CancellationToken cancellationToken);
}

public sealed record ResolvedApplicationUser(Guid UserId, string DisplayName);
