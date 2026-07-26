using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public class SettingsService(
    IApplicationDbContext dbContext,
    ICacheService cache,
    ICacheInvalidator cacheInvalidator,
    IOptions<CacheOptions> cacheOptions)
{
    public async Task<Settings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.SettingsPublic,
            async ct => await dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == Settings.SingletonId, ct),
            cacheOptions.Value.SettingsPublicTtl,
            cancellationToken);
    }

    public Task InvalidateCacheAsync(CancellationToken cancellationToken = default) =>
        cacheInvalidator.InvalidatePublicSettingsAsync(cancellationToken);
}
