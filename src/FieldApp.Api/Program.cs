using System.Text.Json.Serialization;
using FieldApp.Api.Authentication;
using FieldApp.Api.Correlation;
using FieldApp.Api.Database;
using FieldApp.Api.Endpoints;
using FieldApp.Api.Health;
using FieldApp.Application;
using FieldApp.Application.Abstractions;
using FieldApp.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

// `migrate [--seed-demo-data]` runs the database command and exits instead of serving HTTP.
var databaseCommand = DatabaseCommand.Parse(args);

var builder = WebApplication.CreateBuilder(databaseCommand is null ? args : []);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFieldAppAuthentication();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807 Problem Details for every error response, carrying the correlation ID.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.GetCorrelationId();
        if (correlationId is not null)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "HB Observations API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

// TLS terminates at the platform ingress. Only proxies in configured networks are trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var knownNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
    foreach (var network in knownNetworks.Where(n => !string.IsNullOrWhiteSpace(n)))
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

var app = builder.Build();

// Persona authentication must never be active outside Development.
AuthenticationSetup.EnsureSafeConfiguration(app.Environment, app.Configuration);

if (databaseCommand is not null)
{
    return await databaseCommand.RunAsync(app.Services, app.Lifetime.ApplicationStopping);
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapReferenceDataEndpoints();

if (AuthenticationSetup.IsDevelopmentMode(app.Configuration))
{
    app.MapDevelopmentPersonaEndpoints();
}

// Unmatched API routes return Problem Details, never the SPA shell.
app.MapFallback("/api/{**path}", () => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "Resource not found."));

// Client-side routes are served by the compiled React application.
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
