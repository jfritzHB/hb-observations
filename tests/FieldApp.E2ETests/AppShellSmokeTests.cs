using Microsoft.Playwright;

namespace FieldApp.E2ETests;

public sealed class AppShellSmokeTests
{
    private const int PhoneWidth = 360;

    private static string? BaseUrl => Environment.GetEnvironmentVariable("FIELDAPP_E2E_BASE_URL");

    [Fact]
    public async Task App_shell_renders_at_phone_width_without_horizontal_scroll()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(BaseUrl), "Set FIELDAPP_E2E_BASE_URL to a running instance, e.g. http://localhost:8080.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = PhoneWidth, Height = 740 },
            IsMobile = true,
            HasTouch = true,
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(BaseUrl!);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "HB Observations" })).ToBeVisibleAsync();
        var scrollWidth = await page.EvaluateAsync<int>("document.documentElement.scrollWidth");
        Assert.True(scrollWidth <= PhoneWidth, $"Page is {scrollWidth}px wide at a {PhoneWidth}px viewport.");
    }
}
