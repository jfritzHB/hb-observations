using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldApp.Application.Items;
using FieldApp.Domain.FieldItems;
using FieldApp.Infrastructure.Persistence;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>The photo lifecycle against SQL Server and Azurite: reserve, upload, verify, finalize, thumbnail.</summary>
public sealed class PhotoApiTests(SqlServerContainerFixture sqlServer) : IAsyncLifetime
{
    private static readonly Guid _mosEisley = ProjectId(ProjectNumbers.MosEisley);

    private FieldAppFactory? _factory;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private FieldAppFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async ValueTask InitializeAsync()
    {
        if (sqlServer.ServerConnectionString is null)
        {
            return;
        }

        var container = SqlServerContainerFixture.UniqueContainerName("photos");
        var database = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("Photos"), seedDemoData: true, CancellationToken, container);
        _factory = sqlServer.Factory(database, container);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Photo_is_reserved_uploaded_verified_finalized_with_thumbnail_and_audited()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var itemId = await CreateItemAsync(client);
        var jpeg = TestImages.Jpeg(1600, 1200);

        var reserve = await client.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(jpeg), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, reserve.StatusCode);
        var reservation = (await reserve.Content.ReadFromJsonAsync<PhotoUploadReservationDto>(FieldAppFactory.Json, CancellationToken))!;
        Assert.Equal("PUT", reservation.Method);
        Assert.Equal("image/jpeg", reservation.AllowedContentType);
        Assert.Equal(jpeg.Length, reservation.MaxBytes);
        Assert.True(reservation.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.StartsWith("/api/v1/items/", reservation.UploadUrl, StringComparison.Ordinal); // Never a storage URL.

        var repeat = await client.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(jpeg), CancellationToken);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(reservation.PhotoId, (await repeat.Content.ReadFromJsonAsync<PhotoUploadReservationDto>(FieldAppFactory.Json, CancellationToken))!.PhotoId);

        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, reservation.UploadUrl, jpeg)).StatusCode);

        var finalize = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);
        var finalizeAgain = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, finalize.StatusCode);
        Assert.Equal(HttpStatusCode.OK, finalizeAgain.StatusCode);
        var photo = (await finalize.Content.ReadFromJsonAsync<FieldItemPhotoDto>(FieldAppFactory.Json, CancellationToken))!;
        Assert.Equal(PhotoStatus.Finalized, photo.Status);
        Assert.Equal((1600, 1200), (photo.Width, photo.Height));
        Assert.True(photo.IsPrimary);

        var thumbnail = await client.GetAsync(photo.ThumbnailUrl, CancellationToken);
        Assert.Equal("image/jpeg", thumbnail.Content.Headers.ContentType?.MediaType);
        var thumbnailBytes = await thumbnail.Content.ReadAsByteArrayAsync(CancellationToken);
        Assert.InRange(thumbnailBytes.Length, 1, jpeg.Length - 1);
        var original = await client.GetByteArrayAsync(photo.ContentUrl, CancellationToken);
        Assert.Equal(jpeg, original);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldAppDbContext>();
        Assert.Equal(1, await db.FieldItemPhotos.CountAsync(p => p.FieldItemId == itemId && p.Status == PhotoStatus.Finalized, CancellationToken));
        var actions = await db.AuditEvents.Where(e => e.EntityId == reservation.PhotoId).Select(e => e.Action).ToListAsync(CancellationToken);
        Assert.Equal(["PhotoContentUploaded", "PhotoFinalized", "PhotoUploadReserved"], actions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_second_primary_photo_cannot_be_reserved_after_finalization()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var (itemId, _) = await FinalizedPhotoAsync(client);

        var another = await client.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(TestImages.Jpeg(800, 600)), CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, another.StatusCode);
    }

    [Fact]
    public async Task Finalizing_before_upload_is_a_conflict_and_the_photo_can_then_be_completed()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var itemId = await CreateItemAsync(client);
        var jpeg = TestImages.Jpeg();
        var reservation = await ReserveAsync(client, itemId, jpeg);

        var early = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);
        await UploadAsync(client, reservation.UploadUrl, jpeg);
        var later = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
    }

    [Fact]
    public async Task Wrong_hash_is_rejected_and_the_photo_stays_retryable()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var itemId = await CreateItemAsync(client);
        var jpeg = TestImages.Jpeg();
        var reservation = await ReserveAsync(client, itemId, jpeg);
        var tampered = jpeg.ToArray();
        tampered[^3] ^= 0x55;

        await UploadAsync(client, reservation.UploadUrl, tampered);
        var rejected = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);
        await UploadAsync(client, reservation.UploadUrl, jpeg);
        var accepted = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Content_that_is_not_an_image_is_rejected_despite_the_declared_type()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var itemId = await CreateItemAsync(client);
        var html = "<html>definitely not a photo</html>"u8.ToArray();
        var reservation = await ReserveAsync(client, itemId, html);

        await UploadAsync(client, reservation.UploadUrl, html);
        var finalize = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, finalize.StatusCode);
    }

    [Fact]
    public async Task Unsupported_types_oversized_reservations_and_oversized_uploads_are_rejected()
    {
        sqlServer.SkipIfUnavailable();
        using var client = Client();
        var itemId = await CreateItemAsync(client);
        var jpeg = TestImages.Jpeg();

        var gif = await client.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(jpeg, "image/gif"), CancellationToken);
        var huge = await client.PostAsJsonAsync(
            $"/api/v1/items/{itemId}/photos/uploads",
            new { fileName = "x.jpg", contentType = "image/jpeg", byteLength = 64L * 1024 * 1024, sha256 = TestImages.Sha256(jpeg) },
            CancellationToken);
        var reservation = await ReserveAsync(client, itemId, jpeg);
        var tooLong = await UploadAsync(client, reservation.UploadUrl, [.. jpeg, .. new byte[100]]);
        var wrongType = await UploadAsync(client, reservation.UploadUrl, jpeg, "image/png");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, gif.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, huge.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
    }

    [Fact]
    public async Task Knowing_an_item_id_grants_no_photo_access_to_other_users()
    {
        sqlServer.SkipIfUnavailable();
        using var owner = Client();
        using var colleague = Client(PersonaKeys.ProjectManager);
        using var outsider = Client(PersonaKeys.Unassigned);
        var (itemId, photoId) = await FinalizedPhotoAsync(owner);
        var jpeg = TestImages.Jpeg(640, 480);

        Assert.Equal(HttpStatusCode.NotFound, (await colleague.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(jpeg), CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync($"/api/v1/items/{itemId}/photos/{photoId}/finalize", null, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(colleague, $"/api/v1/items/{itemId}/photos/{photoId}/content", jpeg)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await colleague.GetAsync($"/api/v1/items/{itemId}/photos/{photoId}/content", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/v1/items/{Guid.NewGuid()}/photos/{photoId}/finalize", null, CancellationToken)).StatusCode);
    }

    private HttpClient Client(string persona = PersonaKeys.Superintendent) => Factory.CreateClientAs(persona);

    private static async Task<Guid> CreateItemAsync(HttpClient client)
    {
        var draftId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/projects/{_mosEisley}/items")
        {
            Content = JsonContent.Create(new { clientDraftId = draftId, type = "Observation", locationDetail = "Unit 214", tradeId = TradeId("ELE") }),
        };
        request.Headers.Add("Idempotency-Key", draftId.ToString());
        var response = await client.SendAsync(request, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FieldItemDto>(FieldAppFactory.Json, CancellationToken))!.Id;
    }

    private static object Reservation(byte[] bytes, string contentType = "image/jpeg") =>
        new { fileName = "capture.jpg", contentType, byteLength = bytes.Length, sha256 = TestImages.Sha256(bytes) };

    private static async Task<PhotoUploadReservationDto> ReserveAsync(HttpClient client, Guid itemId, byte[] bytes)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/items/{itemId}/photos/uploads", Reservation(bytes), CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Reservation failed: {(int)response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<PhotoUploadReservationDto>(FieldAppFactory.Json, CancellationToken))!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] bytes, string contentType = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return client.PutAsync(url, content, CancellationToken);
    }

    private static async Task<(Guid ItemId, Guid PhotoId)> FinalizedPhotoAsync(HttpClient client)
    {
        var itemId = await CreateItemAsync(client);
        var jpeg = TestImages.Jpeg(1024, 768);
        var reservation = await ReserveAsync(client, itemId, jpeg);
        await UploadAsync(client, reservation.UploadUrl, jpeg);
        var finalize = await client.PostAsync($"/api/v1/items/{itemId}/photos/{reservation.PhotoId}/finalize", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, finalize.StatusCode);
        return (itemId, reservation.PhotoId);
    }
}
