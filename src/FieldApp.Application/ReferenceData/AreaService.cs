using System.Text.Json;
using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Common;

namespace FieldApp.Application.ReferenceData;

public sealed class AreaService(
    IReferenceDataReader reader,
    IAreaStore areas,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    ProjectAuthorizer authorizer,
    ICurrentUser currentUser,
    ICorrelationContext correlation,
    TimeProvider clock)
{
    public const int SearchMaxLength = 100;

    /// <summary>
    /// Areas selectable for field capture (the area and all its ancestors are active), in hierarchical order,
    /// optionally filtered so every whitespace-separated search term appears in the full path.
    /// </summary>
    public async Task<Result<IReadOnlyList<AreaDto>>> ListSelectableAsync(
        Guid projectId,
        string? search,
        CancellationToken cancellationToken)
    {
        if (search is { Length: > SearchMaxLength })
        {
            return AppError.Validation("search", $"Search must be {SearchMaxLength} characters or fewer.");
        }

        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewReferenceData, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var tree = new AreaTree(projectId, await reader.ListAreasAsync(projectId, cancellationToken));
        var terms = (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IReadOnlyList<AreaDto> result = [.. tree.InDisplayOrder()
            .Where(area => tree.IsSelectable(area.Id))
            .Where(area => terms.All(term => area.Path.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(area => ToDto(area, tree))];

        return Result.Success(result);
    }

    public async Task<Result<AreaDto>> GetAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewReferenceData, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var tree = new AreaTree(projectId, await reader.ListAreasAsync(projectId, cancellationToken));
        return tree.Find(areaId) is { } area ? ToDto(area, tree) : AppError.NotFound("Area not found.");
    }

    public async Task<Result<AreaDto>> CreateAsync(Guid projectId, CreateAreaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ManageAreas, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var tree = new AreaTree(projectId, await areas.ListForUpdateAsync(projectId, cancellationToken));

        Area area;
        try
        {
            area = tree.Add(Guid.NewGuid(), request.ParentAreaId, request.Name ?? string.Empty, request.SortOrder);
        }
        catch (DomainConflictException ex)
        {
            return AppError.Conflict(ex.Message);
        }
        catch (DomainException ex)
        {
            return AppError.Validation(ToCamelCase(ex.Field ?? "request"), ex.Message);
        }

        areas.Add(area);
        auditLog.Add(AuditEvent.Record(
            Guid.NewGuid(),
            clock.GetUtcNow(),
            currentUser.RequireUserId(),
            projectId,
            entityType: nameof(Area),
            entityId: area.Id,
            action: "AreaCreated",
            correlation.CorrelationId,
            JsonSerializer.Serialize(new { area.Name, area.Path, area.ParentAreaId, area.SortOrder })));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return AppError.Conflict($"An area named '{area.Path}' already exists in this project.");
        }

        return ToDto(area, tree);
    }

    private static AreaDto ToDto(Area area, AreaTree tree) =>
        new(area.Id, area.ParentAreaId, area.Name, area.Path, tree.DepthOf(area.Id), area.SortOrder, area.IsActive);

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
