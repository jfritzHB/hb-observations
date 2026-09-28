using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace FieldApp.Infrastructure.Storage;

/// <summary>
/// Resolves the private photo container from configuration:
/// <list type="bullet">
/// <item><c>ConnectionStrings:PhotoStorage</c> (Azurite or a storage connection string) takes precedence; or</item>
/// <item><c>Azure:StorageAccountUrl</c> with managed identity / <c>DefaultAzureCredential</c> (preferred in Azure).</item>
/// </list>
/// The container name is <c>Azure:PhotoContainer</c> (default <c>field-item-photos</c>).
/// </summary>
public sealed class PhotoContainerProvider
{
    public const string ConnectionStringName = "PhotoStorage";
    public const string DefaultContainer = "field-item-photos";

    private readonly Lazy<BlobContainerClient?> _container;

    public PhotoContainerProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _container = new Lazy<BlobContainerClient?>(() =>
        {
            var name = configuration["Azure:PhotoContainer"];
            name = string.IsNullOrWhiteSpace(name) ? DefaultContainer : name;

            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new BlobServiceClient(connectionString).GetBlobContainerClient(name);
            }

            var accountUrl = configuration["Azure:StorageAccountUrl"];
            return string.IsNullOrWhiteSpace(accountUrl)
                ? null
                : new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential()).GetBlobContainerClient(name);
        });
    }

    public bool IsConfigured => _container.Value is not null;

    public BlobContainerClient Container =>
        _container.Value ?? throw new InvalidOperationException(
            $"Photo storage is not configured: set ConnectionStrings:{ConnectionStringName} or Azure:StorageAccountUrl.");

    /// <summary>Creates the private container if missing. Run by the migrate command, never on every startup.</summary>
    public async Task EnsureContainerAsync(CancellationToken cancellationToken) =>
        await Container.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.None, cancellationToken: cancellationToken);
}
