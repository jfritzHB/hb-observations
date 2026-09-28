using System.Diagnostics;

namespace FieldApp.Api.Correlation;

/// <summary>
/// Accepts a well-formed <c>X-Correlation-Id</c> from the caller or generates one, echoes it on the
/// response, adds it to the logging scope and makes it available through <see cref="HttpContextCorrelationExtensions"/>.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const int MaxLength = 64;

    private static readonly Func<ILogger, string, IDisposable?> _correlationScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = IsValid(context.Request.Headers[HeaderName].ToString())
            ? context.Request.Headers[HeaderName].ToString()
            : Guid.NewGuid().ToString();

        context.Items[HttpContextCorrelationExtensions.ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_correlationScope(logger, correlationId))
        {
            await next(context);
        }
    }

    /// <summary>Only short, header-safe identifiers are trusted; anything else is replaced rather than reflected.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
