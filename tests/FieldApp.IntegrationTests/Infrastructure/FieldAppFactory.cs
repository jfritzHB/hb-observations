using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory with a temporary web root containing a stand-in SPA shell, and an
/// explicit (possibly empty) application database connection string.
/// </summary>
public sealed class FieldAppFactory : WebApplicationFactory<Program>
{
    public const string SpaShellMarker = "fieldapp-test-spa-shell";

    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("fieldapp-webroot-");

    public string AppDbConnectionString { get; init; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(_webRoot.FullName, "index.html"),
            $"<!doctype html><html><body>{SpaShellMarker}</body></html>");

        builder.UseEnvironment("Development");
        builder.UseWebRoot(_webRoot.FullName);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDb"] = AppDbConnectionString,
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _webRoot.Exists)
        {
            _webRoot.Delete(recursive: true);
        }
    }
}
