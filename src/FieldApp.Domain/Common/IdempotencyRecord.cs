namespace FieldApp.Domain.Common;

/// <summary>
/// Remembers the outcome of an idempotent write (scope + caller + Idempotency-Key) so a retried request returns the
/// original result instead of repeating the write, across requests and application restarts.
/// </summary>
public sealed class IdempotencyRecord
{
    public const int ScopeMaxLength = 100;
    public const int KeyMaxLength = 100;
    public const int FingerprintLength = 64;

    private IdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Scope { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    /// <summary>Hex SHA-256 of the canonical request; a reused key with a different request is rejected.</summary>
    public string RequestFingerprint { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(Guid id, Guid userId, string scope, string key, string requestFingerprint, Guid resourceId, DateTimeOffset now)
    {
        if (!IsValidKey(key))
        {
            throw new DomainException($"Idempotency-Key must be 1-{KeyMaxLength} visible ASCII characters.", "Idempotency-Key");
        }

        return new IdempotencyRecord
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            UserId = Guard.RequiredId(userId, nameof(UserId)),
            Scope = Guard.RequiredText(scope, ScopeMaxLength, nameof(Scope)),
            Key = key,
            RequestFingerprint = requestFingerprint is { Length: FingerprintLength }
                ? requestFingerprint
                : throw new DomainException("Invalid request fingerprint.", nameof(RequestFingerprint)),
            ResourceId = Guard.RequiredId(resourceId, nameof(ResourceId)),
            CreatedAt = Guard.Utc(now),
        };
    }

    public static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= KeyMaxLength } && key.All(c => c is > ' ' and <= '~');
}
