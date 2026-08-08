using System.Threading.RateLimiting;
using Shora.Api.Infrastructure;

namespace Shora.Tests.Unit.Infrastructure;

public class DualWindowRateLimiterTests
{
    [Fact]
    public void AttemptAcquire_disposes_first_lease_when_second_window_rejects()
    {
        var first = new TrackingRateLimiter(acquire: true, "first");
        var second = new TrackingRateLimiter(acquire: false, "second");
        using var limiter = new DualWindowRateLimiter(first, second);

        var lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
        Assert.Equal(1, first.AttemptAcquireCalls);
        Assert.Equal(1, second.AttemptAcquireCalls);
        Assert.Equal(1, first.DisposedLeaseCount);
        Assert.Equal(0, second.DisposedLeaseCount);

        lease.Dispose();
        Assert.Equal(1, second.DisposedLeaseCount);
    }

    [Fact]
    public async Task AcquireAsync_disposes_first_lease_when_second_window_rejects()
    {
        var first = new TrackingRateLimiter(acquire: true, "first");
        var second = new TrackingRateLimiter(acquire: false, "second");
        await using var limiter = new DualWindowRateLimiter(first, second);

        var lease = await limiter.AcquireAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(lease.IsAcquired);
        Assert.Equal(1, first.AcquireAsyncCalls);
        Assert.Equal(1, second.AcquireAsyncCalls);
        Assert.Equal(1, first.DisposedLeaseCount);
        Assert.Equal(0, second.DisposedLeaseCount);

        lease.Dispose();
        Assert.Equal(1, second.DisposedLeaseCount);
    }

    [Fact]
    public void AttemptAcquire_returns_combined_lease_that_disposes_both_windows()
    {
        var first = new TrackingRateLimiter(acquire: true, "first");
        var second = new TrackingRateLimiter(acquire: true, "second");
        using var limiter = new DualWindowRateLimiter(first, second);

        var lease = limiter.AttemptAcquire();

        Assert.True(lease.IsAcquired);
        Assert.Contains("first", lease.MetadataNames);
        Assert.Contains("second", lease.MetadataNames);
        Assert.True(lease.TryGetMetadata("first", out var firstMetadata));
        Assert.Equal("first", firstMetadata);
        Assert.True(lease.TryGetMetadata("second", out var secondMetadata));
        Assert.Equal("second", secondMetadata);

        lease.Dispose();

        Assert.Equal(1, first.DisposedLeaseCount);
        Assert.Equal(1, second.DisposedLeaseCount);
    }

    private sealed class TrackingRateLimiter(bool acquire, string metadataName) : RateLimiter
    {
        public int AttemptAcquireCalls { get; private set; }

        public int AcquireAsyncCalls { get; private set; }

        public int DisposedLeaseCount { get; private set; }

        public override TimeSpan? IdleDuration => null;

        public override RateLimiterStatistics? GetStatistics() => null;

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            _ = permitCount;
            AttemptAcquireCalls++;
            return new TrackingLease(this, acquire, metadataName);
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(
            int permitCount,
            CancellationToken cancellationToken)
        {
            _ = permitCount;
            _ = cancellationToken;
            AcquireAsyncCalls++;
            return ValueTask.FromResult<RateLimitLease>(new TrackingLease(this, acquire, metadataName));
        }

        protected override ValueTask DisposeAsyncCore() => default;

        public void RecordLeaseDisposed()
        {
            DisposedLeaseCount++;
        }
    }

    private sealed class TrackingLease(
        TrackingRateLimiter owner,
        bool isAcquired,
        string metadataName) : RateLimitLease
    {
        private int _disposed;

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames =>
            isAcquired ? [metadataName] : [];

        public override bool TryGetMetadata(string name, out object? metadata)
        {
            if (isAcquired && name == metadataName)
            {
                metadata = metadataName;
                return true;
            }

            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.RecordLeaseDisposed();
            }
        }
    }
}
