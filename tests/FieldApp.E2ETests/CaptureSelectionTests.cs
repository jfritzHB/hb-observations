using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

/// <summary>
/// The Slice 1.1 rapid-capture flow at 360px against a running, seeded instance: location (structured Area and/or
/// Location Detail), Trade with Responsible Company, Observation/Punch List, and the disabled Take Photo boundary,
/// with layout and axe accessibility checks.
/// </summary>
public sealed class CaptureSelectionTests
{
    [Fact]
    public async Task Superintendent_captures_area_plus_detail_trade_and_type_and_reaches_the_photo_boundary()
    {
        await using var phone = await PhoneSession.StartAsync(persona: null);
        var page = phone.Page;

        // Persona, then only authorized projects.
        await phone.GotoAsync("/");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Choose a development persona" })).ToBeVisibleAsync();
        await phone.AssertAccessibleAsync("Persona chooser");
        await page.GetByRole(AriaRole.Button, new() { Name = "Owen Lars" }).TapAsync();

        var projects = page.GetByRole(AriaRole.List, new() { Name = "Your projects" });
        await Expect(projects.GetByRole(AriaRole.Link)).ToHaveCountAsync(2);
        await Expect(projects).Not.ToContainTextAsync("Tosche Station");
        await projects.GetByRole(AriaRole.Link, new() { Name = "Mos Eisley Municipal Center" }).TapAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "New Item" }).TapAsync();

        // Capture screen: compact project context, no numbered steps, no summary.
        var where = page.GetByRole(AriaRole.Combobox, new() { Name = "Where?" });
        await Expect(where).ToBeVisibleAsync();
        await Expect(page.Locator(".context-bar")).ToContainTextAsync("Mos Eisley Municipal Center");
        await Expect(page.GetByRole(AriaRole.Region, new() { Name = "Selections" })).ToHaveCountAsync(0);
        var camera = page.GetByRole(AriaRole.Button, new() { Name = "Take photo" });
        await Expect(camera).ToBeDisabledAsync();
        await phone.AssertNoHorizontalScrollAsync("Capture (empty)");
        await phone.AssertAccessibleAsync("Capture (empty)");
        await LogScrollAsync(page, "Capture (empty)");
        await phone.ScreenshotAsync("11-capture-empty");

        // Where: type to search, tap the structured Area, then add a detail.
        await where.TapAsync();
        await where.PressSequentiallyAsync("201");
        var results = page.GetByRole(AriaRole.Listbox, new() { Name = "Matching areas" });
        await Expect(results.GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
        await phone.AssertAccessibleAsync("Location results open");
        await phone.ScreenshotAsync("12-location-search");
        await results.GetByRole(AriaRole.Option, new() { Name = "Building A / Level 2 / Office 201" }).TapAsync();
        await Expect(page.Locator(".location-area")).ToContainTextAsync("Building A / Level 2 / Office 201");
        await Expect(where).ToHaveValueAsync(string.Empty);
        await where.PressSequentiallyAsync("North wall");

        // Trade and its responsible company (read-only, no company picker).
        await page.GetByRole(AriaRole.Button, new() { Name = "Drywall", Exact = true }).TapAsync();
        await Expect(page.Locator(".responsible")).ToHaveTextAsync("Responsible: Dune Sea Drywall Co.");
        await Expect(page.GetByRole(AriaRole.Combobox)).ToHaveCountAsync(1); // Only the location field.

        // Type.
        await page.GetByText("Punch List", new() { Exact = true }).TapAsync();
        await Expect(page.GetByRole(AriaRole.Radio, new() { Name = "Punch List" })).ToBeCheckedAsync();

        // Take Photo: prominent but still a disabled Slice 2 boundary, never covered by the navigation.
        await Expect(camera).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("camera-button--ready"));
        await Expect(camera).ToBeDisabledAsync();
        await Expect(page.GetByText("Photo capture arrives in the next release")).ToBeVisibleAsync();
        await PhoneSession.AssertNotObscuredAsync(camera, "Take photo");
        await Expect(where).ToHaveValueAsync("North wall");

        await phone.AssertNoHorizontalScrollAsync("Capture (ready)");
        await phone.AssertAccessibleAsync("Capture (ready)");
        await LogScrollAsync(page, "Capture (ready)");
        await phone.ScreenshotAsync("13-capture-ready");
    }

    [Fact]
    public async Task Location_detail_alone_is_accepted_and_never_creates_an_area()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        var areasBefore = await AreaPathsAsync(phone, mosEisley);
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        var where = phone.Page.GetByRole(AriaRole.Combobox, new() { Name = "Where?" });
        await where.TapAsync();
        await where.PressSequentiallyAsync("Unit 214");
        await Expect(where).ToHaveAttributeAsync("aria-expanded", "false"); // No matching structured area.
        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "Electrical", Exact = true }).TapAsync();
        await phone.Page.GetByText("Observation", new() { Exact = true }).TapAsync();

        await Expect(phone.Page.GetByRole(AriaRole.Button, new() { Name = "Take photo" }))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("camera-button--ready"));
        await Expect(phone.Page.Locator(".location-area")).ToHaveCountAsync(0);

        var areasAfter = await AreaPathsAsync(phone, mosEisley);
        Assert.Equal(areasBefore, areasAfter);
        Assert.DoesNotContain(areasAfter, path => path.Contains("Unit 214", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Area_can_be_chosen_without_typing_and_recent_locations_repeat_in_one_tap()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var page = phone.Page;
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        await page.GetByRole(AriaRole.Button, new() { Name = "Browse areas" }).TapAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Browse areas" });
        await Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Building B / Level 1 / Storage (Closed)" })).ToHaveCountAsync(0);
        await phone.AssertAccessibleAsync("Browse areas");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Building A / Level 2", Exact = true }).TapAsync();
        await Expect(page.Locator(".location-area")).ToContainTextAsync("Building A / Level 2");

        // Next visit on this device: the location is a one-tap recent chip.
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");
        var recent = page.GetByRole(AriaRole.Group, new() { Name = "Recent locations" });
        await recent.GetByRole(AriaRole.Button, new() { Name = "Building A / Level 2", Exact = true }).TapAsync();
        await Expect(page.Locator(".location-area")).ToContainTextAsync("Building A / Level 2");
    }

    [Fact]
    public async Task All_trades_lists_only_capture_ready_trades_with_their_companies()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "More trades (7 total)" }).TapAsync();
        var sheet = phone.Page.GetByRole(AriaRole.Dialog, new() { Name = "All trades" });

        await Expect(sheet.GetByRole(AriaRole.Button)).ToHaveCountAsync(8); // 7 trades + close.
        await Expect(sheet).ToContainTextAsync("Responsible: Hoth Climate Mechanical");
        foreach (var excluded in new[] { "Roofing", "Concrete", "Glazing", "Fireproofing" })
        {
            await Expect(sheet).Not.ToContainTextAsync(excluded);
        }

        await phone.AssertAccessibleAsync("All trades sheet");
        await phone.ScreenshotAsync("14-all-trades");
    }

    private static async Task<List<string>> AreaPathsAsync(PhoneSession phone, string projectId)
    {
        var response = await phone.Api.GetAsync($"/api/v1/projects/{projectId}/areas", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Dev-Persona"] = "administrator" },
        });
        Assert.True(response.Ok);
        var json = await response.JsonAsync();
        return [.. json!.Value.EnumerateArray().Select(area => area.GetProperty("path").GetString()!)];
    }

    /// <summary>Records how far the capture screen extends beyond one phone screen (for the UX report).</summary>
    private static async Task LogScrollAsync(IPage page, string where)
    {
        var overflow = await page.EvaluateAsync<int>("document.documentElement.scrollHeight - window.innerHeight");
        TestContext.Current.TestOutputHelper?.WriteLine($"{where}: content extends {Math.Max(0, overflow)}px beyond a 740px-high screen.");
    }
}
