using Shora.Api.Middleware;

namespace Shora.Api.Infrastructure;

public static class HttpContextExtensions
{
    public static string? GetCorrelationId(this HttpContext context)
    {
        return CorrelationIdMiddleware.TryGetCorrelationId(context, out var correlationId)
            ? correlationId
            : null;
    }
}
