using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

/// <summary>
/// The Slice 1 mobile flow at 360px against a running, seeded instance: persona selection, authorized projects,
/// capture selection (Area, Trade, Responsible Company, Type) up to the disabled camera boundary, with layout and
/// axe accessibility checks at each step.
/// </summary>
public sealed class CaptureSelectionTests
{
    [Fact]
    public async Task Superintendent_selects_area_trade_and_type_without_typing_and_reaches_the_camera_boundary()
    {
        await using var phone = await PhoneSession.StartAsync(persona: null);
        var page = phone.Page;

        // 1. Choose the synthetic Superintendent persona.
        await phone.GotoAsync("/");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Choose a development persona" })).ToBeVisibleAsync();
        await phone.AssertAccessibleAsync("Persona chooser");
        await phone.ScreenshotAsync("01-persona-chooser");
        await page.GetByRole(AriaRole.Button, new() { Name = "Owen Lars" }).TapAsync();

        // 2. Only authorized projects are listed.
        var projects = page.GetByRole(AriaRole.List, new() { Name = "Your projects" });
        await Expect(projects.GetByRole(AriaRole.Link)).ToHaveCountAsync(2);
        await Expect(projects).ToContainTextAsync("Mos Eisley Municipal Center");
        await Expect(projects).ToContainTextAsync("Anchorhead Water Treatment Plant");
        await Expect(projects).Not.ToContainTextAsync("Tosche Station");
        await phone.AssertNoHorizontalScrollAsync("Projects");
        await phone.AssertAccessibleAsync("Projects");
        await phone.ScreenshotAsync("02-projects");

        // 3-4. Open Mos Eisley Municipal Center and tap New Item.
        await projects.GetByRole(AriaRole.Link, new() { Name = "Mos Eisley Municipal Center" }).TapAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Mos Eisley Municipal Center" })).ToBeVisibleAsync();
        await phone.AssertNoHorizontalScrollAsync("Project home");
        await phone.AssertAccessibleAsync("Project home");
        await phone.ScreenshotAsync("03-project-home");
        await page.GetByRole(AriaRole.Link, new() { Name = "New Item" }).TapAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "New item", Level = 1 })).ToBeVisibleAsync();
        var camera = page.GetByRole(AriaRole.Button, new() { Name = "Take photo" });
        await Expect(camera).ToHaveAttributeAsync("aria-disabled", "true");
        await phone.AssertNoHorizontalScrollAsync("New item (empty)");
        await phone.AssertAccessibleAsync("New item (empty)");
        await phone.ScreenshotAsync("04-new-item-empty");

        // 5. Area: tap through the hierarchical list (no typing).
        await page.GetByRole(AriaRole.Button, new() { Name = "Choose area" }).TapAsync();
        var areaSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Choose area" });
        await Expect(areaSheet).ToBeVisibleAsync();
        await Expect(areaSheet.GetByRole(AriaRole.Button, new() { Name = "Building B / Level 1 / Storage (Closed)" })).ToHaveCountAsync(0);
        await phone.AssertAccessibleAsync("Area sheet");
        await phone.ScreenshotAsync("05-area-sheet");
        await areaSheet.GetByRole(AriaRole.Button, new() { Name = "Building A / Level 2 / Office 201", Exact = true }).TapAsync();
        await Expect(areaSheet).ToBeHiddenAsync();

        // 6-7. Trade: Drywall, and its responsible company appears immediately (read-only, no second picker).
        await page.GetByRole(AriaRole.Button, new() { Name = "Drywall", Exact = true }).TapAsync();
        await Expect(page.Locator(".responsible")).ToContainTextAsync("Dune Sea Drywall Co.");
        await Expect(page.GetByRole(AriaRole.Combobox)).ToHaveCountAsync(0);

        // 8. Type: Punch List.
        await page.GetByText("Punch List", new() { Exact = true }).TapAsync();
        await Expect(page.GetByRole(AriaRole.Radio, new() { Name = "Punch List" })).ToBeCheckedAsync();

        // 9. Everything summarized above the camera.
        var summary = page.GetByRole(AriaRole.Region, new() { Name = "Selections" });
        await Expect(summary).ToContainTextAsync("Building A / Level 2 / Office 201");
        await Expect(summary).ToContainTextAsync("Drywall");
        await Expect(summary).ToContainTextAsync("Dune Sea Drywall Co.");
        await Expect(summary).ToContainTextAsync("Punch List");

        // 10. The camera is a disabled Slice 2 boundary; nothing is captured or faked.
        await Expect(camera).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(page.GetByText("Selections complete. Photo capture is not available yet")).ToBeVisibleAsync();
        await Expect(camera).ToBeDisabledAsync(); // Playwright honours aria-disabled: it cannot be activated.
        await PhoneSession.AssertNotObscuredAsync(camera, "Camera button");

        await phone.AssertNoHorizontalScrollAsync("New item (complete)");
        await phone.AssertAccessibleAsync("New item (complete)");
        await phone.ScreenshotAsync("06-new-item-complete");
    }

    [Fact]
    public async Task Area_search_is_optional_and_filters_by_every_term()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "Choose area" }).TapAsync();
        var sheet = phone.Page.GetByRole(AriaRole.Dialog, new() { Name = "Choose area" });
        await sheet.GetByRole(AriaRole.Searchbox).FillAsync("level 2 office");

        await Expect(sheet.GetByRole(AriaRole.Status)).ToHaveTextAsync("2 areas match");
        await Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Building A / Level 2 / Office 202" })).ToBeVisibleAsync();
        await phone.AssertNoHorizontalScrollAsync("Area search");
        await phone.AssertAccessibleAsync("Area search");
    }

    [Fact]
    public async Task All_trades_view_lists_only_capture_ready_trades_with_their_companies()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "View all trades (7)" }).TapAsync();
        var sheet = phone.Page.GetByRole(AriaRole.Dialog, new() { Name = "Choose trade" });

        await Expect(sheet.GetByRole(AriaRole.Button)).ToHaveCountAsync(8); // 7 trades + close.
        await Expect(sheet).ToContainTextAsync("Responsible: Hoth Climate Mechanical");
        foreach (var excluded in new[] { "Roofing", "Concrete", "Glazing", "Fireproofing" })
        {
            await Expect(sheet).Not.ToContainTextAsync(excluded);
        }

        await phone.AssertAccessibleAsync("Trade sheet");
        await phone.ScreenshotAsync("07-trade-sheet");
    }
}
