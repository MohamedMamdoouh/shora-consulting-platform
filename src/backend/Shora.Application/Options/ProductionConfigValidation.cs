namespace Shora.Application.Options;

internal static class ProductionConfigValidation
{
    internal const string PlaceholderHost = "YOUR_PRODUCTION_HOST";

    internal static bool IsValidProductionHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains(PlaceholderHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host);
    }

    internal static bool ContainsPlaceholder(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(PlaceholderHost, StringComparison.OrdinalIgnoreCase);
}
