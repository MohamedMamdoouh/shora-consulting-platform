using System.Threading.RateLimiting;

namespace Shora.Api.Infrastructure;

internal sealed class SharedRateLimiter(RateLimiter inner) : RateLimiter
{
    public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();

    public override TimeSpan? IdleDuration => inner.IdleDuration;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
        inner.AttemptAcquire(permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken) =>
        inner.AcquireAsync(permitCount, cancellationToken);

    protected override ValueTask DisposeAsyncCore() => default;
}
