using FieldApp.Domain.Memberships;

namespace FieldApp.Application.Authorization;

/// <summary>
/// Which project roles satisfy each operation. Permissions are only ever granted through an active
/// <see cref="ProjectMembership"/>; this table can be tightened without changing use cases or stored data.
/// </summary>
public static class ProjectPolicies
{
    public static ProjectRoles RolesFor(ProjectOperation operation) => operation switch
    {
        ProjectOperation.ViewProject => ProjectRolesExtensions.All,
        ProjectOperation.ViewReferenceData => ProjectRolesExtensions.All,
        ProjectOperation.ManageAreas => ProjectRoles.ProjectManager | ProjectRoles.Administrator,
        ProjectOperation.CaptureItems => ProjectRoles.Superintendent | ProjectRoles.ProjectManager | ProjectRoles.Administrator,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown project operation."),
    };

    public static bool Allows(ProjectRoles memberRoles, ProjectOperation operation) =>
        (memberRoles & RolesFor(operation)) != ProjectRoles.None;
}
