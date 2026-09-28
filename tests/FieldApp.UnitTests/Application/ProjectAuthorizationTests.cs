using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Domain.Memberships;

namespace FieldApp.UnitTests.Application;

public sealed class ProjectAuthorizationTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ProjectRoles.Superintendent)]
    [InlineData(ProjectRoles.ProjectManager)]
    [InlineData(ProjectRoles.Administrator)]
    [InlineData(ProjectRoles.TradePartner)]
    public void Every_member_role_may_view_the_project_and_its_reference_data(ProjectRoles role)
    {
        Assert.True(ProjectPolicies.Allows(role, ProjectOperation.ViewProject));
        Assert.True(ProjectPolicies.Allows(role, ProjectOperation.ViewReferenceData));
    }

    [Theory]
    [InlineData(ProjectRoles.ProjectManager, true)]
    [InlineData(ProjectRoles.Administrator, true)]
    [InlineData(ProjectRoles.Superintendent, false)]
    [InlineData(ProjectRoles.TradePartner, false)]
    [InlineData(ProjectRoles.Superintendent | ProjectRoles.ProjectManager, true)]
    public void Only_project_managers_and_administrators_may_manage_areas(ProjectRoles roles, bool allowed)
    {
        Assert.Equal(allowed, ProjectPolicies.Allows(roles, ProjectOperation.ManageAreas));
    }

    [Fact]
    public void No_role_satisfies_any_operation()
    {
        foreach (var operation in Enum.GetValues<ProjectOperation>())
        {
            Assert.False(ProjectPolicies.Allows(ProjectRoles.None, operation));
        }
    }

    [Fact]
    public async Task Knowing_a_project_id_without_membership_is_concealed_as_not_found()
    {
        var data = new InMemoryReferenceData();
        var otherUser = Guid.NewGuid();
        var projectId = data.AddProjectWithMember(otherUser, ProjectRoles.Administrator);
        var authorizer = new ProjectAuthorizer(data, new FakeCurrentUser(Guid.NewGuid()));

        var result = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewProject, CancellationToken);

        Assert.False(result.IsGranted);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Invisible_project_is_not_found_even_for_an_operation_that_would_be_forbidden()
    {
        var data = new InMemoryReferenceData();
        var projectId = data.AddProjectWithMember(Guid.NewGuid(), ProjectRoles.Administrator);
        var authorizer = new ProjectAuthorizer(data, new FakeCurrentUser(Guid.NewGuid()));

        var result = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ManageAreas, CancellationToken);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Visible_project_with_unpermitted_operation_is_forbidden()
    {
        var data = new InMemoryReferenceData();
        var userId = Guid.NewGuid();
        var projectId = data.AddProjectWithMember(userId, ProjectRoles.Superintendent);
        var authorizer = new ProjectAuthorizer(data, new FakeCurrentUser(userId));

        var result = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ManageAreas, CancellationToken);

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.NotNull(result.Membership);
    }

    [Fact]
    public async Task Member_with_permitted_operation_is_granted()
    {
        var data = new InMemoryReferenceData();
        var userId = Guid.NewGuid();
        var projectId = data.AddProjectWithMember(userId, ProjectRoles.ProjectManager);
        var authorizer = new ProjectAuthorizer(data, new FakeCurrentUser(userId));

        var result = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ManageAreas, CancellationToken);

        Assert.True(result.IsGranted);
    }

    [Fact]
    public async Task Request_without_application_user_is_a_programming_error()
    {
        var authorizer = new ProjectAuthorizer(new InMemoryReferenceData(), new FakeCurrentUser(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authorizer.AuthorizeAsync(Guid.NewGuid(), ProjectOperation.ViewProject, CancellationToken));
    }
}
