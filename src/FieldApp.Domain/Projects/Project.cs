using FieldApp.Domain.Common;

namespace FieldApp.Domain.Projects;

public sealed class Project
{
    public const int NumberMaxLength = 30;
    public const int NameMaxLength = 200;
    public const int TimeZoneIdMaxLength = 64;

    private Project()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>IANA or Windows time zone identifier used to render timestamps for this project.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Project Create(Guid id, string number, string name, string timeZoneId, DateTimeOffset createdAt)
    {
        var zone = Guard.RequiredText(timeZoneId, TimeZoneIdMaxLength, nameof(TimeZoneId));
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
        {
            throw new DomainException($"Time zone '{zone}' is not recognized.", nameof(TimeZoneId));
        }

        return new Project
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            Number = Guard.RequiredText(number, NumberMaxLength, nameof(Number)),
            Name = Guard.RequiredText(name, NameMaxLength, nameof(Name)),
            TimeZoneId = zone,
            Status = ProjectStatus.Active,
            CreatedAt = Guard.Utc(createdAt),
        };
    }

    public void Close() => Status = ProjectStatus.Closed;
}
