using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Contracts.Settings;
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

    public async Task<Result<PublicSettingsResponse>> GetPublicAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        if (settings is null)
        {
            return Result<PublicSettingsResponse>.Failure(
                Error.NotFound(ErrorCodes.Settings.NotFound, "Settings not found."));
        }

        return Result<PublicSettingsResponse>.Success(
            new PublicSettingsResponse(settings.SessionPrice, settings.SessionDurationMinutes));
    }

    public Task InvalidateCacheAsync(CancellationToken cancellationToken = default) =>
        cacheInvalidator.InvalidatePublicSettingsAsync(cancellationToken);
}
