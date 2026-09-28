using FieldApp.Domain.Common;

namespace FieldApp.Domain.Areas;

/// <summary>
/// The area hierarchy of one project. Validates that loaded data forms a forest (no cycles, no dangling or
/// cross-project parents), and guards additions and moves so that it stays one.
/// </summary>
public sealed class AreaTree
{
    private const int SortOrderStep = 10;

    private readonly Dictionary<Guid, Area> _areas;

    public AreaTree(Guid projectId, IEnumerable<Area> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        ProjectId = projectId;
        _areas = areas.ToDictionary(area => area.Id);

        foreach (var area in _areas.Values)
        {
            if (area.ProjectId != projectId)
            {
                throw new DomainException($"Area {area.Id} belongs to a different project.");
            }

            if (area.ParentAreaId is { } parentId && !_areas.ContainsKey(parentId))
            {
                throw new DomainException($"Area {area.Id} references a parent that is not in this project.");
            }
        }

        foreach (var area in _areas.Values)
        {
            _ = Ancestors(area).Count(); // Throws on a cycle.
        }
    }

    public Guid ProjectId { get; }

    public IReadOnlyCollection<Area> Areas => _areas.Values;

    public Area? Find(Guid areaId) => _areas.GetValueOrDefault(areaId);

    /// <summary>Adds a new area under <paramref name="parentAreaId"/> (or at the root).</summary>
    public Area Add(Guid id, Guid? parentAreaId, string name, int? sortOrder = null)
    {
        var parent = parentAreaId is { } parentId
            ? Find(parentId) ?? throw new DomainException("The parent area does not exist in this project.", "ParentAreaId")
            : null;

        var area = Area.Create(id, ProjectId, parent, name, sortOrder ?? NextSortOrder(parent?.Id));
        EnsureUniquePath(area.Path, exceptAreaId: null);

        _areas.Add(area.Id, area);
        return area;
    }

    /// <summary>Moves an area (and its subtree) under a new parent, rejecting moves that would create a cycle.</summary>
    public void Move(Guid areaId, Guid? newParentAreaId)
    {
        var area = Find(areaId) ?? throw new DomainException("The area does not exist in this project.", "AreaId");
        var newParent = newParentAreaId is { } parentId
            ? Find(parentId) ?? throw new DomainException("The parent area does not exist in this project.", "ParentAreaId")
            : null;

        if (newParent is not null && (newParent.Id == area.Id || Ancestors(newParent).Any(a => a.Id == area.Id)))
        {
            throw new DomainException("An area cannot be moved beneath itself or one of its descendants.", "ParentAreaId");
        }

        var newPath = newParent is null ? area.Name : newParent.Path + Area.PathSeparator + area.Name;
        EnsureUniquePath(newPath, exceptAreaId: area.Id);

        area.AttachTo(newParent);
        foreach (var descendant in Descendants(area))
        {
            descendant.RecomputePath(_areas[descendant.ParentAreaId!.Value]);
        }
    }

    /// <summary>An area is selectable for field capture when it and every ancestor are active.</summary>
    public bool IsSelectable(Guid areaId) =>
        Find(areaId) is { } area && area.IsActive && Ancestors(area).All(ancestor => ancestor.IsActive);

    public int DepthOf(Guid areaId) =>
        Find(areaId) is { } area ? Ancestors(area).Count() : throw new DomainException("The area does not exist in this project.");

    /// <summary>Depth-first order: parents before children, siblings by sort order then name.</summary>
    public IReadOnlyList<Area> InDisplayOrder()
    {
        var childrenByParent = _areas.Values.ToLookup(area => area.ParentAreaId);
        var ordered = new List<Area>(_areas.Count);

        void Visit(Guid? parentId)
        {
            foreach (var child in childrenByParent[parentId]
                .OrderBy(area => area.SortOrder)
                .ThenBy(area => area.Name, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(child);
                Visit(child.Id);
            }
        }

        Visit(null);
        return ordered;
    }

    private IEnumerable<Area> Ancestors(Area area)
    {
        var visited = new HashSet<Guid> { area.Id };
        var current = area;

        while (current.ParentAreaId is { } parentId)
        {
            if (!visited.Add(parentId))
            {
                throw new DomainException($"The area hierarchy contains a cycle at area {parentId}.");
            }

            current = _areas[parentId];
            yield return current;
        }
    }

    private IEnumerable<Area> Descendants(Area area)
    {
        foreach (var child in _areas.Values.Where(a => a.ParentAreaId == area.Id).ToList())
        {
            yield return child;
            foreach (var grandchild in Descendants(child))
            {
                yield return grandchild;
            }
        }
    }

    private int NextSortOrder(Guid? parentId)
    {
        var siblings = _areas.Values.Where(area => area.ParentAreaId == parentId).ToList();
        return siblings.Count == 0 ? SortOrderStep : siblings.Max(area => area.SortOrder) + SortOrderStep;
    }

    private void EnsureUniquePath(string path, Guid? exceptAreaId)
    {
        if (_areas.Values.Any(area => area.Id != exceptAreaId && string.Equals(area.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainConflictException($"An area named '{path}' already exists in this project.", "Name");
        }
    }
}
