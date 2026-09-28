using FieldApp.Application.Abstractions;
using FieldApp.Application.Common;

namespace FieldApp.Application.Authorization;

/// <summary>
/// Applies the resource-concealment policy: no active membership (or no such project) is indistinguishable
/// from a missing project (NotFound); a visible project with an unpermitted operation is Forbidden.
/// </summary>
public sealed class ProjectAuthorizer(IProjectMembershipReader memberships, ICurrentUser currentUser)
{
    public async Task<ProjectAuthorization> AuthorizeAsync(
        Guid projectId,
        ProjectOperation operation,
        CancellationToken cancellationToken)
    {
        var membership = await memberships.FindActiveAsync(projectId, currentUser.RequireUserId(), cancellationToken);

        if (membership is null)
        {
            return new ProjectAuthorization(null, AppError.NotFound("Project not found."));
        }

        return ProjectPolicies.Allows(membership.Roles, operation)
            ? new ProjectAuthorization(membership, null)
            : new ProjectAuthorization(membership, AppError.Forbidden());
    }
}

public sealed record ProjectAuthorization(MembershipGrant? Membership, AppError? Error)
{
    public bool IsGranted => Error is null;
}
