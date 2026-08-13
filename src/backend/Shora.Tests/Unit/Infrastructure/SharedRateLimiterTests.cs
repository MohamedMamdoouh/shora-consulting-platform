using System.Threading.RateLimiting;
using Shora.Api.Infrastructure;

namespace Shora.Tests.Unit.Infrastructure;

public class SharedRateLimiterTests
{
    [Fact]
    public async Task DisposeAsync_on_wrapper_does_not_dispose_inner_limiter()
    {
        var inner = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 2,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

        await using (var wrapper = new SharedRateLimiter(inner))
        {
            var lease = wrapper.AttemptAcquire();
            Assert.True(lease.IsAcquired);
            lease.Dispose();
        }

        var innerLease = inner.AttemptAcquire();
        Assert.True(innerLease.IsAcquired);
        innerLease.Dispose();

        await inner.DisposeAsync();
    }
}
