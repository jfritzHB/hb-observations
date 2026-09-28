using FieldApp.Domain.Common;
using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

/// <summary>Tracked access to field items (with their photos) and idempotency records.</summary>
public interface IFieldItemStore
{
    Task<FieldItem?> FindAsync(Guid itemId, CancellationToken cancellationToken);

    Task<FieldItem?> FindByClientDraftAsync(Guid projectId, Guid createdByUserId, Guid clientDraftId, CancellationToken cancellationToken);

    Task<IdempotencyRecord?> FindIdempotencyRecordAsync(Guid userId, string scope, string key, CancellationToken cancellationToken);

    void Add(FieldItem item);

    void Add(IdempotencyRecord record);
}

/// <summary>
/// Private object storage for photos (Azure Blob Storage in every environment; Azurite locally). Keys are generated
/// by the server; objects are never exposed through public URLs.
/// </summary>
public interface IPhotoStorage
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Null when the object does not exist.</summary>
    Task<StoredObject?> GetPropertiesAsync(string key, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
}

public sealed record StoredObject(long Length, string? ContentType);

/// <summary>Server-side image inspection and thumbnail creation, behind an abstraction so it can move to a worker later.</summary>
public interface IImageProcessor
{
    /// <exception cref="ImageProcessingException">The bytes are not a decodable image within the pixel limit.</exception>
    ProcessedImage Process(ReadOnlyMemory<byte> image, int thumbnailMaxEdge, long maxPixels);
}

public sealed record ProcessedImage(int Width, int Height, byte[] ThumbnailJpeg);

public sealed class ImageProcessingException : Exception
{
    public ImageProcessingException()
    {
    }

    public ImageProcessingException(string message)
        : base(message)
    {
    }

    public ImageProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
