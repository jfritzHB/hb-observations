using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

/// <summary>The capture selection works from a keyboard, with a clearly visible focus indicator.</summary>
public sealed class KeyboardAccessibilityTests
{
    [Fact]
    public async Task Capture_selection_is_operable_by_keyboard_with_visible_focus()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var page = phone.Page;
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Choose area" })).ToBeVisibleAsync();

        // Tab to the area control: it must show a visible focus ring.
        var areaButton = page.GetByRole(AriaRole.Button, new() { Name = "Choose area" });
        await TabUntilFocusedAsync(page, areaButton);
        await AssertVisibleFocusAsync(page, "Area button");

        // Enter opens the sheet with focus inside; Escape closes it and returns focus to the trigger.
        await page.Keyboard.PressAsync("Enter");
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Choose area" });
        await Expect(sheet).ToBeVisibleAsync();
        Assert.True(await sheet.EvaluateAsync<bool>("dialog => dialog.contains(document.activeElement)"), "Focus should move into the sheet.");
        await page.Keyboard.PressAsync("Escape");
        await Expect(sheet).ToBeHiddenAsync();
        await Expect(areaButton).ToBeFocusedAsync();

        // Choose an area from the keyboard.
        await page.Keyboard.PressAsync("Enter");
        var office = sheet.GetByRole(AriaRole.Button, new() { Name = "Building A / Level 2 / Office 201", Exact = true });
        await TabUntilFocusedAsync(page, office);
        await AssertVisibleFocusAsync(page, "Area option");
        await page.Keyboard.PressAsync("Enter");
        await Expect(sheet).ToBeHiddenAsync();

        // Trade chip via Space.
        var drywall = page.GetByRole(AriaRole.Button, new() { Name = "Drywall", Exact = true });
        await TabUntilFocusedAsync(page, drywall);
        await AssertVisibleFocusAsync(page, "Trade chip");
        await page.Keyboard.PressAsync("Space");
        await Expect(drywall).ToHaveAttributeAsync("aria-pressed", "true");

        // Type via radio group arrow keys.
        var observation = page.GetByRole(AriaRole.Radio, new() { Name = "Observation" });
        await TabUntilFocusedAsync(page, observation);
        await page.Keyboard.PressAsync("Space");
        await AssertVisibleFocusAsync(page, "Type option", viaParentLabel: true);
        await page.Keyboard.PressAsync("ArrowRight");
        await Expect(page.GetByRole(AriaRole.Radio, new() { Name = "Punch List" })).ToBeCheckedAsync();

        await Expect(page.GetByRole(AriaRole.Region, new() { Name = "Selections" })).ToContainTextAsync("Dune Sea Drywall Co.");
        await phone.AssertAccessibleAsync("Keyboard flow end state");
    }

    private static async Task TabUntilFocusedAsync(IPage page, ILocator target, int maxTabs = 60)
    {
        for (var i = 0; i < maxTabs; i++)
        {
            if (await target.EvaluateAsync<bool>("element => element === document.activeElement"))
            {
                return;
            }

            await page.Keyboard.PressAsync("Tab");
        }

        Assert.Fail($"Could not reach {target} with the Tab key.");
    }

    /// <summary>The focused element (or its label, for visually hidden radios) must draw a non-zero outline.</summary>
    private static async Task AssertVisibleFocusAsync(IPage page, string what, bool viaParentLabel = false)
    {
        var outline = await page.EvaluateAsync<string>(
            """
            viaLabel => {
              const focused = document.activeElement;
              const element = viaLabel ? focused.closest('label') : focused;
              const style = getComputedStyle(element);
              return `${style.outlineStyle} ${style.outlineWidth}`;
            }
            """,
            viaParentLabel);

        Assert.False(outline.StartsWith("none", StringComparison.Ordinal) || outline.EndsWith(" 0px", StringComparison.Ordinal), $"{what} has no visible focus indicator ({outline}).");
    }
}
