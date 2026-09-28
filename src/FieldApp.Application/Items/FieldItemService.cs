using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Common;
using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

/// <summary>Server draft creation (idempotent) and retrieval.</summary>
public sealed class FieldItemService(
    IFieldItemStore items,
    IReferenceDataReader referenceData,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    ProjectAuthorizer projectAuthorizer,
    ItemAccess itemAccess,
    ICurrentUser currentUser,
    ICorrelationContext correlation,
    TimeProvider clock,
    PhotoPolicy policy)
{
    public const string CreateScope = "CreateFieldItem";

    /// <summary>
    /// Creates a Draft item. The same Idempotency-Key (or the same clientDraftId) with the same request returns the
    /// original item without creating another; reusing either for a different request is a conflict.
    /// </summary>
    public async Task<Result<CreatedItem>> CreateDraftAsync(
        Guid projectId,
        CreateDraftItemRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IdempotencyRecord.IsValidKey(idempotencyKey))
        {
            return AppError.Validation("Idempotency-Key", $"An Idempotency-Key header of 1-{IdempotencyRecord.KeyMaxLength} visible characters is required.");
        }

        if (request.ClientDraftId == Guid.Empty)
        {
            return AppError.Validation("clientDraftId", "clientDraftId is required.");
        }

        if (request.Type is null)
        {
            return AppError.Validation("type", "type is required (Observation or PunchList).");
        }

        var authorization = await projectAuthorizer.AuthorizeAsync(projectId, ProjectOperation.CaptureItems, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var userId = currentUser.RequireUserId();
        var fingerprint = Fingerprint(projectId, request);

        var replay = await TryReplayAsync(projectId, userId, idempotencyKey!, fingerprint, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var mapping = (await referenceData.ListProjectTradesAsync(projectId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Trade.Id == request.TradeId);
        if (mapping is null || !mapping.IsAvailableForCapture)
        {
            return AppError.Validation("tradeId", "The trade is not available for capture on this project.");
        }

        Area? area = null;
        if (request.AreaId is { } areaId)
        {
            var tree = new AreaTree(projectId, await referenceData.ListAreasAsync(projectId, cancellationToken));
            area = tree.Find(areaId);
            if (area is null || !tree.IsSelectable(areaId))
            {
                return AppError.Validation("areaId", "The area is not an active area of this project.");
            }
        }

        FieldItem item;
        try
        {
            item = FieldItem.CreateDraft(
                Guid.NewGuid(), projectId, request.ClientDraftId, request.Type.Value, area, request.LocationDetail,
                mapping.ProjectTrade, mapping.Trade, mapping.ResponsibleCompany, request.Priority ?? ItemPriority.Normal,
                userId, clock.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return AppError.Validation(ItemMapping.CamelCase(ex.Field ?? "request"), ex.Message);
        }

        items.Add(item);
        items.Add(IdempotencyRecord.Create(Guid.NewGuid(), userId, CreateScope, idempotencyKey!, fingerprint, item.Id, clock.GetUtcNow()));
        auditLog.Add(AuditEvent.Record(
            Guid.NewGuid(), clock.GetUtcNow(), userId, projectId, nameof(FieldItem), item.Id, "ItemDraftCreated",
            correlation.CorrelationId,
            JsonSerializer.Serialize(new
            {
                item.ClientDraftId,
                Type = item.Type.ToString(),
                item.AreaId,
                item.LocationDetail,
                item.TradeId,
                item.ResponsibleCompanyId,
            })));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // A concurrent request with the same key or clientDraftId won the race: return its result.
            return await TryReplayAsync(projectId, userId, idempotencyKey!, fingerprint, request, cancellationToken)
                ?? AppError.Conflict("This draft is being created by another request; retry shortly.");
        }

        return new CreatedItem(item.ToDto(policy), Replayed: false);
    }

    public async Task<Result<FieldItemDto>> GetAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var access = await itemAccess.LoadAsync(itemId, ProjectOperation.ViewProject, cancellationToken);
        return access.Error is not null ? access.Error : access.Item!.ToDto(policy);
    }

    private async Task<Result<CreatedItem>?> TryReplayAsync(
        Guid projectId,
        Guid userId,
        string key,
        string fingerprint,
        CreateDraftItemRequest request,
        CancellationToken cancellationToken)
    {
        var record = await items.FindIdempotencyRecordAsync(userId, CreateScope, key, cancellationToken);
        if (record is not null)
        {
            if (!string.Equals(record.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return AppError.Conflict("This Idempotency-Key was already used for a different request.");
            }

            var original = await items.FindAsync(record.ResourceId, cancellationToken);
            return original is null ? null : Result.Success(new CreatedItem(original.ToDto(policy), Replayed: true));
        }

        var sameDraft = await items.FindByClientDraftAsync(projectId, userId, request.ClientDraftId, cancellationToken);
        if (sameDraft is null)
        {
            return null;
        }

        return SameRequest(sameDraft, request)
            ? new CreatedItem(sameDraft.ToDto(policy), Replayed: true)
            : AppError.Conflict("This clientDraftId was already used for a different item.");
    }

    private static bool SameRequest(FieldItem item, CreateDraftItemRequest request) =>
        item.Type == request.Type
        && item.AreaId == request.AreaId
        && string.Equals(item.LocationDetail, NormalizeDetail(request.LocationDetail), StringComparison.Ordinal)
        && item.TradeId == request.TradeId
        && item.Priority == (request.Priority ?? ItemPriority.Normal);

    private static string? NormalizeDetail(string? detail) => string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

    private static string Fingerprint(Guid projectId, CreateDraftItemRequest request)
    {
        var canonical = string.Join(
            '|',
            projectId.ToString("N", CultureInfo.InvariantCulture),
            request.ClientDraftId.ToString("N", CultureInfo.InvariantCulture),
            request.Type?.ToString() ?? string.Empty,
            request.AreaId?.ToString("N", CultureInfo.InvariantCulture) ?? string.Empty,
            NormalizeDetail(request.LocationDetail) ?? string.Empty,
            request.TradeId.ToString("N", CultureInfo.InvariantCulture),
            (request.Priority ?? ItemPriority.Normal).ToString());

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
