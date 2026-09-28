using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;

namespace FieldApp.Application.ReferenceData;

public sealed record ProjectDto(
    Guid Id,
    string Number,
    string Name,
    string TimeZoneId,
    ProjectStatus Status,
    IReadOnlyList<ProjectRoles> MyRoles);

public sealed record AreaDto(
    Guid Id,
    Guid? ParentAreaId,
    string Name,
    string Path,
    int Depth,
    int SortOrder,
    bool IsActive);

public sealed record CreateAreaRequest(Guid? ParentAreaId, string? Name, int? SortOrder);

/// <summary>A trade that can be selected for field capture, with its configured Responsible Company (read-only).</summary>
public sealed record CaptureTradeDto(Guid TradeId, string Code, string Name, ResponsibleCompanyDto ResponsibleCompany);

public sealed record ResponsibleCompanyDto(Guid Id, string Name);

public sealed record ProjectCompanyDto(Guid Id, string Name, IReadOnlyList<Guid> TradeIds);

public sealed record CurrentUserDto(Guid Id, string DisplayName, string? Email);
