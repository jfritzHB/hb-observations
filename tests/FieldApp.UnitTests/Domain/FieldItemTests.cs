using System.Security.Cryptography;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Common;
using FieldApp.Domain.Companies;
using FieldApp.Domain.FieldItems;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;

namespace FieldApp.UnitTests.Domain;

public sealed class FieldItemTests
{
    private static readonly DateTimeOffset _now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _projectId = Guid.NewGuid();
    private static readonly Guid _userId = Guid.NewGuid();

    private readonly Trade _drywall = Trade.Create(Guid.NewGuid(), "DRY", "Drywall");
    private readonly Company _company = Company.Create(Guid.NewGuid(), "Synthetic Drywall Co.");

    [Fact]
    public void Draft_snapshots_trade_and_responsible_company_from_the_project_trade()
    {
        var item = Draft(area: null, detail: "Unit 214");

        Assert.Equal(LifecycleState.Draft, item.LifecycleState);
        Assert.Equal(_drywall.Id, item.TradeId);
        Assert.Equal("Drywall", item.TradeNameSnapshot);
        Assert.Equal(_company.Id, item.ResponsibleCompanyId);
        Assert.Equal("Synthetic Drywall Co.", item.ResponsibleCompanyNameSnapshot);
        Assert.Equal(ItemPriority.Normal, item.Priority);
        Assert.Equal(_now, item.CreatedAt);
    }

    [Fact]
    public void Draft_accepts_area_detail_both_or_neither()
    {
        var area = Area.Create(Guid.NewGuid(), _projectId, null, "Building A", 10);

        var both = Draft(area, "North wall");
        Assert.Equal(area.Id, both.AreaId);
        Assert.Equal("Building A", both.AreaPathSnapshot);
        Assert.Equal("North wall", both.LocationDetail);

        var neither = Draft(null, "  ");
        Assert.False(neither.Location.IsMeaningful); // Allowed on a draft; publish (Slice 4) will require it.
    }

    [Fact]
    public void Area_from_another_project_is_rejected()
    {
        var foreign = Area.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Elsewhere", 10);

        var ex = Assert.Throws<DomainException>(() => Draft(foreign, null));
        Assert.Equal("AreaId", ex.Field);
    }

    [Fact]
    public void Location_detail_longer_than_120_characters_is_rejected()
    {
        Assert.Throws<DomainException>(() => Draft(null, new string('x', 121)));
    }

    [Fact]
    public void Unmapped_disabled_or_foreign_project_trades_are_rejected()
    {
        var unmapped = ProjectTrade.Create(Guid.NewGuid(), _projectId, _drywall.Id, null);
        Assert.Throws<DomainException>(() => Draft(null, "x", unmapped, null));

        var disabled = ProjectTrade.Create(Guid.NewGuid(), _projectId, _drywall.Id, _company.Id);
        disabled.Disable();
        Assert.Throws<DomainException>(() => Draft(null, "x", disabled, _company));

        var foreign = ProjectTrade.Create(Guid.NewGuid(), Guid.NewGuid(), _drywall.Id, _company.Id);
        var ex = Assert.Throws<DomainException>(() => Draft(null, "x", foreign, _company));
        Assert.Equal("TradeId", ex.Field);
    }

    [Fact]
    public void Undefined_type_is_rejected()
    {
        var mapping = ProjectTrade.Create(Guid.NewGuid(), _projectId, _drywall.Id, _company.Id);

        Assert.Throws<DomainException>(() => FieldItem.CreateDraft(
            Guid.NewGuid(), _projectId, Guid.NewGuid(), (FieldItemType)9, null, "x", mapping, _drywall, _company,
            ItemPriority.Normal, _userId, _now));
    }

