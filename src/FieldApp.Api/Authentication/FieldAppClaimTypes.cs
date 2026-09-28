namespace FieldApp.Api.Authentication;

/// <summary>
/// Claims every authentication scheme must provide (<see cref="IdentityProvider"/> + <see cref="Subject"/>), and
/// the claim added once that identity is resolved to an application user.
/// </summary>
public static class FieldAppClaimTypes
{
    public const string IdentityProvider = "idp";
    public const string Subject = "sub";
    public const string ApplicationUserId = "fieldapp:uid";
    public const string DisplayName = "name";
}

public static class FieldAppPolicies
{
    /// <summary>Authenticated and linked to an active, provisioned application user.</summary>
    public const string ApplicationUser = "ApplicationUser";
}
