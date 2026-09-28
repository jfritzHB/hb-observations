using System.Security.Cryptography;
using SkiaSharp;

namespace FieldApp.IntegrationTests.Infrastructure;

/// <summary>Synthetic test images generated at runtime; never real construction photos.</summary>
public static class TestImages
{
    public static byte[] Jpeg(int width = 1600, int height = 1200)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(70, 130, 180));
            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(width / 4f, height / 4f, width / 2f, height / 2f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return encoded.ToArray();
    }

    public static string Sha256(byte[] bytes) => Convert.ToBase64String(SHA256.HashData(bytes));
}
