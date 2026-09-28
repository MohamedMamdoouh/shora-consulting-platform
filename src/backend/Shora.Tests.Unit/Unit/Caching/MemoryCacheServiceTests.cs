using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shora.Application.Options;
using Shora.Infrastructure.Services;

namespace Shora.Tests.Unit.Caching;

public class MemoryCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_returns_cached_value_on_second_call()
    {
        var service = CreateService(enabled: true);
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken _) =>
            Task.FromResult<string?>(Interlocked.Increment(ref factoryCalls).ToString());

        var first = await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));
        var second = await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal("1", first);
        Assert.Equal("1", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_bypasses_cache_when_disabled()
    {
        var service = CreateService(enabled: false);
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken _) =>
            Task.FromResult<string?>(Interlocked.Increment(ref factoryCalls).ToString());

        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task Remove_clears_cached_entry()
    {
        var service = CreateService(enabled: true);
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken _) =>
            Task.FromResult<string?>(Interlocked.Increment(ref factoryCalls).ToString());

        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));
        service.Remove("test:key");
        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task RemoveByPrefix_clears_matching_entries()
    {
        var service = CreateService(enabled: true);
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken _) =>
            Task.FromResult<string?>(Interlocked.Increment(ref factoryCalls).ToString());

        await service.GetOrCreateAsync("availability:a", Factory, TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("availability:b", Factory, TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("settings:public", Factory, TimeSpan.FromMinutes(5));

        service.RemoveByPrefix("availability:");

        await service.GetOrCreateAsync("availability:a", Factory, TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("availability:b", Factory, TimeSpan.FromMinutes(5));
        await service.GetOrCreateAsync("settings:public", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal(5, factoryCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_expires_after_ttl()
    {
        var service = CreateService(enabled: true);
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken _) =>
            Task.FromResult<string?>(Interlocked.Increment(ref factoryCalls).ToString());

        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMilliseconds(50));
        await Task.Delay(75);
        await service.GetOrCreateAsync("test:key", Factory, TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, factoryCalls);
    }

    private static MemoryCacheService CreateService(bool enabled)
    {
        return new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheOptions { Enabled = enabled }));
    }
}
