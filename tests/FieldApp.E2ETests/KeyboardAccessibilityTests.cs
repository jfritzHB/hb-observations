using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

/// <summary>The rapid-capture screen works from a keyboard, with visible, unobscured focus.</summary>
public sealed class KeyboardAccessibilityTests
{
    [Fact]
    public async Task Rapid_capture_is_operable_by_keyboard_with_visible_unobscured_focus()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        var page = phone.Page;
        var mosEisley = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{mosEisley}/new-item");

        // Location combobox: type, arrow through results, Escape closes, Enter selects.
        var where = page.GetByRole(AriaRole.Combobox, new() { Name = "Where?" });
        await TabUntilFocusedAsync(page, where);
        await AssertVisibleFocusAsync(page, "Where? combobox");
        await page.Keyboard.TypeAsync("office");
        await Expect(where).ToHaveAttributeAsync("aria-expanded", "true");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        var office202 = page.GetByRole(AriaRole.Option, new() { Name = "Building A / Level 2 / Office 202" });
        await Expect(office202).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(where).ToHaveAttributeAsync("aria-activedescendant", await office202.GetAttributeAsync("id") ?? "missing");
        await phone.AssertAccessibleAsync("Combobox with active option");

        await page.Keyboard.PressAsync("Escape");
        await Expect(where).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(where).ToHaveValueAsync("office");

        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator(".location-area")).ToContainTextAsync("Building A / Level 2 / Office 201");
        await Expect(where).ToBeFocusedAsync();

        // Detail typed after the Area is kept.
        await page.Keyboard.TypeAsync("North wall");
        await Expect(where).ToHaveValueAsync("North wall");

        // Trade chip via Space; focus must not hide under the docked camera or bottom navigation.
        var drywall = page.GetByRole(AriaRole.Button, new() { Name = "Drywall", Exact = true });
        await TabUntilFocusedAsync(page, drywall);
        await AssertVisibleFocusAsync(page, "Trade chip");
        await PhoneSession.AssertNotObscuredAsync(drywall, "Focused trade chip");
        await page.Keyboard.PressAsync("Space");
        await Expect(drywall).ToHaveAttributeAsync("aria-pressed", "true");

        // Type via radio group arrow keys.
        var observation = page.GetByRole(AriaRole.Radio, new() { Name = "Observation" });
        await TabUntilFocusedAsync(page, observation);
        await page.Keyboard.PressAsync("Space");
        await AssertVisibleFocusAsync(page, "Type option", viaParentLabel: true);
        await page.Keyboard.PressAsync("ArrowRight");
        var punchList = page.GetByRole(AriaRole.Radio, new() { Name = "Punch List" });
        await Expect(punchList).ToBeCheckedAsync();
        await PhoneSession.AssertNotObscuredAsync(page.Locator("label.segmented__option").Last, "Focused type option");

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
