using FieldApp.Domain.Memberships;

namespace FieldApp.Application.Authorization;

public interface IProjectMembershipReader
{
    /// <summary>The caller's active membership in an existing project, or null (project missing or no membership).</summary>
    Task<MembershipGrant?> FindActiveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
}

public sealed record MembershipGrant(Guid ProjectId, Guid UserId, ProjectRoles Roles, Guid? CompanyId);
