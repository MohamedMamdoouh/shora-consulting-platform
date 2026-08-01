using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Availability;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Contracts.Availability;

namespace Shora.Application.Services;

public class AvailabilityService(
    IApplicationDbContext dbContext,
    ICacheService cache,
    IDateTimeProvider dateTimeProvider,
    IOptions<CacheOptions> cacheOptions)
{
    public async Task<Result<AvailabilityResponse>> GetOpenSlotsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = AvailabilityRangeValidator.Normalize(fromUtc, toUtc, dateTimeProvider.UtcNow);
        if (rangeResult.IsFailure)
        {
            return rangeResult.Error!;
        }

        var (effectiveFromUtc, effectiveToUtc) = rangeResult.Value;

        var response = await cache.GetOrCreateAsync(
            CacheKeys.Availability(effectiveFromUtc, effectiveToUtc),
            async ct => await LoadOpenSlotsAsync(effectiveFromUtc, effectiveToUtc, ct),
            cacheOptions.Value.AvailabilityTtl,
            cancellationToken);

        return response ?? new AvailabilityResponse([]);
    }

    private async Task<AvailabilityResponse> LoadOpenSlotsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var blockedRanges = await dbContext.BlockedDates
            .AsNoTracking()
            .Select(block => new BlockedRangeSpec(block.StartUtc, block.EndUtc))
            .ToListAsync(cancellationToken);

        var slots = await dbContext.AvailabilitySlots
            .AsNoTracking()
            .Where(slot => !slot.IsBooked && slot.StartTimeUtc >= fromUtc && slot.StartTimeUtc < toUtc)
            .OrderBy(slot => slot.StartTimeUtc)
            .ToListAsync(cancellationToken);

        var openSlots = slots
            .Where(slot => !SlotScheduleCalculator.OverlapsBlockedRange(
                slot.StartTimeUtc,
                slot.EndTimeUtc,
                blockedRanges))
            .Select(slot => new AvailabilitySlotDto(slot.Id, slot.StartTimeUtc, slot.EndTimeUtc))
            .ToList();

        return new AvailabilityResponse(openSlots);
    }
}
