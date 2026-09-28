using FieldApp.Domain.Common;

namespace FieldApp.Domain.Areas;

/// <summary>
/// A selectable project location in a hierarchy such as Building / Level / Room. <see cref="Path"/> is the
/// flattened display path. Hierarchy changes go through <see cref="AreaTree"/>, which prevents cycles.
/// </summary>
public sealed class Area
{
    public const int NameMaxLength = 100;
    public const int PathMaxLength = 450;
    public const string PathSeparator = " / ";

    private Area()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid? ParentAreaId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Path { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Creates an area. A parent, when given, must belong to the same project.</summary>
    public static Area Create(Guid id, Guid projectId, Area? parent, string name, int sortOrder)
    {
        var area = new Area
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            ProjectId = Guard.RequiredId(projectId, nameof(ProjectId)),
            Name = Guard.RequiredText(name, NameMaxLength, nameof(Name)),
            SortOrder = sortOrder,
            IsActive = true,
        };

        area.AttachTo(parent);
        return area;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    internal void AttachTo(Area? parent)
    {
        if (parent is not null && parent.ProjectId != ProjectId)
        {
            throw new DomainException("A parent area must belong to the same project.", nameof(ParentAreaId));
        }

        if (parent?.Id == Id)
        {
            throw new DomainException("An area cannot be its own parent.", nameof(ParentAreaId));
        }

        ParentAreaId = parent?.Id;
        RecomputePath(parent);
    }

    internal void RecomputePath(Area? parent)
    {
        var path = parent is null ? Name : parent.Path + PathSeparator + Name;
        if (path.Length > PathMaxLength)
        {
            throw new DomainException($"The full area path must be {PathMaxLength} characters or fewer.", nameof(Name));
        }

        Path = path;
    }
}
