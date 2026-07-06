using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public class SettingsService(IApplicationDbContext dbContext)
{
    public async Task<Settings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);
    }
}
