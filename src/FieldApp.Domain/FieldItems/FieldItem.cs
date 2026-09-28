using FieldApp.Domain.Areas;
using FieldApp.Domain.Capture;
using FieldApp.Domain.Common;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;

namespace FieldApp.Domain.FieldItems;

/// <summary>
/// An Observation or Punch List item. Slice 2 implements the server Draft: capture context, server-resolved
/// Responsible Company, snapshots and the primary photo. Title, descriptions, numbering and publish come later.
/// </summary>
public sealed class FieldItem
{
    public const int TradeNameSnapshotMaxLength = Trade.NameMaxLength;
    public const int CompanyNameSnapshotMaxLength = Company.NameMaxLength;

    private readonly List<FieldItemPhoto> _photos = [];

    private FieldItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The client's local draft ID; unique per project and creator, so a retried creation finds this item.</summary>
    public Guid ClientDraftId { get; private set; }

    public FieldItemType Type { get; private set; }

    public LifecycleState LifecycleState { get; private set; }

    public Guid? AreaId { get; private set; }

    public string? AreaPathSnapshot { get; private set; }

    public string? LocationDetail { get; private set; }

    public Guid TradeId { get; private set; }

    public string TradeNameSnapshot { get; private set; } = string.Empty;

    /// <summary>Always resolved from the Project Trade mapping; never supplied by a client.</summary>
    public Guid ResponsibleCompanyId { get; private set; }

    public string ResponsibleCompanyNameSnapshot { get; private set; } = string.Empty;

    public ItemPriority Priority { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<FieldItemPhoto> Photos => _photos;

    public ItemLocation Location => ItemLocation.Restore(AreaId, AreaPathSnapshot, LocationDetail);

    public FieldItemPhoto? PrimaryPhoto => _photos.SingleOrDefault(photo => photo.IsPrimary && photo.IsActive);

    /// <summary>
    /// Creates a server draft. The trade must be available for capture on the project (enabled, active and mapped to
    /// an active company); the Responsible Company and display snapshots come from that mapping and the Area.
    /// A draft may lack a location; publishing will require one.
    /// </summary>
    public static FieldItem CreateDraft(
        Guid id,
        Guid projectId,
        Guid clientDraftId,
        FieldItemType type,
        Area? area,
        string? locationDetail,
        ProjectTrade projectTrade,
        Trade trade,
        Company? responsibleCompany,
        ItemPriority priority,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(projectTrade);
        ArgumentNullException.ThrowIfNull(trade);

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Type must be Observation or PunchList.", nameof(Type));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new DomainException("Priority must be Normal, High or Critical.", nameof(Priority));
        }

        if (projectTrade.ProjectId != projectId || !projectTrade.IsAvailableForCapture(trade, responsibleCompany))
        {
            throw new DomainException("The trade is not available for capture on this project.", nameof(TradeId));
        }

        if (area is not null && area.ProjectId != projectId)
        {
            throw new DomainException("The area does not belong to this project.", nameof(AreaId));
        }

        var location = ItemLocation.Create(area, locationDetail);
        var utcNow = Guard.Utc(now);

        return new FieldItem
        {
            Id = Guard.RequiredId(id, nameof(Id)),
            ProjectId = Guard.RequiredId(projectId, nameof(ProjectId)),
            ClientDraftId = Guard.RequiredId(clientDraftId, nameof(ClientDraftId)),
            Type = type,
            LifecycleState = LifecycleState.Draft,
            AreaId = location.AreaId,
            AreaPathSnapshot = location.AreaPathSnapshot,
            LocationDetail = location.LocationDetail,
            TradeId = trade.Id,
            TradeNameSnapshot = trade.Name,
            ResponsibleCompanyId = responsibleCompany!.Id,
            ResponsibleCompanyNameSnapshot = responsibleCompany.Name,
            Priority = priority,
            CreatedByUserId = Guard.RequiredId(createdByUserId, nameof(CreatedByUserId)),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    /// <summary>
    /// Reserves the upload of the primary photo. Retrying with the same photo returns the existing reservation
    /// (extending it if it expired); a different photo replaces an unfinalized reservation. Once a primary photo is
    /// finalized, the capture photo cannot be replaced here.
    /// </summary>
    public PhotoReservation ReservePrimaryPhoto(
        Guid photoId,
        string mediaType,
        long byteLength,
        string sha256,
        DateTimeOffset? capturedAt,
        Guid userId,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (LifecycleState != LifecycleState.Draft)
        {
            throw new DomainConflictException("Photos are added to drafts only in this release.");
        }

        var current = PrimaryPhoto;
        if (current is { Status: PhotoStatus.Finalized })
        {
            throw new DomainConflictException("This item already has its primary photo.");
        }

        if (current is not null && current.Matches(mediaType, byteLength, sha256))
        {
            if (current.IsExpired(now))
            {
                current.ExtendReservation(now + lifetime);
            }

            Touch(now);
            return new PhotoReservation(current, Reused: true);
        }

        current?.Abandon();

        var photo = FieldItemPhoto.Reserve(
            photoId, this, mediaType, byteLength, sha256, capturedAt, userId, now, now + lifetime, isPrimary: true,
            sortOrder: _photos.Count);
        _photos.Add(photo);
        Touch(now);
        return new PhotoReservation(photo, Reused: false);
    }

    public FieldItemPhoto GetPhoto(Guid photoId) =>
        _photos.SingleOrDefault(photo => photo.Id == photoId)
        ?? throw new DomainException("The photo does not belong to this item.", "PhotoId");

    public void RecordPhotoUploaded(Guid photoId, DateTimeOffset now)
    {
        GetPhoto(photoId).MarkUploaded(now);
        Touch(now);
    }

    public void FinalizePhoto(Guid photoId, int width, int height, string thumbnailBlobKey, DateTimeOffset now)
    {
        GetPhoto(photoId).MarkFinalized(width, height, thumbnailBlobKey, now);
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = Guard.Utc(now);
}

public sealed record PhotoReservation(FieldItemPhoto Photo, bool Reused);
