namespace FieldApp.Domain.Common;

internal static class Guard
{
    /// <summary>Trims <paramref name="value"/> and enforces presence, maximum length and no control characters.</summary>
    public static string RequiredText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.", field);
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} must be {maxLength} characters or fewer.", field);
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new DomainException($"{field} must not contain control characters.", field);
        }

        return trimmed;
    }

    public static Guid RequiredId(Guid value, string field) =>
        value == Guid.Empty ? throw new DomainException($"{field} is required.", field) : value;

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}
