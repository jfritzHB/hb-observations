using FieldApp.Domain.Areas;
using FieldApp.Domain.Common;

namespace FieldApp.Domain.Capture;

/// <summary>
/// Where an item is: a structured <see cref="Area"/> (with its path snapshot), a free-text
/// <see cref="LocationDetail"/>, or both. Location Detail is item data only and never creates or changes Areas.
/// A draft may have no location yet; publishing requires <see cref="IsMeaningful"/>.
/// Defined ahead of FieldItem (docs/04-domain-model.md) so later slices reuse one rule.
/// </summary>
public sealed record ItemLocation
{
    public const int LocationDetailMaxLength = 120;

    private ItemLocation(Guid? areaId, string? areaPathSnapshot, string? locationDetail)
    {
        AreaId = areaId;
        AreaPathSnapshot = areaPathSnapshot;
        LocationDetail = locationDetail;
    }

    public static ItemLocation None { get; } = new(null, null, null);

    public Guid? AreaId { get; }

    public string? AreaPathSnapshot { get; }

    public string? LocationDetail { get; }

    /// <summary>At least one of a structured Area or a Location Detail is present.</summary>
    public bool IsMeaningful => AreaId is not null || LocationDetail is not null;

    /// <param name="area">Structured area, which must be active (any level of the hierarchy), or null.</param>
    /// <param name="locationDetail">Free text such as "Unit 214" or "North wall"; blank means none.</param>
    public static ItemLocation Create(Area? area, string? locationDetail)
    {
        if (area is { IsActive: false })
        {
            throw new DomainException("An inactive area cannot be used as an item location.", nameof(AreaId));
        }

        var detail = string.IsNullOrWhiteSpace(locationDetail)
            ? null
            : Guard.RequiredText(locationDetail, LocationDetailMaxLength, nameof(LocationDetail));

        return new ItemLocation(area?.Id, area?.Path, detail);
    }

    /// <summary>Rehydrates a location already validated when it was stored.</summary>
    public static ItemLocation Restore(Guid? areaId, string? areaPathSnapshot, string? locationDetail) =>
        new(areaId, areaPathSnapshot, locationDetail);

    /// <summary>Enforces the publish-time invariant.</summary>
    public ItemLocation EnsureMeaningful() =>
        IsMeaningful
            ? this
            : throw new DomainException("A location is required: choose an area or enter a location detail.", nameof(LocationDetail));
}