    [Fact]
    public void Reserving_the_same_photo_again_reuses_the_reservation()
    {
        var item = Draft(null, "Unit 214");
        var sha = Sha("photo-1");

        var first = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, sha, null, _userId, _now, TimeSpan.FromMinutes(15));
        var again = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, sha, null, _userId, _now.AddMinutes(1), TimeSpan.FromMinutes(15));

        Assert.False(first.Reused);
        Assert.True(again.Reused);
        Assert.Same(first.Photo, again.Photo);
        Assert.Single(item.Photos);
        Assert.True(first.Photo.IsPrimary);
        Assert.Contains(item.Id.ToString("N"), first.Photo.BlobKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Expired_reservation_for_the_same_photo_is_extended()
    {
        var item = Draft(null, "Unit 214");
        var sha = Sha("photo-1");
        var reservation = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, sha, null, _userId, _now, TimeSpan.FromMinutes(15));

        var later = _now.AddHours(1);
        Assert.True(reservation.Photo.IsExpired(later));

        item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, sha, null, _userId, later, TimeSpan.FromMinutes(15));

        Assert.False(reservation.Photo.IsExpired(later));
    }

    [Fact]
    public void A_different_photo_replaces_an_unfinalized_reservation()
    {
        var item = Draft(null, "Unit 214");
        var first = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, Sha("a"), null, _userId, _now, TimeSpan.FromMinutes(15)).Photo;

        var second = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 2000, Sha("b"), null, _userId, _now, TimeSpan.FromMinutes(15)).Photo;

        Assert.Equal(PhotoStatus.Abandoned, first.Status);
        Assert.False(first.IsPrimary);
        Assert.Same(second, item.PrimaryPhoto);
    }

    [Fact]
    public void Finalizing_records_dimensions_and_thumbnail_and_blocks_another_primary()
    {
        var item = Draft(null, "Unit 214");
        var photo = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, Sha("a"), _now, _userId, _now, TimeSpan.FromMinutes(15)).Photo;
        item.RecordPhotoUploaded(photo.Id, _now.AddSeconds(5));

        item.FinalizePhoto(photo.Id, 2560, 1920, photo.ThumbnailKeyFor(), _now.AddSeconds(6));

        Assert.Equal(PhotoStatus.Finalized, photo.Status);
        Assert.Equal((2560, 1920), (photo.Width, photo.Height));
        Assert.EndsWith("/thumbnail.jpg", photo.ThumbnailBlobKey, StringComparison.Ordinal);
        Assert.Throws<DomainConflictException>(() =>
            item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, Sha("b"), null, _userId, _now, TimeSpan.FromMinutes(15)));
        Assert.Throws<DomainConflictException>(() => item.FinalizePhoto(photo.Id, 1, 1, "x", _now));
    }

    [Fact]
    public void Uploading_after_the_reservation_expires_is_rejected()
    {
        var item = Draft(null, "Unit 214");
        var photo = item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 1000, Sha("a"), null, _userId, _now, TimeSpan.FromMinutes(15)).Photo;

        Assert.Throws<DomainConflictException>(() => item.RecordPhotoUploaded(photo.Id, _now.AddMinutes(16)));
    }

    [Theory]
    [InlineData("image/gif", 1000, true)]
    [InlineData("image/jpeg", 0, true)]
    [InlineData("image/jpeg", 1000, false)]
    public void Invalid_reservations_are_rejected(string mediaType, long byteLength, bool validSha)
    {
        var item = Draft(null, "Unit 214");

        Assert.Throws<DomainException>(() => item.ReservePrimaryPhoto(
            Guid.NewGuid(), mediaType, byteLength, validSha ? Sha("a") : "not-a-hash", null, _userId, _now, TimeSpan.FromMinutes(15)));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, null)]
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C }, null)]
    public void File_signatures_identify_supported_images_only(byte[] header, string? expected)
    {
        Assert.Equal(expected, ImageSignature.Detect(header));
    }

    [Theory]
    [InlineData("3f5a1c9e-2b77-4d61-9d4a-0a1b2c3d4e5f", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    public void Idempotency_keys_are_short_visible_ascii(string key, bool valid)
    {
        Assert.Equal(valid, IdempotencyRecord.IsValidKey(key));
        Assert.False(IdempotencyRecord.IsValidKey(new string('k', 101)));
    }

    private FieldItem Draft(Area? area, string? detail, ProjectTrade? mapping = null, Company? company = null) =>
        FieldItem.CreateDraft(
            Guid.NewGuid(), _projectId, Guid.NewGuid(), FieldItemType.PunchList, area, detail,
            mapping ?? ProjectTrade.Create(Guid.NewGuid(), _projectId, _drywall.Id, _company.Id),
            _drywall, mapping is null ? _company : company, ItemPriority.Normal, _userId, _now);

    private static string Sha(string seed) => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed)));
}
