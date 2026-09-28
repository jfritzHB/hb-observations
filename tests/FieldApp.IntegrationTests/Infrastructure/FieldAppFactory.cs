using System.Text.Json;
using System.Text.Json.Serialization;
using FieldApp.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory with a temporary web root containing a stand-in SPA shell, an explicit (possibly
/// empty) application database connection string, and an optional environment/configuration override.
/// </summary>
public sealed class FieldAppFactory : WebApplicationFactory<Program>
{
    public const string SpaShellMarker = "fieldapp-test-spa-shell";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("fieldapp-webroot-");

    public string AppDbConnectionString { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = "Development";

    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    public HttpClient CreateClientAs(string? persona)
    {
        var client = CreateClient();
        if (persona is not null)
        {
            client.DefaultRequestHeaders.Add(DevelopmentPersonaAuthenticationHandler.HeaderName, persona);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(_webRoot.FullName, "index.html"),
            $"<!doctype html><html><body>{SpaShellMarker}</body></html>");

        builder.UseEnvironment(EnvironmentName);
        builder.UseWebRoot(_webRoot.FullName);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>(Settings)
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
