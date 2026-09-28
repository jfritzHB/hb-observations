using FieldApp.Api.Authentication;
using FieldApp.Application.Items;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace FieldApp.Api.Endpoints;

/// <summary>Slice 2 item capture endpoints (docs/05-api-contract.md): idempotent server drafts and the photo lifecycle.</summary>
public static class ItemEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static void MapItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1")
            .RequireAuthorization(FieldAppPolicies.ApplicationUser)
            .WithTags("Items")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/projects/{projectId:guid}/items", async (
                Guid projectId,
                CreateDraftItemRequest request,
                [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
                FieldItemService service,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateDraftAsync(projectId, request, idempotencyKey, cancellationToken);
                return result.ToHttp(created =>
                {
                    http.Response.Headers.ETag = Quote(created.Item.ETag);
                    if (created.Replayed)
                    {
                        http.Response.Headers[ReplayedHeader] = "true";
                    }

                    return TypedResults.Created($"/api/v1/items/{created.Item.Id}", created.Item);
                });
            })
            .WithName("CreateDraftItem")
            .WithSummary("Creates (or, for a retried request, returns) a server Draft item. Requires an Idempotency-Key header.")
            .Produces<FieldItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/items/{itemId:guid}", async (Guid itemId, FieldItemService service, HttpContext http, CancellationToken cancellationToken) =>
                (await service.GetAsync(itemId, cancellationToken)).ToHttp(item =>
                {
                    http.Response.Headers.ETag = Quote(item.ETag);
                    return TypedResults.Ok(item);
                }))
            .WithName("GetItem")
            .Produces<FieldItemDto>();

        api.MapPost("/items/{itemId:guid}/photos/uploads", async (
                Guid itemId,
                ReservePhotoUploadRequest request,
                PhotoService photos,
                CancellationToken cancellationToken) =>
                (await photos.ReserveUploadAsync(itemId, request, cancellationToken)).ToHttp(reservation =>
                    reservation.Reused
                        ? TypedResults.Ok(reservation.Reservation)
                        : TypedResults.Created(reservation.Reservation.UploadUrl, reservation.Reservation)))
            .WithName("ReservePhotoUpload")
            .WithSummary("Grants a narrow, expiring upload for the item's primary photo. Repeating the same request returns the same reservation.")
            .Produces<PhotoUploadReservationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        api.MapPut("/items/{itemId:guid}/photos/{photoId:guid}/content", async (
                Guid itemId,
                Guid photoId,
                HttpContext http,
                PhotoService photos,
                PhotoPolicy policy,
                CancellationToken cancellationToken) =>
            {
                var bodyLimit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodyLimit is { IsReadOnly: false })
                {
                    bodyLimit.MaxRequestBodySize = policy.MaxPhotoBytes;
                }

                var result = await photos.UploadContentAsync(
                    itemId, photoId, http.Request.ContentType, http.Request.ContentLength, http.Request.Body, cancellationToken);
                return result.ToHttp(_ => TypedResults.NoContent());
            })
            .WithName("UploadPhotoContent")
            .WithSummary("Streams the reserved photo bytes into private storage (the upload URL returned by the reservation).")
            .Accepts<IFormFile>("image/jpeg", "image/png", "image/webp")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapPost("/items/{itemId:guid}/photos/{photoId:guid}/finalize", async (
                Guid itemId,
                Guid photoId,
                PhotoService photos,
                CancellationToken cancellationToken) =>
                (await photos.FinalizeAsync(itemId, photoId, cancellationToken)).ToHttp())
            .WithName("FinalizePhoto")
            .WithSummary("Verifies the uploaded object (existence, size, hash, signature), records metadata and a thumbnail. Idempotent.")
            .Produces<FieldItemPhotoDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapGet("/items/{itemId:guid}/photos/{photoId:guid}/content", async (
                Guid itemId,
                Guid photoId,
                [FromQuery] string? variant,
                PhotoService photos,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await photos.OpenContentAsync(itemId, photoId, !string.Equals(variant, "original", StringComparison.OrdinalIgnoreCase), cancellationToken);
                return result.ToHttp(content =>
                {
                    http.Response.Headers.CacheControl = "private, max-age=300";
                    return TypedResults.Stream(content.Content, content.ContentType);
                });
            })
            .WithName("GetPhotoContent")
            .WithSummary("Streams a finalized photo through the API (variant=thumbnail, the default, or original). No public blob URLs.");
    }

    private static string Quote(string etag) => $"\"{etag}\"";
}
