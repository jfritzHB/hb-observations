using FieldApp.Domain.Common;

namespace FieldApp.Domain.Memberships;

/// <summary>
/// Grants a user one or more roles on a project. All project authorization derives from an active membership.
/// Trade Partner membership is company scoped and cannot be combined with employee roles.
/// </summary>
public sealed class ProjectMembership
{
    private ProjectMembership()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRoles Roles { get; private set; }

    public Guid? CompanyId { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static ProjectMembership Create(Guid id, Guid projectId, Guid userId, ProjectRoles roles, Guid? companyId)
    {
        var membership = new ProjectMembership
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            ProjectId = Guard.RequiredId(projectId, nameof(ProjectId)),
            UserId = Guard.RequiredId(userId, nameof(UserId)),
            IsActive = true,
        };

        membership.ChangeRoles(roles, companyId);
        return membership;
    }

    public void ChangeRoles(ProjectRoles roles, Guid? companyId)
    {
        if (roles == ProjectRoles.None || (roles & ~ProjectRolesExtensions.All) != 0)
        {
            throw new DomainException("A membership requires at least one valid role.", nameof(Roles));
        }

        if (roles.HasFlag(ProjectRoles.TradePartner))
        {
            if (roles != ProjectRoles.TradePartner)
            {
                throw new DomainException("A Trade Partner membership cannot include employee roles.", nameof(Roles));
            }

            if (companyId is null || companyId == Guid.Empty)
            {
                throw new DomainException("A Trade Partner membership must be scoped to a company.", nameof(CompanyId));
            }
        }

        Roles = roles;
        CompanyId = companyId == Guid.Empty ? null : companyId;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
