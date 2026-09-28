using FieldApp.Domain.Common;

namespace FieldApp.Domain.Audit;

/// <summary>
/// Append-only record of a security or business event (docs/07-security-operations.md): actor, UTC time,
/// project, action, correlation ID and minimal sanitized detail.
/// </summary>
public sealed class AuditEvent
{
    public const int EntityTypeMaxLength = 100;
    public const int ActionMaxLength = 100;
    public const int CorrelationIdMaxLength = 64;
    public const int DetailsMaxLength = 4000;

    private AuditEvent()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string? CorrelationId { get; private set; }

    /// <summary>Sanitized JSON detail. Never contains secrets, tokens or photo data.</summary>
    public string? Details { get; private set; }

    public static AuditEvent Record(
        Guid id,
        DateTimeOffset occurredAt,
        Guid actorUserId,
        Guid? projectId,
        string entityType,
        Guid entityId,
        string action,
        string? correlationId,
        string? details) => new()
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            OccurredAt = Guard.Utc(occurredAt),
            ActorUserId = Guard.RequiredId(actorUserId, nameof(ActorUserId)),
            ProjectId = projectId,
            EntityType = Guard.RequiredText(entityType, EntityTypeMaxLength, nameof(EntityType)),
            EntityId = Guard.RequiredId(entityId, nameof(EntityId)),
            Action = Guard.RequiredText(action, ActionMaxLength, nameof(Action)),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : Guard.RequiredText(correlationId, CorrelationIdMaxLength, nameof(CorrelationId)),
            Details = details is { Length: > DetailsMaxLength } ? throw new DomainException("Audit details are too long.", nameof(Details)) : details,
        };
}
