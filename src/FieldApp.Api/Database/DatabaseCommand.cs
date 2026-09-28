using FieldApp.Infrastructure.Persistence;
using FieldApp.Infrastructure.Seeding;
using FieldApp.Infrastructure.Storage;

namespace FieldApp.Api.Database;

/// <summary>
/// <c>migrate [--seed-demo-data]</c>: applies EF Core migrations (and optionally the synthetic demo data), then
/// exits. The same image runs it as a Container Apps Job or pipeline step before a new revision receives traffic,
/// and as the <c>migrate</c> service in Docker Compose. The web application never migrates on startup.
/// </summary>
public sealed partial class DatabaseCommand(bool seedDemoData)
{
    public const string Verb = "migrate";

    public bool SeedDemoData { get; } = seedDemoData;
    public const string SeedFlag = "--seed-demo-data";

    public static DatabaseCommand? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || !string.Equals(args[0], Verb, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var options = args.Skip(1).ToList();
        var unknown = options.Where(option => !string.Equals(option, SeedFlag, StringComparison.OrdinalIgnoreCase)).ToList();
        return unknown.Count > 0
            ? throw new ArgumentException($"Unknown option(s) for '{Verb}': {string.Join(' ', unknown)}. Usage: {Verb} [{SeedFlag}]")
            : new DatabaseCommand(options.Count > 0);
    }

    public async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseCommand>>();

        try
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);

            // The private photo container is provisioned by this controlled step, not by application startup.
            var photoContainers = scope.ServiceProvider.GetRequiredService<PhotoContainerProvider>();
            if (photoContainers.IsConfigured)
            {
                await photoContainers.EnsureContainerAsync(cancellationToken);
            }

            if (SeedDemoData)
            {
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken);
            }

            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database command failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
