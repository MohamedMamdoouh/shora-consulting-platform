using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class RefreshTokenPurgeService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<RefreshTokenPurgeService> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var deletedCount = await dbContext.RefreshTokens
            .Where(token => token.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation("Refresh token purge deleted {DeletedCount} expired token(s).", deletedCount);
        }

        return deletedCount;
    }
}
