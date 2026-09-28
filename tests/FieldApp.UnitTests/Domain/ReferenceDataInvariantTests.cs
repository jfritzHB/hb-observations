using FieldApp.Domain.Common;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;

namespace FieldApp.UnitTests.Domain;

public sealed class ReferenceDataInvariantTests
{
    [Fact]
    public void Project_normalizes_created_at_to_utc_and_starts_active()
    {
        var project = Project.Create(Guid.NewGuid(), " HB-TEST-900 ", "Synthetic Project", "UTC", new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-7)));

        Assert.Equal("HB-TEST-900", project.Number);
        Assert.Equal(TimeSpan.Zero, project.CreatedAt.Offset);
        Assert.Equal(15, project.CreatedAt.Hour);
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void Project_rejects_unknown_time_zone()
    {
        var ex = Assert.Throws<DomainException>(() => Project.Create(Guid.NewGuid(), "HB-1", "Name", "Mars/Olympus_Mons", DateTimeOffset.UtcNow));

        Assert.Equal(nameof(Project.TimeZoneId), ex.Field);
    }

    [Fact]
    public void Project_requires_an_id()
    {
        Assert.Throws<DomainException>(() => Project.Create(Guid.Empty, "HB-1", "Name", "UTC", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Trade_code_is_upper_cased_and_restricted()
    {
        Assert.Equal("DRY", Trade.Create(Guid.NewGuid(), "dry", "Drywall").Code);
        Assert.Throws<DomainException>(() => Trade.Create(Guid.NewGuid(), "DR Y", "Drywall"));
    }

    [Fact]
    public void Project_trade_mapped_to_active_company_is_available_for_capture()
    {
        var (trade, company, projectTrade) = MappedTrade();

        Assert.True(projectTrade.IsAvailableForCapture(trade, company));
    }

    [Fact]
    public void Unmapped_project_trade_is_not_available_for_capture()
    {
        var trade = Trade.Create(Guid.NewGuid(), "ROOF", "Roofing");
        var projectTrade = ProjectTrade.Create(Guid.NewGuid(), Guid.NewGuid(), trade.Id, responsibleCompanyId: null);

        Assert.False(projectTrade.IsAvailableForCapture(trade, null));
    }

    [Fact]
    public void Disabled_project_trade_is_not_available_for_capture()
    {
        var (trade, company, projectTrade) = MappedTrade();
        projectTrade.Disable();

        Assert.False(projectTrade.IsAvailableForCapture(trade, company));
    }

    [Fact]
    public void Inactive_trade_is_not_available_for_capture()
    {
        var (trade, company, projectTrade) = MappedTrade();
        trade.Deactivate();

        Assert.False(projectTrade.IsAvailableForCapture(trade, company));
    }

    [Fact]
    public void Inactive_responsible_company_makes_trade_unavailable_for_capture()
    {
        var (trade, company, projectTrade) = MappedTrade();
        company.Deactivate();

        Assert.False(projectTrade.IsAvailableForCapture(trade, company));
    }

    [Fact]
    public void A_company_other_than_the_mapped_one_never_satisfies_the_mapping()
    {
        var (trade, _, projectTrade) = MappedTrade();
        var otherCompany = Company.Create(Guid.NewGuid(), "Some Other Synthetic Co.");

        Assert.False(projectTrade.IsAvailableForCapture(trade, otherCompany));
    }

    [Fact]
    public void Assigning_a_responsible_company_makes_an_unmapped_trade_available()
    {
        var trade = Trade.Create(Guid.NewGuid(), "ROOF", "Roofing");
        var company = Company.Create(Guid.NewGuid(), "Synthetic Roofing Co.");
        var projectTrade = ProjectTrade.Create(Guid.NewGuid(), Guid.NewGuid(), trade.Id, null);

        projectTrade.AssignResponsibleCompany(company.Id);

        Assert.True(projectTrade.IsAvailableForCapture(trade, company));
    }

    [Fact]
    public void Membership_requires_at_least_one_role()
    {
        Assert.Throws<DomainException>(() => ProjectMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProjectRoles.None, null));
    }

    [Fact]
    public void Membership_may_hold_several_employee_roles()
    {
        var membership = ProjectMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProjectRoles.Superintendent | ProjectRoles.ProjectManager, null);

        Assert.Equal([ProjectRoles.Superintendent, ProjectRoles.ProjectManager], membership.Roles.ToList());
        Assert.True(membership.IsActive);
    }

    [Fact]
    public void Trade_partner_membership_must_be_company_scoped()
    {
        var ex = Assert.Throws<DomainException>(() =>
            ProjectMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProjectRoles.TradePartner, companyId: null));

        Assert.Equal(nameof(ProjectMembership.CompanyId), ex.Field);
    }

    [Fact]
    public void Trade_partner_membership_cannot_include_employee_roles()
    {
        Assert.Throws<DomainException>(() => ProjectMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProjectRoles.TradePartner | ProjectRoles.ProjectManager, Guid.NewGuid()));
    }

    [Fact]
    public void Undefined_role_bits_are_rejected()
    {
        Assert.Throws<DomainException>(() =>
            ProjectMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (ProjectRoles)64, null));
    }

    private static (Trade Trade, Company Company, ProjectTrade ProjectTrade) MappedTrade()
    {
        var trade = Trade.Create(Guid.NewGuid(), "DRY", "Drywall");
        var company = Company.Create(Guid.NewGuid(), "Synthetic Drywall Co.");
        return (trade, company, ProjectTrade.Create(Guid.NewGuid(), Guid.NewGuid(), trade.Id, company.Id));
    }
}
