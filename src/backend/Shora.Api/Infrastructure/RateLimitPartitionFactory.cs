using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Shora.Api.Middleware;

namespace Shora.Api.Infrastructure;

internal static class RateLimitPartitionFactory
{
    internal const int EmailPartitionBucketCount = 4096;

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
        var emailBucket = string.IsNullOrWhiteSpace(email)
            ? null
            : GetEmailBucket(email);
        var partitionKey = emailBucket is null
            ? $"{policyPrefix}:ip:{ip}"
            : $"{policyPrefix}:ip:{ip}:email-bucket:{emailBucket.Value}";

        return RateLimitPartition.Get(partitionKey, _ =>
        {
            var ipLimiter = cache.GetFixedWindow($"{policyPrefix}:ip:{ip}", permitLimit, window);
            if (emailBucket is null)
            {
                return ipLimiter;
            }

            var emailLimiter = cache.GetFixedWindow($"{policyPrefix}:email-bucket:{emailBucket.Value}", permitLimit, window);
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

    private static int GetEmailBucket(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % EmailPartitionBucketCount);
    }
}
