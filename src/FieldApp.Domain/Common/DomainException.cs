namespace FieldApp.Domain.Common;

/// <summary>A domain invariant was violated by invalid input. <see cref="Field"/> names the offending input when there is one.</summary>
public class DomainException : Exception
{
    public DomainException(string message, string? field = null)
        : base(message)
    {
        Field = field;
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? Field { get; }
}

/// <summary>The input is valid on its own but conflicts with existing state (for example a duplicate name).</summary>
public sealed class DomainConflictException : DomainException
{
    public DomainConflictException(string message, string? field = null)
        : base(message, field)
    {
    }

    public DomainConflictException()
    {
    }

    public DomainConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
