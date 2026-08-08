using System.Security.Claims;
using System.Threading.RateLimiting;
using Shora.Api.Middleware;

namespace Shora.Api.Infrastructure;

internal static class RateLimitPartitionFactory
{
    public static RateLimitPartition<string> FixedWindowByIp(
        HttpContext httpContext,
        string policyPrefix,
        int permitLimit,
        TimeSpan window,
        RateLimiterCache cache) =>
        FixedWindow(
            $"{policyPrefix}:ip:{GetClientIp(httpContext)}",
            permitLimit,
            window,
            cache);

    public static RateLimitPartition<string> FixedWindowByIpAndEmail(
        HttpContext httpContext,
        string policyPrefix,
        int permitLimit,
        TimeSpan window,
        RateLimiterCache cache)
    {
        var ip = GetClientIp(httpContext);
        var email = AuthRateLimitEmailMiddleware.TryGetAuthEmail(httpContext);
        var partitionKey = string.IsNullOrWhiteSpace(email)
            ? $"{policyPrefix}:ip:{ip}"
            : $"{policyPrefix}:{ip}:{email}";

        return RateLimitPartition.Get(partitionKey, _ =>
        {
            var ipLimiter = cache.GetFixedWindow($"{policyPrefix}:ip:{ip}", permitLimit, window);
            if (string.IsNullOrWhiteSpace(email))
            {
                return ipLimiter;
            }

            var emailLimiter = cache.GetFixedWindow($"{policyPrefix}:email:{email}", permitLimit, window);
            return new DualWindowRateLimiter(ipLimiter, emailLimiter);
        });
    }

    public static RateLimitPartition<string> FixedWindowByUserOrIp(
        HttpContext httpContext,
        string policyPrefix,
        int permitLimit,
        TimeSpan window,
        RateLimiterCache cache)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = !string.IsNullOrEmpty(userId)
            ? $"{policyPrefix}:user:{userId}"
            : $"{policyPrefix}:ip:{GetClientIp(httpContext)}";

        return FixedWindow(partitionKey, permitLimit, window, cache);
    }

    private static RateLimitPartition<string> FixedWindow(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        RateLimiterCache cache) =>
        RateLimitPartition.Get(
            partitionKey,
            _ => cache.GetFixedWindow(partitionKey, permitLimit, window));

    private static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
