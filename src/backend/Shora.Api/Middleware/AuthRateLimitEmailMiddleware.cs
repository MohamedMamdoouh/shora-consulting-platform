using System.Text.Json;
using Shora.Api.Infrastructure;

namespace Shora.Api.Middleware;

public sealed class AuthRateLimitEmailMiddleware(RequestDelegate next)
{
    private static readonly object AuthEmailItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresAuthEmailExtraction(context))
        {
            var email = await TryExtractEmailFromJsonBodyAsync(context.Request, context.RequestAborted);
            if (!string.IsNullOrWhiteSpace(email))
            {
                context.Items[AuthEmailItemKey] = email.Trim().ToLowerInvariant();
            }
        }

        await next(context);
    }

    public static string? TryGetAuthEmail(HttpContext context)
    {
        if (context.Items.TryGetValue(AuthEmailItemKey, out var value) && value is string email)
        {
            return email;
        }

        return null;
    }

    private static bool RequiresAuthEmailExtraction(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<ExtractAuthEmailForRateLimitAttribute>() is not null;

    private static async Task<string?> TryExtractEmailFromJsonBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        request.EnableBuffering();

        try
        {
            request.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            request.Body.Position = 0;

            if (document.RootElement.TryGetProperty("email", out var emailProperty)
                && emailProperty.ValueKind == JsonValueKind.String)
            {
                return emailProperty.GetString();
            }
        }
        catch (JsonException)
        {
            request.Body.Position = 0;
        }

        return null;
    }
}

public static class AuthRateLimitEmailMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthRateLimitEmail(this IApplicationBuilder app) =>
        app.UseMiddleware<AuthRateLimitEmailMiddleware>();
}
