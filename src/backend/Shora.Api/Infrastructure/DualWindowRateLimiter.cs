using System.Threading.RateLimiting;

namespace Shora.Api.Infrastructure;

internal sealed class DualWindowRateLimiter : RateLimiter
{
    private readonly RateLimiter _first;
    private readonly RateLimiter? _second;
    private int _disposed;

    public DualWindowRateLimiter(RateLimiter first, RateLimiter? second)
    {
        _first = first;
        _second = second;
    }

    public override RateLimiterStatistics? GetStatistics() => _first.GetStatistics();

    public override TimeSpan? IdleDuration => _first.IdleDuration;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var firstLease = _first.AttemptAcquire(permitCount);
        if (!firstLease.IsAcquired)
        {
            return firstLease;
        }

        if (_second is null)
        {
            return firstLease;
        }

        var secondLease = _second.AttemptAcquire(permitCount);
        if (secondLease.IsAcquired)
        {
            return new DualWindowRateLimitLease(firstLease, secondLease);
        }

        firstLease.Dispose();
        return secondLease;
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        return AcquireAsyncInternal(permitCount, cancellationToken);
    }

    private async ValueTask<RateLimitLease> AcquireAsyncInternal(int permitCount, CancellationToken cancellationToken)
    {
        var firstLease = await _first.AcquireAsync(permitCount, cancellationToken);
        if (!firstLease.IsAcquired)
        {
            return firstLease;
        }

        if (_second is null)
        {
            return firstLease;
        }

        var secondLease = await _second.AcquireAsync(permitCount, cancellationToken);
        if (secondLease.IsAcquired)
        {
            return new DualWindowRateLimitLease(firstLease, secondLease);
        }

        firstLease.Dispose();
        return secondLease;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return default;
        }

        return default;
    }

    private sealed class DualWindowRateLimitLease : RateLimitLease
    {
        private readonly RateLimitLease _first;
        private readonly RateLimitLease _second;

        public DualWindowRateLimitLease(RateLimitLease first, RateLimitLease second)
        {
            _first = first;
            _second = second;
        }

        public override bool IsAcquired => _first.IsAcquired && _second.IsAcquired;

        public override IEnumerable<string> MetadataNames => _first.MetadataNames.Concat(_second.MetadataNames);

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (_first.TryGetMetadata(metadataName, out metadata))
            {
                return true;
            }

            return _second.TryGetMetadata(metadataName, out metadata);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _first.Dispose();
                _second.Dispose();
            }
        }
    }
}
