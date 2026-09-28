using FieldApp.Domain.Common;

namespace FieldApp.Domain.FieldItems;

/// <summary>
/// A photo of a field item. Stored as generated blob keys (never public URLs). Reserved first, then uploaded, then
/// finalized by the server after it verifies the object and creates a thumbnail.
/// </summary>
public sealed class FieldItemPhoto
{
    public const int BlobKeyMaxLength = 300;
    public const int Sha256Base64Length = 44;

    private FieldItemPhoto()
    {
    }

    public Guid Id { get; private set; }

    public Guid FieldItemId { get; private set; }

    public PhotoStatus Status { get; private set; }

    public string BlobKey { get; private set; } = string.Empty;

    public string? ThumbnailBlobKey { get; private set; }

    public string MediaType { get; private set; } = string.Empty;

    public long ByteLength { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    /// <summary>Base64 SHA-256 of the exact bytes the client promised to upload.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset ReservedAt { get; private set; }

    public DateTimeOffset ReservationExpiresAt { get; private set; }

    public DateTimeOffset? UploadedAt { get; private set; }

    public DateTimeOffset? FinalizedAt { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public bool IsPrimary { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status != PhotoStatus.Abandoned;

    internal static FieldItemPhoto Reserve(
        Guid id,
        FieldItem item,
        string mediaType,
        long byteLength,
        string sha256,
        DateTimeOffset? capturedAt,
        Guid userId,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        bool isPrimary,
        int sortOrder)
    {
        if (!ImageSignature.IsSupported(mediaType))
        {
            throw new DomainException($"Photos must be one of: {string.Join(", ", ImageSignature.SupportedMediaTypes)}.", nameof(MediaType));
        }

        if (byteLength <= 0)
        {
            throw new DomainException("The photo must not be empty.", nameof(ByteLength));
        }

        if (!IsSha256Base64(sha256))
        {
            throw new DomainException("sha256 must be the base64 SHA-256 of the photo.", nameof(Sha256));
        }

        return new FieldItemPhoto
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            FieldItemId = item.Id,
            Status = PhotoStatus.Reserved,
            BlobKey = $"projects/{item.ProjectId:N}/items/{item.Id:N}/photos/{id:N}/original",
            MediaType = mediaType,
            ByteLength = byteLength,
            Sha256 = sha256,
            SortOrder = sortOrder,
            CapturedAt = capturedAt?.ToUniversalTime(),
            ReservedAt = Guard.Utc(now),
            ReservationExpiresAt = Guard.Utc(expiresAt),
            UploadedByUserId = Guard.RequiredId(userId, nameof(UploadedByUserId)),
            IsPrimary = isPrimary,
        };
    }

    public bool IsExpired(DateTimeOffset now) => Status == PhotoStatus.Reserved && now >= ReservationExpiresAt;

    public bool Matches(string mediaType, long byteLength, string sha256) =>
        MediaType == mediaType && ByteLength == byteLength && string.Equals(Sha256, sha256, StringComparison.Ordinal);

    public string ThumbnailKeyFor() => BlobKey[..BlobKey.LastIndexOf('/')] + "/thumbnail.jpg";

    internal void ExtendReservation(DateTimeOffset expiresAt) => ReservationExpiresAt = Guard.Utc(expiresAt);

    internal void MarkUploaded(DateTimeOffset now)
    {
        if (Status != PhotoStatus.Reserved)
        {
            throw new DomainConflictException("This photo can no longer be uploaded.");
        }

        if (IsExpired(now))
        {
            throw new DomainConflictException("The upload reservation has expired; request a new upload.");
        }

        UploadedAt = Guard.Utc(now);
    }

    internal void MarkFinalized(int width, int height, string thumbnailBlobKey, DateTimeOffset now)
    {
        if (Status != PhotoStatus.Reserved)
        {
            throw new DomainConflictException("Only a reserved photo can be finalized.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new DomainException("Photo dimensions must be positive.");
        }

        Status = PhotoStatus.Finalized;
        Width = width;
        Height = height;
        ThumbnailBlobKey = Guard.RequiredText(thumbnailBlobKey, BlobKeyMaxLength, nameof(ThumbnailBlobKey));
        UploadedAt ??= Guard.Utc(now);
        FinalizedAt = Guard.Utc(now);
    }

    internal void Abandon()
    {
        if (Status == PhotoStatus.Finalized)
        {
            throw new DomainConflictException("A finalized photo cannot be abandoned.");
        }

        Status = PhotoStatus.Abandoned;
        IsPrimary = false;
    }

    private static bool IsSha256Base64(string? value)
    {
        if (value is not { Length: Sha256Base64Length })
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[33];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == 32;
    }
}
