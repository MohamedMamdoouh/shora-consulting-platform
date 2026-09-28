using Shora.Api.Infrastructure;
using Shora.Application.Abstractions;
using Shora.Application.Common;

namespace Shora.Tests.Unit.Caching;

public class CacheInvalidatorTests
{
    [Fact]
    public async Task InvalidatePublicSettingsAsync_clears_app_cache_and_output_tag()
    {
        var cacheService = new FakeCacheService();
        var outputCacheStore = new FakeOutputCacheStore();
        var invalidator = new CacheInvalidator(cacheService, outputCacheStore);

        await cacheService.GetOrCreateAsync(
            CacheKeys.SettingsPublic,
            _ => Task.FromResult<object?>(new object()),
            TimeSpan.FromMinutes(5));

        await invalidator.InvalidatePublicSettingsAsync();

        Assert.False(cacheService.Contains(CacheKeys.SettingsPublic));
        Assert.Contains(CacheOutputTags.PublicSettings, outputCacheStore.EvictedTags);
    }

    [Fact]
    public async Task InvalidateAvailabilityAsync_clears_availability_prefix_and_output_tag()
    {
        var cacheService = new FakeCacheService();
        var outputCacheStore = new FakeOutputCacheStore();
        var invalidator = new CacheInvalidator(cacheService, outputCacheStore);

        await cacheService.GetOrCreateAsync(
            CacheKeys.Availability(DateTime.UtcNow, DateTime.UtcNow.AddDays(7)),
            _ => Task.FromResult<object?>(new object()),
            TimeSpan.FromSeconds(30));

        await invalidator.InvalidateAvailabilityAsync();

        Assert.Empty(cacheService.Keys);
        Assert.Contains(CacheOutputTags.PublicAvailability, outputCacheStore.EvictedTags);
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> _entries = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => _entries.Keys;

        public Task<T?> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T?>> factory,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                return Task.FromResult((T?)existing);
            }

            return factory(cancellationToken).ContinueWith(
                task =>
                {
                    var value = task.Result;
                    _entries[key] = value;
                    return value;
                },
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void Remove(string key) => _entries.Remove(key);

        public void RemoveByPrefix(string prefix)
        {
            foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                Remove(key);
            }
        }

        public bool Contains(string key) => _entries.ContainsKey(key);
    }

    private sealed class FakeOutputCacheStore : Microsoft.AspNetCore.OutputCaching.IOutputCacheStore
    {
        public List<string> EvictedTags { get; } = [];

        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
        {
            EvictedTags.Add(tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult<byte[]?>(null);

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask TagAsync(string key, string[] tags, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
