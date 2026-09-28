namespace FieldApp.Domain.Memberships;

/// <summary>Roles a user holds within one project (docs/01-product-requirements.md).</summary>
[Flags]
public enum ProjectRoles
{
    None = 0,
    Superintendent = 1,
    ProjectManager = 2,
    Administrator = 4,
    TradePartner = 8,
}

public static class ProjectRolesExtensions
{
    public const ProjectRoles All =
        ProjectRoles.Superintendent | ProjectRoles.ProjectManager | ProjectRoles.Administrator | ProjectRoles.TradePartner;

    public static IReadOnlyList<ProjectRoles> ToList(this ProjectRoles roles) =>
        [.. Enum.GetValues<ProjectRoles>().Where(role => role != ProjectRoles.None && roles.HasFlag(role))];
}
