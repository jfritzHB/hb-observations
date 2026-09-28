using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

/// <summary>Authorization boundaries as a user (and the API) experiences them, using other synthetic personas.</summary>
public sealed class AuthorizationAndPersonaTests
{
    [Fact]
    public async Task Project_manager_does_not_see_anchorhead_and_a_direct_link_reveals_nothing()
    {
        await using var phone = await PhoneSession.StartAsync("project-manager");
        var anchorhead = await phone.ProjectIdAsync("HB-TEST-002");

        await phone.GotoAsync("/projects");
        var projects = phone.Page.GetByRole(AriaRole.List, new() { Name = "Your projects" });
        await Expect(projects.GetByRole(AriaRole.Link)).ToHaveCountAsync(1);
        await Expect(projects).Not.ToContainTextAsync("Anchorhead");

        await phone.GotoAsync($"/projects/{anchorhead}/new-item");
        await Expect(phone.Page.GetByRole(AriaRole.Heading, new() { Name = "Project not found" })).ToBeVisibleAsync();
        await Expect(phone.Page.GetByText("Anchorhead")).ToHaveCountAsync(0);
        await phone.AssertAccessibleAsync("Project not found");
        await phone.ScreenshotAsync("08-project-not-found");
    }

    [Fact]
    public async Task Switching_persona_in_more_changes_the_visible_projects()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        await phone.GotoAsync("/more");

        await Expect(phone.Page.GetByText("Owen Lars (Superintendent)")).ToBeVisibleAsync();
        await phone.AssertNoHorizontalScrollAsync("More");
        await phone.AssertAccessibleAsync("More");
        await phone.ScreenshotAsync("09-more");

        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "Wuher Dunesea" }).TapAsync();

        var projects = phone.Page.GetByRole(AriaRole.List, new() { Name = "Your projects" });
        await Expect(projects.GetByRole(AriaRole.Link)).ToHaveCountAsync(1);
        await Expect(projects).ToContainTextAsync("Mos Eisley Municipal Center");
        await Expect(projects).ToContainTextAsync("Trade Partner");
    }

    [Fact]
    public async Task Api_applies_the_401_403_404_concealment_policy()
    {
        await using var phone = await PhoneSession.StartAsync(persona: null);
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        var tosche = await phone.ProjectIdAsync("HB-TEST-003");

        var anonymous = await phone.Api.GetAsync("/api/v1/projects");
        var concealed = await phone.Api.GetAsync($"/api/v1/projects/{tosche}/trades", As("superintendent"));
        var forbidden = await phone.Api.PostAsync($"/api/v1/projects/{mosEisley}/areas", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Dev-Persona"] = "superintendent" },
            DataObject = new { name = "E2E forbidden area" },
        });

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(404, concealed.Status);
        Assert.Equal(403, forbidden.Status);
    }

    private static APIRequestContextOptions As(string persona) =>
        new() { Headers = new Dictionary<string, string> { ["X-Dev-Persona"] = persona } };
}
