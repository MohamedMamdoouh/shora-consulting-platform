using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;

namespace Shora.Tests.Unit.Caching;

public class SettingsServiceCacheTests
{
    [Fact]
    public async Task GetAsync_uses_settings_public_cache_key()
    {
        var settings = new Settings { Id = Settings.SingletonId, SessionPrice = 500, SessionDurationMinutes = 60 };
        var dbContext = CreateDbContext(settings);
        var cache = new TrackingCacheService();

        var service = new SettingsService(
            dbContext,
            cache,
            new NoOpCacheInvalidator(),
            Options.Create(new CacheOptions()));

        var result = await service.GetAsync();

        Assert.Equal(CacheKeys.SettingsPublic, cache.LastKey);
        Assert.Equal(1, cache.FactoryCalls);
        Assert.Equal(settings.SessionPrice, result?.SessionPrice);
    }

    [Fact]
    public async Task GetAsync_returns_cached_value_on_second_call()
    {
        var settings = new Settings { Id = Settings.SingletonId, SessionPrice = 500, SessionDurationMinutes = 60 };
        var dbContext = CreateDbContext(settings);
        var cache = new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheOptions()));

        var service = new SettingsService(
            dbContext,
            cache,
            new NoOpCacheInvalidator(),
            Options.Create(new CacheOptions()));

        var first = await service.GetAsync();
        var second = await service.GetAsync();

        Assert.Equal(first?.SessionPrice, second?.SessionPrice);
    }

    [Fact]
    public async Task InvalidateCacheAsync_delegates_to_cache_invalidator()
    {
        var dbContext = CreateDbContext(new Settings { Id = Settings.SingletonId });
        var cacheInvalidator = new TrackingCacheInvalidator();

        var service = new SettingsService(
            dbContext,
            new TrackingCacheService(),
            cacheInvalidator,
            Options.Create(new CacheOptions()));

        await service.InvalidateCacheAsync();

        Assert.Equal(1, cacheInvalidator.PublicSettingsInvalidations);
    }

    private static ApplicationDbContext CreateDbContext(Settings settings)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Settings.Add(settings);
        dbContext.SaveChanges();
        return dbContext;
    }

    private sealed class TrackingCacheService : ICacheService
    {
        public string? LastKey { get; private set; }

        public int FactoryCalls { get; private set; }

        public Task<T?> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T?>> factory,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            LastKey = key;
            FactoryCalls++;
            return factory(cancellationToken);
        }

        public void Remove(string key)
        {
        }

        public void RemoveByPrefix(string prefix)
        {
        }
    }

    private sealed class TrackingCacheInvalidator : ICacheInvalidator
    {
        public int PublicSettingsInvalidations { get; private set; }

        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default)
        {
            PublicSettingsInvalidations++;
            return Task.CompletedTask;
        }

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
