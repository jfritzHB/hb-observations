using System.Security.Cryptography;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Application.Items;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Companies;
using FieldApp.Domain.FieldItems;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;

namespace FieldApp.UnitTests.Application;

/// <summary>Draft creation and the photo pipeline against in-memory ports, including failure boundaries.</summary>
public sealed class PhotoPipelineTests
{
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private readonly InMemoryReferenceData _data = new();
    private readonly FakePhotoStorage _storage = new();
    private readonly FakeImageProcessor _images = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId;
    private readonly Trade _trade = Trade.Create(Guid.NewGuid(), "DRY", "Drywall");

    public PhotoPipelineTests()
    {
        _projectId = _data.AddProjectWithMember(_userId, ProjectRoles.Superintendent);
        var company = Company.Create(Guid.NewGuid(), "Synthetic Drywall Co.");
        _data.ProjectTrades.Add(new ProjectTradeMapping(ProjectTrade.Create(Guid.NewGuid(), _projectId, _trade.Id, company.Id), _trade, company));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Draft_creation_is_idempotent_and_rejects_key_reuse_for_a_different_request()
    {
        var request = Request();

        var first = await Items().CreateDraftAsync(_projectId, request, "key-1", CancellationToken);
        var again = await Items().CreateDraftAsync(_projectId, request, "key-1", CancellationToken);
        var sameDraftNewKey = await Items().CreateDraftAsync(_projectId, request, "key-2", CancellationToken);
        var misuse = await Items().CreateDraftAsync(_projectId, request with { LocationDetail = "Different" }, "key-1", CancellationToken);

        Assert.False(first.Value!.Replayed);
        Assert.True(again.Value!.Replayed);
        Assert.Equal(first.Value.Item.Id, again.Value.Item.Id);
        Assert.Equal(first.Value.Item.Id, sameDraftNewKey.Value!.Item.Id);
        Assert.Equal(ErrorKind.Conflict, misuse.Error!.Kind);
        Assert.Single(_data.Items);
        Assert.Single(_data.AuditEvents, audit => audit.Action == "ItemDraftCreated");
    }

    [Fact]
    public async Task Draft_creation_requires_an_idempotency_key()
    {
        var result = await Items().CreateDraftAsync(_projectId, Request(), null, CancellationToken);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.True(result.Error.ValidationErrors!.ContainsKey("Idempotency-Key"));
    }

    [Fact]
    public async Task Trade_partner_cannot_create_items_and_non_members_see_nothing()
    {
        var partner = Guid.NewGuid();
        _data.Memberships.Add(new MembershipGrant(_projectId, partner, ProjectRoles.TradePartner, Guid.NewGuid()));

        var forbidden = await Items(partner).CreateDraftAsync(_projectId, Request(), "k", CancellationToken);
        var concealed = await Items(Guid.NewGuid()).CreateDraftAsync(_projectId, Request(), "k", CancellationToken);

        Assert.Equal(ErrorKind.Forbidden, forbidden.Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, concealed.Error!.Kind);
    }

    [Fact]
    public async Task Photo_pipeline_reserves_uploads_verifies_and_finalizes_once()
    {
        var itemId = await CreateItemAsync();
        var reservation = (await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg), CancellationToken)).Value!.Reservation;

