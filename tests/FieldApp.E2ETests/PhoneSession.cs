using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace FieldApp.E2ETests;

/// <summary>
/// A Chromium page emulating a phone in portrait (360 x 740 CSS px, touch), optionally signed in as a synthetic
/// development persona, with helpers for layout, accessibility (axe) and screenshot checks.
/// </summary>
internal sealed class PhoneSession : IAsyncDisposable
{
    public const int PhoneWidth = 360;
    public const string PersonaStorageKey = "fieldapp.devPersona";

    private static readonly string[] _axeTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;

    private PhoneSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, string baseUrl)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        Page = page;
        BaseUrl = baseUrl;
    }

    public static string? ConfiguredBaseUrl => Environment.GetEnvironmentVariable("FIELDAPP_E2E_BASE_URL")?.TrimEnd('/');

    public IPage Page { get; }

    public string BaseUrl { get; }

    public IAPIRequestContext Api => _context.APIRequest;

    public static async Task<PhoneSession> StartAsync(string? persona)
    {
        var baseUrl = ConfiguredBaseUrl;
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "Set FIELDAPP_E2E_BASE_URL to a running, seeded instance, e.g. http://localhost:8080.");

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = baseUrl,
            ViewportSize = new ViewportSize { Width = PhoneWidth, Height = 740 },
            DeviceScaleFactor = 2,
            IsMobile = true,
            HasTouch = true,
        });

        if (persona is not null)
        {
            // Only seeds the choice; switching persona in the app still works afterwards.
            await context.AddInitScriptAsync(
                $"if (!window.localStorage.getItem('{PersonaStorageKey}')) window.localStorage.setItem('{PersonaStorageKey}', '{persona}');");
        }

        var page = await context.NewPageAsync();
        return new PhoneSession(playwright, browser, context, page, baseUrl!);
    }

    public async Task GotoAsync(string path)
    {
        await Page.GotoAsync(path);
    }

    public async Task AssertNoHorizontalScrollAsync(string where)
    {
        var scrollWidth = await Page.EvaluateAsync<int>("document.documentElement.scrollWidth");
        var clientWidth = await Page.EvaluateAsync<int>("document.documentElement.clientWidth");
        Assert.True(scrollWidth <= clientWidth, $"{where}: page is {scrollWidth}px wide in a {clientWidth}px viewport (horizontal scroll).");
    }

    /// <summary>After scrolling it into view, the element itself is what a tap at its centre would hit (not the fixed nav).</summary>
    public static async Task AssertNotObscuredAsync(ILocator locator, string what)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var hit = await locator.EvaluateAsync<bool>(
            """
            element => {
              const box = element.getBoundingClientRect();
              const top = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
              return top !== null && element.contains(top);
            }
            """);
        Assert.True(hit, $"{what} is covered by another element (for example the fixed bottom navigation).");
    }

    /// <summary>Runs axe-core (WCAG 2.x A/AA rules) against the current page state.</summary>
    public async Task AssertAccessibleAsync(string where)
    {
        var result = await Page.RunAxe(new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions { Type = "tag", Values = [.. _axeTags] },
        });

        if (result.Violations.Length == 0)
        {
            return;
        }

        var report = new StringBuilder($"{where}: {result.Violations.Length} accessibility violation(s):");
        foreach (var violation in result.Violations)
        {
            report.AppendLine().Append(" - ").Append(violation.Id).Append(" (").Append(violation.Impact).Append("): ").Append(violation.Help);
            foreach (var node in violation.Nodes.Take(3))
            {
                report.AppendLine().Append("     ").Append(node.Html);
            }
        }

        Assert.Fail(report.ToString());
    }

    /// <summary>Saves a screenshot when FIELDAPP_E2E_SCREENSHOTS names a directory (for manual review).</summary>
    public async Task ScreenshotAsync(string name)
    {
        var directory = Environment.GetEnvironmentVariable("FIELDAPP_E2E_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        // Viewport-sized: shows exactly what a phone user sees, including the fixed navigation.
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, $"{name}.png") });
    }

    /// <summary>The ID of a seeded project, looked up through the API as the Administrator persona.</summary>
    public async Task<string> ProjectIdAsync(string projectNumber)
    {
        var response = await Api.GetAsync("/api/v1/projects", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Dev-Persona"] = "administrator" },
        });
        Assert.True(response.Ok, $"Listing projects as administrator failed: {response.Status}");

        var projects = await response.JsonAsync();
        foreach (var project in projects!.Value.EnumerateArray())
        {
            if (project.GetProperty("number").GetString() == projectNumber)
            {
                return project.GetProperty("id").GetString()!;
            }
        }

        throw new InvalidOperationException($"Seeded project {projectNumber} was not found. Is the demo data seeded?");
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
