namespace FieldApp.Domain.FieldItems;

/// <summary>Supported photo formats, identified by their file signature (never by extension or declared MIME type alone).</summary>
public static class ImageSignature
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";

    /// <summary>Bytes needed to identify every supported format.</summary>
    public const int HeaderLength = 12;

    public static IReadOnlySet<string> SupportedMediaTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { Jpeg, Png, Webp };

    public static bool IsSupported(string? mediaType) => mediaType is not null && SupportedMediaTypes.Contains(mediaType);

    /// <summary>The media type indicated by the leading bytes, or null when it is not a supported image.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return Jpeg;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return null;
    }
}
