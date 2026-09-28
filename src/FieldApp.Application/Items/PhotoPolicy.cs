namespace FieldApp.Application.Items;

/// <summary>
/// Server-side photo limits (bound from <c>Application:*</c> configuration).
/// <list type="bullet">
/// <item>MaxPhotoBytes 12 MiB: the client sends ~0.8-3 MB JPEGs (2560px long edge); the headroom allows unprocessed fallbacks.</item>
/// <item>MaxPhotoPixels 50 MP: above any phone camera, while blocking decompression bombs.</item>
/// <item>ReservationLifetime 15 minutes: long enough for a slow site connection, short enough to stay narrow.</item>
/// <item>Thumbnail 480px long edge, JPEG: list and preview use only; the original is kept for inspection.</item>
/// </list>
/// </summary>
public sealed record PhotoPolicy
{
    public long MaxPhotoBytes { get; init; } = 12 * 1024 * 1024;

    public long MaxPhotoPixels { get; init; } = 50_000_000;

    public TimeSpan ReservationLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public int ThumbnailMaxEdge { get; init; } = 480;
}
