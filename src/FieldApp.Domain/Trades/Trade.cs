using FieldApp.Domain.Common;

namespace FieldApp.Domain.Trades;

/// <summary>Controlled trade list entry. Enabled per project through <see cref="Projects.ProjectTrade"/>.</summary>
public sealed class Trade
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;

    private Trade()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Short upper-case code, for example <c>DRY</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Trade Create(Guid id, string code, string name)
    {
        var normalizedCode = Guard.RequiredText(code, CodeMaxLength, nameof(Code)).ToUpperInvariant();
        if (!normalizedCode.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new DomainException("Code may contain only letters, digits and hyphens.", nameof(Code));
        }

        return new Trade
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            Code = normalizedCode,
            Name = Guard.RequiredText(name, NameMaxLength, nameof(Name)),
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
