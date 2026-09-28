namespace FieldApp.Application.Abstractions;

/// <summary>
/// The application user making the current request. Populated by the host from whichever authentication scheme
/// is configured (the development persona stub today, Microsoft Entra ID later); use cases never see the scheme.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The resolved application user ID, or null when the caller is not a provisioned application user.</summary>
    Guid? UserId { get; }
}

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.UserId
            ?? throw new InvalidOperationException("No application user is associated with the current request.");
    }
}
