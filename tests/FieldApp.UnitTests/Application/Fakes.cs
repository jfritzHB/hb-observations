using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Users;

namespace FieldApp.UnitTests.Application;

internal sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;
}

internal sealed class FakeCorrelation : ICorrelationContext
{
    public string? CorrelationId => "unit-test-correlation";
}

/// <summary>In-memory stand-in for the persistence ports used by the reference-data use cases.</summary>
internal sealed class InMemoryReferenceData : IReferenceDataReader, IProjectMembershipReader, IAreaStore, IAuditLog, IUnitOfWork
{
    public List<Project> Projects { get; } = [];

    public List<MembershipGrant> Memberships { get; } = [];

    public List<Area> Areas { get; } = [];

    public List<ProjectTradeMapping> ProjectTrades { get; } = [];

    public List<AuditEvent> AuditEvents { get; } = [];

    public bool FailNextSaveWithDuplicateKey { get; set; }

    public int SaveCount { get; private set; }

    private List<Area> PendingAreas { get; } = [];

    private List<AuditEvent> PendingAudit { get; } = [];

    public Task<IReadOnlyList<ProjectWithRoles>> ListProjectsForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectWithRoles>>([.. Memberships
            .Where(m => m.UserId == userId)
            .Join(Projects, m => m.ProjectId, p => p.Id, (m, p) => new ProjectWithRoles(p, m.Roles))]);

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.SingleOrDefault(p => p.Id == projectId));

    public Task<IReadOnlyList<Area>> ListAreasAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Area>>([.. Areas.Where(a => a.ProjectId == projectId)]);

    public Task<IReadOnlyList<ProjectTradeMapping>> ListProjectTradesAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectTradeMapping>>([.. ProjectTrades.Where(m => m.ProjectTrade.ProjectId == projectId)]);

    public Task<AppUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(null);

    public Task<MembershipGrant?> FindActiveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Memberships.SingleOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<IReadOnlyList<Area>> ListForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        ListAreasAsync(projectId, cancellationToken);

    public void Add(Area area) => PendingAreas.Add(area);

    public void Add(AuditEvent auditEvent) => PendingAudit.Add(auditEvent);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailNextSaveWithDuplicateKey)
        {
            FailNextSaveWithDuplicateKey = false;
            PendingAreas.Clear();
            PendingAudit.Clear();
            throw new DuplicateKeyException("duplicate");
        }

        Areas.AddRange(PendingAreas);
        AuditEvents.AddRange(PendingAudit);
        PendingAreas.Clear();
        PendingAudit.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }

    public Guid AddProjectWithMember(Guid userId, ProjectRoles roles)
    {
        var project = Project.Create(Guid.NewGuid(), $"HB-UNIT-{Projects.Count + 1:000}", "Synthetic Unit Project", "UTC", DateTimeOffset.UtcNow);
        Projects.Add(project);
        Memberships.Add(new MembershipGrant(project.Id, userId, roles, roles == ProjectRoles.TradePartner ? Guid.NewGuid() : null));
        return project.Id;
    }
}
