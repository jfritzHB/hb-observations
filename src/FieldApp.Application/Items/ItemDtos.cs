using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

/// <summary>
/// Creates a server draft. There is deliberately no Responsible Company field: the server resolves it from the
/// Project Trade. Unknown JSON properties are ignored.
/// </summary>
public sealed record CreateDraftItemRequest(
    Guid ClientDraftId,
    FieldItemType? Type,
    Guid? AreaId,
    string? LocationDetail,
    Guid TradeId,
    ItemPriority? Priority);

public sealed record FieldItemDto(
    Guid Id,
    Guid ProjectId,
    Guid ClientDraftId,
    FieldItemType Type,
    LifecycleState LifecycleState,
    Guid? AreaId,
    string? AreaPath,
    string? LocationDetail,
    Guid TradeId,
    string TradeName,
    ItemCompanyDto ResponsibleCompany,
    ItemPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FieldItemPhotoDto> Photos,
    ItemMediaDto Media)
{
    /// <summary>Base64 rowversion, exposed as the ETag header.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ETag { get; init; } = string.Empty;
}

public sealed record ItemCompanyDto(Guid Id, string Name);

/// <summary>Item-scoped media instructions returned with the draft.</summary>
public sealed record ItemMediaDto(string PhotoUploadsUrl, long MaxPhotoBytes, IReadOnlyCollection<string> AllowedContentTypes);

public sealed record FieldItemPhotoDto(
    Guid Id,
    PhotoStatus Status,
    bool IsPrimary,
    string MediaType,
    long ByteLength,
    int? Width,
    int? Height,
    DateTimeOffset? CapturedAt,
    DateTimeOffset? UploadedAt,
    DateTimeOffset? FinalizedAt,
    string? ThumbnailUrl,
    string? ContentUrl);

public sealed record ReservePhotoUploadRequest(string? FileName, string? ContentType, long ByteLength, string? Sha256, DateTimeOffset? CapturedAt);

/// <summary>A narrow, expiring grant to upload exactly one photo.</summary>
public sealed record PhotoUploadReservationDto(
    Guid PhotoId,
    string UploadUrl,
    string Method,
    DateTimeOffset ExpiresAt,
    string AllowedContentType,
    long MaxBytes);

public sealed record CreatedItem(FieldItemDto Item, bool Replayed);

public sealed record PhotoContent(Stream Content, string ContentType);
