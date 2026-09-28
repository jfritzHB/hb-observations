using FieldApp.Application.Items;
using SkiaSharp;

namespace FieldApp.Infrastructure.Imaging;

/// <summary>
/// Reads dimensions and creates a JPEG thumbnail with SkiaSharp. Dimensions are read from the header before decoding
/// so oversized images are rejected without allocating their pixels. Orientation is normalized by the client.
/// </summary>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    private const int ThumbnailQuality = 80;

    public ProcessedImage Process(ReadOnlyMemory<byte> image, int thumbnailMaxEdge, long maxPixels)
    {
        using var data = SKData.CreateCopy(image.Span);
        using var codec = SKCodec.Create(data)
            ?? throw new ImageProcessingException("The uploaded file could not be read as an image.");

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0)
        {
            throw new ImageProcessingException("The uploaded image has no dimensions.");
        }

        if ((long)width * height > maxPixels)
        {
            throw new ImageProcessingException($"The image is larger than {maxPixels / 1_000_000} megapixels.");
        }

        using var bitmap = SKBitmap.Decode(codec)
            ?? throw new ImageProcessingException("The uploaded image could not be decoded.");

        var scale = Math.Min(1d, (double)thumbnailMaxEdge / Math.Max(width, height));
        var thumbnailInfo = new SKImageInfo(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));

        using var thumbnail = bitmap.Resize(thumbnailInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new ImageProcessingException("A thumbnail could not be created.");
        using var encoded = SKImage.FromBitmap(thumbnail).Encode(SKEncodedImageFormat.Jpeg, ThumbnailQuality)
            ?? throw new ImageProcessingException("A thumbnail could not be encoded.");

        return new ProcessedImage(width, height, encoded.ToArray());
    }
}
