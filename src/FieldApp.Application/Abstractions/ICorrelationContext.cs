namespace FieldApp.Application.Abstractions;

/// <summary>The correlation ID of the current operation, recorded on audit events.</summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
