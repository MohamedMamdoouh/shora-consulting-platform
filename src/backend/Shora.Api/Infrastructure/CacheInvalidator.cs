using Microsoft.AspNetCore.OutputCaching;
using Shora.Application.Abstractions;
using Shora.Application.Common;

namespace Shora.Api.Infrastructure;

public sealed class CacheInvalidator(
    ICacheService cacheService,
    IOutputCacheStore outputCacheStore) : ICacheInvalidator
{
    public async Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default)
    {
        cacheService.Remove(CacheKeys.SettingsPublic);
        await outputCacheStore.EvictByTagAsync(CacheOutputTags.PublicSettings, cancellationToken);
    }

    public async Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cacheService.RemoveByPrefix(CacheKeys.AvailabilityPrefix);
        await outputCacheStore.EvictByTagAsync(CacheOutputTags.PublicAvailability, cancellationToken);
    }
}
