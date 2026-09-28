using System.Security.Cryptography;
using System.Text.Json;
using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Common;
using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

/// <summary>
/// Media lifecycle (docs/06-ai-media.md): reserve a narrow, expiring upload; receive the bytes into private storage;
/// verify the object (existence, size, hash, file signature, decodability), record metadata, create a thumbnail and
/// finalize. Every step is safe to retry.
/// </summary>
public sealed class PhotoService(
    IFieldItemStore items,
    IPhotoStorage storage,
    IImageProcessor imageProcessor,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    ItemAccess itemAccess,
    ICurrentUser currentUser,
    ICorrelationContext correlation,
    TimeProvider clock,
    PhotoPolicy policy)
{
    public async Task<Result<PhotoReservationResult>> ReserveUploadAsync(
        Guid itemId,
        ReservePhotoUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await itemAccess.LoadAsync(itemId, ProjectOperation.CaptureItems, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!ImageSignature.IsSupported(contentType))
        {
            return AppError.UnsupportedMediaType($"Photos must be one of: {string.Join(", ", ImageSignature.SupportedMediaTypes)}.");
        }

        if (request.ByteLength <= 0)
        {
            return AppError.Validation("byteLength", "byteLength must be positive.");
        }

        if (request.ByteLength > policy.MaxPhotoBytes)
        {
            return AppError.PayloadTooLarge($"Photos may be at most {policy.MaxPhotoBytes} bytes.");
        }

        var item = access.Item!;
        PhotoReservation reservation;
        try
        {
            reservation = item.ReservePrimaryPhoto(
                Guid.NewGuid(), contentType!, request.ByteLength, request.Sha256 ?? string.Empty, request.CapturedAt,
                currentUser.RequireUserId(), clock.GetUtcNow(), policy.ReservationLifetime);
        }
        catch (DomainConflictException ex)
        {
            return AppError.Conflict(ex.Message);
        }
        catch (DomainException ex)
        {
            return AppError.Validation(ItemMapping.CamelCase(ex.Field ?? "request"), ex.Message);
        }

        Audit(item, reservation.Photo, reservation.Reused ? "PhotoUploadReservationReused" : "PhotoUploadReserved",
            new { PhotoId = reservation.Photo.Id, reservation.Photo.MediaType, reservation.Photo.ByteLength });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return AppError.Conflict("The item changed while reserving the upload; retry.");
        }

        var photo = reservation.Photo;
        return new PhotoReservationResult(
            new PhotoUploadReservationDto(
                photo.Id,
                $"/api/v1/items/{item.Id}/photos/{photo.Id}/content",
                "PUT",
                photo.ReservationExpiresAt,
                photo.MediaType,
                photo.ByteLength),
            reservation.Reused);
    }

    /// <summary>Streams the reserved photo into private storage. Re-uploading while reserved simply overwrites.</summary>
    public async Task<Result<bool>> UploadContentAsync(
        Guid itemId,
        Guid photoId,
        string? contentType,
        long? contentLength,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var access = await itemAccess.LoadAsync(itemId, ProjectOperation.CaptureItems, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var item = access.Item!;
        var photo = item.Photos.FirstOrDefault(candidate => candidate.Id == photoId);
        if (photo is null)
        {
            return AppError.NotFound("Photo not found.");
        }

        if (photo.Status != PhotoStatus.Reserved)
        {
            return AppError.Conflict("This photo can no longer be uploaded.");
        }

        if (photo.IsExpired(clock.GetUtcNow()))
        {
            return AppError.Conflict("The upload reservation has expired; request a new upload.");
        }

        if (!string.Equals(NormalizeContentType(contentType), photo.MediaType, StringComparison.Ordinal))
        {
            return AppError.UnsupportedMediaType($"The upload must be sent as {photo.MediaType}.");
        }

        if (contentLength > photo.ByteLength)
        {
            return AppError.PayloadTooLarge("The upload is larger than reserved.");
        }

        var limited = new LimitedReadStream(content, photo.ByteLength);
        try
        {
            // Incoming bytes never overwrite the verified original, including uploads already in flight at finalize.
            await storage.UploadAsync(photo.BlobKey + "/upload", limited, photo.MediaType, cancellationToken);
        }
        catch (Exception) when (limited.Exceeded)
        {
            return AppError.PayloadTooLarge("The upload is larger than reserved.");
        }
        catch (PhotoStorageException)
        {
            return AppError.Unavailable("Photo storage is unavailable; retry the upload.");
        }

        if (photo.IsExpired(clock.GetUtcNow()))
        {
            return AppError.Conflict("The upload reservation expired; request a new upload.");
        }

        item.RecordPhotoUploaded(photo.Id, clock.GetUtcNow());
        Audit(item, photo, "PhotoContentUploaded", new { PhotoId = photo.Id });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return AppError.Conflict("The photo changed during upload; retry to recover its current state.");
        }

        return true;
    }

    /// <summary>Verifies the uploaded object and finalizes the photo. Finalizing an already finalized photo returns it.</summary>
    public async Task<Result<FieldItemPhotoDto>> FinalizeAsync(Guid itemId, Guid photoId, CancellationToken cancellationToken)
    {
        var access = await itemAccess.LoadAsync(itemId, ProjectOperation.CaptureItems, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var item = access.Item!;
        var photo = item.Photos.FirstOrDefault(candidate => candidate.Id == photoId);
        if (photo is null)
        {
            return AppError.NotFound("Photo not found.");
        }

        if (photo.Status == PhotoStatus.Finalized)
        {
            return photo.ToDto();
        }

        if (photo.Status == PhotoStatus.Abandoned)
        {
            return AppError.Conflict("This photo was replaced by a different photo.");
        }

        byte[] bytes;
        try
        {
            var incomingKey = photo.BlobKey + "/upload";
            var stored = await storage.GetPropertiesAsync(incomingKey, cancellationToken);
            // Resume reservations uploaded by the earlier Slice 2 implementation without losing those bytes.
            if (stored is null)
            {
                incomingKey = photo.BlobKey;
                stored = await storage.GetPropertiesAsync(incomingKey, cancellationToken);
            }
            if (stored is null)
            {
                return AppError.Conflict("The photo has not been uploaded yet.");
            }

            if (stored.Length != photo.ByteLength)
            {
                return AppError.Unprocessable("The uploaded photo is not the size that was reserved; upload it again.");
            }

            await using var source = await storage.OpenReadAsync(incomingKey, cancellationToken);
            using var buffer = new MemoryStream((int)photo.ByteLength);
            var limited = new LimitedReadStream(source, photo.ByteLength);
            try
            {
                await limited.CopyToAsync(buffer, cancellationToken);
            }
            catch (Exception) when (limited.Exceeded)
            {
                return AppError.Unprocessable("The uploaded photo changed size; upload it again.");
            }
            bytes = buffer.ToArray();
        }
        catch (PhotoStorageException)
        {
            return AppError.Unavailable("Photo storage is unavailable; retry finalizing.");
        }

        if (!string.Equals(Convert.ToBase64String(SHA256.HashData(bytes)), photo.Sha256, StringComparison.Ordinal))
        {
            return AppError.Unprocessable("The uploaded photo does not match its checksum; upload it again.");
        }

        var detected = ImageSignature.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, ImageSignature.HeaderLength)));
        if (detected is null || detected != photo.MediaType)
        {
            return AppError.UnsupportedMediaType("The uploaded file is not a supported image of the declared type.");
        }

        ProcessedImage processed;
        try
        {
            processed = imageProcessor.Process(bytes, policy.ThumbnailMaxEdge, policy.MaxPhotoPixels);
        }
        catch (ImageProcessingException ex)
        {
            return AppError.Unprocessable(ex.Message);
        }

        var thumbnailKey = photo.ThumbnailKeyFor();
        try
        {
            using var verified = new MemoryStream(bytes, writable: false);
            await storage.UploadAsync(photo.BlobKey, verified, photo.MediaType, cancellationToken);
            using var thumbnail = new MemoryStream(processed.ThumbnailJpeg, writable: false);
            await storage.UploadAsync(thumbnailKey, thumbnail, ImageSignature.Jpeg, cancellationToken);
        }
        catch (PhotoStorageException)
        {
            return AppError.Unavailable("The thumbnail could not be stored; retry finalizing.");
        }

        item.FinalizePhoto(photo.Id, processed.Width, processed.Height, thumbnailKey, clock.GetUtcNow());
        Audit(item, photo, "PhotoFinalized", new { PhotoId = photo.Id, photo.MediaType, photo.ByteLength, processed.Width, processed.Height });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent finalize won; report its result.
            var current = await items.FindAsync(itemId, cancellationToken);
            var winner = current?.Photos.FirstOrDefault(candidate => candidate.Id == photoId);
            return winner is { Status: PhotoStatus.Finalized }
                ? winner.ToDto()
                : AppError.Conflict("The photo changed while finalizing; retry.");
        }

        return photo.ToDto();
    }

    public async Task<Result<PhotoContent>> OpenContentAsync(Guid itemId, Guid photoId, bool thumbnail, CancellationToken cancellationToken)
    {
        var access = await itemAccess.LoadAsync(itemId, ProjectOperation.ViewProject, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var photo = access.Item!.Photos.FirstOrDefault(candidate => candidate.Id == photoId && candidate.Status == PhotoStatus.Finalized);
        if (photo is null)
        {
            return AppError.NotFound("Photo not found.");
        }

        try
        {
            var stream = await storage.OpenReadAsync(thumbnail ? photo.ThumbnailBlobKey! : photo.BlobKey, cancellationToken);
            return new PhotoContent(stream, thumbnail ? ImageSignature.Jpeg : photo.MediaType);
        }
        catch (PhotoStorageException)
        {
            return AppError.Unavailable("Photo storage is unavailable; retry.");
        }
    }

    private static string? NormalizeContentType(string? contentType) =>
        contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();

    private void Audit(FieldItem item, FieldItemPhoto photo, string action, object details) =>
        auditLog.Add(AuditEvent.Record(
            Guid.NewGuid(), clock.GetUtcNow(), currentUser.RequireUserId(), item.ProjectId, nameof(FieldItemPhoto), photo.Id,
            action, correlation.CorrelationId, JsonSerializer.Serialize(details)));
}

public sealed record PhotoReservationResult(PhotoUploadReservationDto Reservation, bool Reused);
