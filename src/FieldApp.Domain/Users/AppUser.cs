using FieldApp.Domain.Common;

namespace FieldApp.Domain.Users;

/// <summary>
/// An application user, linked to one external identity (<see cref="IdentityProvider"/> + <see cref="IdentitySubject"/>).
/// Access comes only from <see cref="Memberships.ProjectMembership"/>, never from the identity itself.
/// </summary>
public sealed class AppUser
{
    public const int IdentityProviderMaxLength = 100;
    public const int IdentitySubjectMaxLength = 200;
    public const int DisplayNameMaxLength = 200;
    public const int EmailMaxLength = 320;

    private AppUser()
    {
    }

    public Guid Id { get; private set; }

    public string IdentityProvider { get; private set; } = string.Empty;

    public string IdentitySubject { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static AppUser Create(
        Guid id,
        string identityProvider,
        string identitySubject,
        string displayName,
        string? email,
        DateTimeOffset createdAt) => new()
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            IdentityProvider = Guard.RequiredText(identityProvider, IdentityProviderMaxLength, nameof(IdentityProvider)),
            IdentitySubject = Guard.RequiredText(identitySubject, IdentitySubjectMaxLength, nameof(IdentitySubject)),
            DisplayName = Guard.RequiredText(displayName, DisplayNameMaxLength, nameof(DisplayName)),
            Email = string.IsNullOrWhiteSpace(email) ? null : Guard.RequiredText(email, EmailMaxLength, nameof(Email)),
            IsActive = true,
            CreatedAt = Guard.Utc(createdAt),
        };

    public void Deactivate() => IsActive = false;
}
