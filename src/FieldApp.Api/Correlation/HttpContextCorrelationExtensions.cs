namespace FieldApp.Api.Correlation;

public static class HttpContextCorrelationExtensions
{
    internal static readonly object ItemKey = new();

    public static string? GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
    }
}
