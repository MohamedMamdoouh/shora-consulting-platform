using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class MemoryCacheService(
    IMemoryCache memoryCache,
    IOptions<CacheOptions> options) : ICacheService
{
    private readonly CacheOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await factory(cancellationToken);
        }

        if (memoryCache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        _keys.TryAdd(key, 0);
        memoryCache.Set(key, value, ttl);
        return value;
    }

    public void Remove(string key)
    {
        memoryCache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Remove(key);
        }
    }
}
