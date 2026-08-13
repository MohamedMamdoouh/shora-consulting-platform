using System.Threading.RateLimiting;

namespace Shora.Api.Infrastructure;

internal static class RateLimitPartitionFactory
{
    public static RateLimitPartition<string> FixedWindowByIp(
        HttpContext httpContext,
        string policyPrefix,
        int permitLimit,
        TimeSpan window) =>
        RateLimitPartition.Get(
            $"{policyPrefix}:ip:{GetClientIp(httpContext)}",
            _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            }));

    private static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
