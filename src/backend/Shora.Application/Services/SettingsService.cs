using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.AdminSettings;
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
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings not found.");
        }

        return new PublicSettingsResponse(settings.SessionPrice, settings.SessionDurationMinutes);
    }

    public async Task<Result<AdminSettingsResponse>> GetAdminAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings not found.");
        }

        return MapAdminResponse(settings);
    }

    public async Task<Result<AdminSettingsResponse>> UpdateAsync(
        ValidatedSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Settings
            .FirstOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings not found.");
        }

        settings.SessionPrice = update.SessionPrice;
        settings.SessionDurationMinutes = update.SessionDurationMinutes;
        settings.BufferMinutes = update.BufferMinutes;
        settings.ReceiptUploadWindowMinutes = update.ReceiptUploadWindowMinutes;
        settings.CancellationRequestAutoDeclineHours = update.CancellationRequestAutoDeclineHours;
        settings.ConsultantWhatsAppNumber = update.ConsultantWhatsAppNumber;
        settings.VodafoneCashNumber = update.VodafoneCashNumber;
        settings.InstaPayHandle = update.InstaPayHandle;
        settings.PaymentInstructions = update.PaymentInstructions;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return MapAdminResponse(settings);
    }

    public Task InvalidateCacheAsync(CancellationToken cancellationToken = default) =>
        cacheInvalidator.InvalidatePublicSettingsAsync(cancellationToken);

    private static AdminSettingsResponse MapAdminResponse(Settings settings) =>
        new(
            settings.SessionPrice,
            settings.SessionDurationMinutes,
            settings.BufferMinutes,
            settings.ReceiptUploadWindowMinutes,
            settings.CancellationRequestAutoDeclineHours,
            settings.ConsultantWhatsAppNumber,
            settings.VodafoneCashNumber,
            settings.InstaPayHandle,
            settings.PaymentInstructions,
            settings.ReceiptRetentionMonths);
}
