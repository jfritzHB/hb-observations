using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using FieldApp.Domain.Users;

namespace FieldApp.Application.ReferenceData;

/// <summary>Read-only access to reference data. Implementations return untracked entities.</summary>
public interface IReferenceDataReader
{
    Task<IReadOnlyList<ProjectWithRoles>> ListProjectsForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Area>> ListAreasAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectTradeMapping>> ListProjectTradesAsync(Guid projectId, CancellationToken cancellationToken);

    Task<AppUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record ProjectWithRoles(Project Project, ProjectRoles Roles);

public sealed record ProjectTradeMapping(ProjectTrade ProjectTrade, Trade Trade, Company? ResponsibleCompany)
{
    public bool IsAvailableForCapture => ProjectTrade.IsAvailableForCapture(Trade, ResponsibleCompany);
}

/// <summary>Tracked area access for commands.</summary>
public interface IAreaStore
{
    Task<IReadOnlyList<Area>> ListForUpdateAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(Area area);
}

public interface IAuditLog
{
    void Add(AuditEvent auditEvent);
}

public interface IUnitOfWork
{
    /// <summary>Persists all pending changes atomically.</summary>
    /// <exception cref="Common.DuplicateKeyException">A uniqueness constraint was violated (for example by a concurrent request).</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
