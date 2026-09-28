using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;

namespace FieldApp.Application.ReferenceData;

public sealed class ProjectQueries(IReferenceDataReader reader, ProjectAuthorizer authorizer, ICurrentUser currentUser)
{
    /// <summary>Projects in which the caller has an active membership.</summary>
    public async Task<IReadOnlyList<ProjectDto>> ListMineAsync(CancellationToken cancellationToken)
    {
        var projects = await reader.ListProjectsForUserAsync(currentUser.RequireUserId(), cancellationToken);

        return [.. projects
            .OrderBy(row => row.Project.Number, StringComparer.OrdinalIgnoreCase)
            .Select(row => ToDto(row.Project, row.Roles))];
    }

    public async Task<Result<ProjectDto>> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewProject, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var project = await reader.GetProjectAsync(projectId, cancellationToken);
        return project is null
            ? AppError.NotFound("Project not found.")
            : ToDto(project, authorization.Membership!.Roles);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await reader.GetUserAsync(currentUser.RequireUserId(), cancellationToken);
        return user is null ? null : new CurrentUserDto(user.Id, user.DisplayName, user.Email);
    }

    private static ProjectDto ToDto(Project project, ProjectRoles roles) =>
        new(project.Id, project.Number, project.Name, project.TimeZoneId, project.Status, roles.ToList());
}
