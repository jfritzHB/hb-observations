using FieldApp.Domain.Common;

namespace FieldApp.Domain.Companies;

/// <summary>A trade partner (or other) company that can be responsible for a trade on a project.</summary>
public sealed class Company
{
    public const int NameMaxLength = 200;

    private Company()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Company Create(Guid id, string name) => new()
    {
        Id = Guard.RequiredId(id, nameof(Id)),
        Name = Guard.RequiredText(name, NameMaxLength, nameof(Name)),
        IsActive = true,
    };

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
