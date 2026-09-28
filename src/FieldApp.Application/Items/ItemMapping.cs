using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

internal static class ItemMapping
{
    public static FieldItemDto ToDto(this FieldItem item, PhotoPolicy policy) => new(
        item.Id,
        item.ProjectId,
        item.ClientDraftId,
        item.Type,
        item.LifecycleState,
        item.AreaId,
        item.AreaPathSnapshot,
        item.LocationDetail,
        item.TradeId,
        item.TradeNameSnapshot,
        new ItemCompanyDto(item.ResponsibleCompanyId, item.ResponsibleCompanyNameSnapshot),
        item.Priority,
        item.CreatedAt,
        item.UpdatedAt,
        [.. item.Photos.Where(photo => photo.IsActive).OrderBy(photo => photo.SortOrder).Select(photo => photo.ToDto())],
        new ItemMediaDto($"/api/v1/items/{item.Id}/photos/uploads", policy.MaxPhotoBytes, [.. ImageSignature.SupportedMediaTypes.Order(StringComparer.Ordinal)]))
    {
        ETag = Convert.ToBase64String(item.RowVersion),
    };

    public static FieldItemPhotoDto ToDto(this FieldItemPhoto photo)
    {
        var finalized = photo.Status == PhotoStatus.Finalized;
        var contentBase = $"/api/v1/items/{photo.FieldItemId}/photos/{photo.Id}/content";

        return new FieldItemPhotoDto(
            photo.Id,
            photo.Status,
            photo.IsPrimary,
            photo.MediaType,
            photo.ByteLength,
            photo.Width,
            photo.Height,
            photo.CapturedAt,
            photo.UploadedAt,
            photo.FinalizedAt,
            finalized ? $"{contentBase}?variant=thumbnail" : null,
            finalized ? $"{contentBase}?variant=original" : null);
    }

    public static string CamelCase(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