        Assert.True((await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/jpeg", _jpeg.Length, new MemoryStream(_jpeg), CancellationToken)).IsSuccess);
        var finalized = await Photos().FinalizeAsync(itemId, reservation.PhotoId, CancellationToken);
        var again = await Photos().FinalizeAsync(itemId, reservation.PhotoId, CancellationToken);

        Assert.Equal(PhotoStatus.Finalized, finalized.Value!.Status);
        Assert.Equal((1600, 1200), (finalized.Value.Width, finalized.Value.Height));
        Assert.Equal(PhotoStatus.Finalized, again.Value!.Status);
        Assert.Single(_data.AuditEvents, audit => audit.Action == "PhotoFinalized");
        Assert.Contains(_storage.Objects.Keys, key => key.EndsWith("/thumbnail.jpg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Late_incoming_bytes_cannot_replace_a_verified_original()
    {
        var (itemId, photoId) = await UploadedPhotoAsync(_jpeg);
        await Photos().FinalizeAsync(itemId, photoId, CancellationToken);
        var photo = _data.Items.Single().PrimaryPhoto!;
        // Simulate an upload that passed authorization before finalization and completed afterwards.
        _storage.Objects[photo.BlobKey + "/upload"] = [1, 2, 3];
        var content = await Photos().OpenContentAsync(itemId, photoId, thumbnail: false, CancellationToken);
        using var buffer = new MemoryStream();
        await content.Value!.Content.CopyToAsync(buffer, CancellationToken);
        Assert.Equal(_jpeg, buffer.ToArray());
    }

    [Fact]
    public async Task Thumbnail_storage_failure_leaves_the_photo_unfinalized_and_retry_succeeds()
    {
        var (itemId, photoId) = await UploadedPhotoAsync(_jpeg);
        _storage.FailThumbnailWrites = true;

        var failed = await Photos().FinalizeAsync(itemId, photoId, CancellationToken);

        Assert.Equal(ErrorKind.Unavailable, failed.Error!.Kind);
        Assert.Equal(PhotoStatus.Reserved, _data.Items.Single().PrimaryPhoto!.Status);

        _storage.FailThumbnailWrites = false;
        var retried = await Photos().FinalizeAsync(itemId, photoId, CancellationToken);

        Assert.Equal(PhotoStatus.Finalized, retried.Value!.Status);
    }

    [Fact]
    public async Task Undecodable_image_is_unprocessable()
    {
        var (itemId, photoId) = await UploadedPhotoAsync(_jpeg);
        _images.Fail = true;

        var result = await Photos().FinalizeAsync(itemId, photoId, CancellationToken);

        Assert.Equal(ErrorKind.Unprocessable, result.Error!.Kind);
    }

    [Fact]
    public async Task Wrong_hash_is_unprocessable_and_a_correct_reupload_then_finalizes()
    {
        var itemId = await CreateItemAsync();
        var reservation = (await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg), CancellationToken)).Value!.Reservation;
        var corrupted = _jpeg.ToArray();
        corrupted[^1] ^= 0xFF;
        await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/jpeg", corrupted.Length, new MemoryStream(corrupted), CancellationToken);

        var mismatch = await Photos().FinalizeAsync(itemId, reservation.PhotoId, CancellationToken);
        await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/jpeg", _jpeg.Length, new MemoryStream(_jpeg), CancellationToken);
        var fixedUp = await Photos().FinalizeAsync(itemId, reservation.PhotoId, CancellationToken);

        Assert.Equal(ErrorKind.Unprocessable, mismatch.Error!.Kind);
        Assert.Equal(PhotoStatus.Finalized, fixedUp.Value!.Status);
    }

    [Fact]
    public async Task Finalizing_before_the_upload_arrives_is_a_conflict()
    {
        var itemId = await CreateItemAsync();
        var reservation = (await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg), CancellationToken)).Value!.Reservation;

        var result = await Photos().FinalizeAsync(itemId, reservation.PhotoId, CancellationToken);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task Content_that_is_not_the_declared_image_type_is_rejected()
    {
        var text = "not really an image"u8.ToArray();
        var (itemId, photoId) = await UploadedPhotoAsync(text);

        var result = await Photos().FinalizeAsync(itemId, photoId, CancellationToken);

        Assert.Equal(ErrorKind.UnsupportedMediaType, result.Error!.Kind);
    }

    [Fact]
    public async Task Oversized_reservations_and_uploads_are_rejected()
    {
        var itemId = await CreateItemAsync();

        var tooBig = await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg) with { ByteLength = new PhotoPolicy().MaxPhotoBytes + 1 }, CancellationToken);
        var badType = await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg) with { ContentType = "image/gif" }, CancellationToken);
        var reservation = (await Photos().ReserveUploadAsync(itemId, Reserve(_jpeg), CancellationToken)).Value!.Reservation;
        var longer = _jpeg.Concat(new byte[10]).ToArray();
        var overUpload = await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/jpeg", null, new MemoryStream(longer), CancellationToken);
        var wrongType = await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/png", _jpeg.Length, new MemoryStream(_jpeg), CancellationToken);

        Assert.Equal(ErrorKind.PayloadTooLarge, tooBig.Error!.Kind);
        Assert.Equal(ErrorKind.UnsupportedMediaType, badType.Error!.Kind);
        Assert.Equal(ErrorKind.PayloadTooLarge, overUpload.Error!.Kind);
        Assert.Equal(ErrorKind.UnsupportedMediaType, wrongType.Error!.Kind);
    }

    [Fact]
    public async Task Another_user_cannot_touch_someone_elses_draft()
    {
        var itemId = await CreateItemAsync();
        var colleague = Guid.NewGuid();
        _data.Memberships.Add(new MembershipGrant(_projectId, colleague, ProjectRoles.ProjectManager, null));

        var reserve = await Photos(colleague).ReserveUploadAsync(itemId, Reserve(_jpeg), CancellationToken);
        var get = await Items(colleague).GetAsync(itemId, CancellationToken);

        Assert.Equal(ErrorKind.NotFound, reserve.Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, get.Error!.Kind);
    }

    private async Task<Guid> CreateItemAsync() =>
        (await Items().CreateDraftAsync(_projectId, Request(), Guid.NewGuid().ToString(), CancellationToken)).Value!.Item.Id;

    private async Task<(Guid ItemId, Guid PhotoId)> UploadedPhotoAsync(byte[] bytes)
    {
        var itemId = await CreateItemAsync();
        var reservation = (await Photos().ReserveUploadAsync(itemId, Reserve(bytes), CancellationToken)).Value!.Reservation;
        await Photos().UploadContentAsync(itemId, reservation.PhotoId, "image/jpeg", bytes.Length, new MemoryStream(bytes), CancellationToken);
        return (itemId, reservation.PhotoId);
    }

    private CreateDraftItemRequest Request() =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), FieldItemType.PunchList, null, "Unit 214", _trade.Id, null);

    private static ReservePhotoUploadRequest Reserve(byte[] bytes) =>
        new("capture.jpg", "image/jpeg", bytes.Length, Convert.ToBase64String(SHA256.HashData(bytes)), null);

    private FieldItemService Items(Guid? user = null)
    {
        var currentUser = new FakeCurrentUser(user ?? _userId);
        return new FieldItemService(
            _data, _data, _data, _data, new ProjectAuthorizer(_data, currentUser), new ItemAccess(_data, _data, currentUser),
            currentUser, new FakeCorrelation(), TimeProvider.System, new PhotoPolicy());
    }

    private PhotoService Photos(Guid? user = null)
    {
        var currentUser = new FakeCurrentUser(user ?? _userId);
        return new PhotoService(
            _data, _storage, _images, _data, _data, new ItemAccess(_data, _data, currentUser), currentUser,
            new FakeCorrelation(), TimeProvider.System, new PhotoPolicy());
    }

    private sealed class FakePhotoStorage : IPhotoStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public bool FailThumbnailWrites { get; set; }

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            if (FailThumbnailWrites && key.EndsWith("/thumbnail.jpg", StringComparison.Ordinal))
            {
                throw new PhotoStorageException("simulated outage");
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Objects[key] = buffer.ToArray();
        }

        public Task<StoredObject?> GetPropertiesAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Objects.TryGetValue(key, out var bytes) ? new StoredObject(bytes.Length, null) : null);

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Objects[key]));
    }

    private sealed class FakeImageProcessor : IImageProcessor
    {
        public bool Fail { get; set; }

        public ProcessedImage Process(ReadOnlyMemory<byte> image, int thumbnailMaxEdge, long maxPixels) =>
            Fail ? throw new ImageProcessingException("simulated decode failure") : new ProcessedImage(1600, 1200, [0xFF, 0xD8, 0xFF]);
    }
}
