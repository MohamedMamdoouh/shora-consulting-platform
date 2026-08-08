using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Shora.Api.Infrastructure;

internal sealed class RateLimiterCache
{
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();

    public RateLimiter GetFixedWindow(string key, int permitLimit, TimeSpan window) =>
        _limiters.GetOrAdd(
            key,
            _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            }));
}
