using FieldApp.Application.Authorization;
using FieldApp.Application.Identity;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FieldApp.Infrastructure.Persistence;

internal sealed class ReferenceDataReader(FieldAppDbContext db)
    : IReferenceDataReader, IProjectMembershipReader, IApplicationUserResolver
{
    public async Task<IReadOnlyList<ProjectWithRoles>> ListProjectsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await (
            from membership in db.ProjectMemberships.AsNoTracking()
            join project in db.Projects.AsNoTracking() on membership.ProjectId equals project.Id
            where membership.UserId == userId && membership.IsActive
            select new { project, membership.Roles })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new ProjectWithRoles(row.project, row.Roles))];
    }

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Projects.AsNoTracking().SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    public async Task<IReadOnlyList<Area>> ListAreasAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Areas.AsNoTracking().Where(area => area.ProjectId == projectId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTradeMapping>> ListProjectTradesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await (
            from projectTrade in db.ProjectTrades.AsNoTracking()
            join trade in db.Trades.AsNoTracking() on projectTrade.TradeId equals trade.Id
            join company in db.Companies.AsNoTracking() on projectTrade.ResponsibleCompanyId equals company.Id into companies
            from company in companies.DefaultIfEmpty()
            where projectTrade.ProjectId == projectId
            select new { projectTrade, trade, company })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new ProjectTradeMapping(row.projectTrade, row.trade, row.company))];
    }

    public Task<AppUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken);

    public async Task<MembershipGrant?> FindActiveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await db.ProjectMemberships.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId && m.IsActive)
            .Select(m => new { m.ProjectId, m.UserId, m.Roles, m.CompanyId })
            .SingleOrDefaultAsync(cancellationToken);

        return membership is null
            ? null
            : new MembershipGrant(membership.ProjectId, membership.UserId, membership.Roles, membership.CompanyId);
    }

    public async Task<ResolvedApplicationUser?> ResolveAsync(string identityProvider, string subject, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.IdentityProvider == identityProvider && u.IdentitySubject == subject && u.IsActive)
            .Select(u => new { u.Id, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? null : new ResolvedApplicationUser(user.Id, user.DisplayName);
    }
}
