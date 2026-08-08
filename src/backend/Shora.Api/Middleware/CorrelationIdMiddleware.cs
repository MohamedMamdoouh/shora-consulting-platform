using System.Text.RegularExpressions;

namespace Shora.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxLength = 64;

    private static readonly object CorrelationIdItemKey = new();

    private static readonly Regex SafeIdPattern = SafeCorrelationIdRegex();

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = ResolveCorrelationId(incoming, logger);

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static bool TryGetCorrelationId(HttpContext context, out string? correlationId)
    {
        if (context.Items.TryGetValue(CorrelationIdItemKey, out var value) && value is string id)
        {
            correlationId = id;
            return true;
        }

        correlationId = null;
        return false;
    }

    internal static string ResolveCorrelationId(string? incoming, ILogger? logger = null)
    {
        if (IsValidCorrelationId(incoming))
        {
            return incoming!;
        }

        if (!string.IsNullOrWhiteSpace(incoming))
        {
            logger?.LogDebug(
                "Ignoring invalid {HeaderName} header value; generating a new correlation ID.",
                HeaderName);
        }

        return Guid.NewGuid().ToString("N");
    }

    internal static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        if (Guid.TryParse(value, out _))
        {
            return true;
        }

        return SafeIdPattern.IsMatch(value);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex SafeCorrelationIdRegex();
}
