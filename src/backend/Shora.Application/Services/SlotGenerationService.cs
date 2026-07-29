using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Availability;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public sealed class SlotGenerationService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<SlotGenerationService> logger)
{
    public async Task GenerateHorizonAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            logger.LogWarning("Skipping slot generation because settings row {SettingsId} was not found.",
                Settings.SingletonId);
            return;
        }

        var windows = await dbContext.AvailabilityWindows
            .AsNoTracking()
            .Where(window => window.IsActive)
            .ToListAsync(cancellationToken);

        if (windows.Count == 0)
        {
            logger.LogInformation("Skipping slot generation because no active availability windows exist.");
            return;
        }

        var blockedRanges = await dbContext.BlockedDates
            .AsNoTracking()
            .Select(block => new BlockedRangeSpec(block.StartUtc, block.EndUtc))
            .ToListAsync(cancellationToken);

        var horizonStartUtc = dateTimeProvider.UtcNow;
        var horizonEndUtc = horizonStartUtc.AddDays(SlotGenerationConstants.HorizonWeeks * 7);
        var consultantTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SlotGenerationConstants.ConsultantTimeZoneId);

        var windowSpecs = windows
            .Select(window => new AvailabilityWindowSpec(window.DayOfWeek, window.StartTime, window.EndTime, window.IsActive))
            .ToList();

        var desiredSlots = SlotScheduleCalculator.GenerateDesiredSlots(
            windowSpecs,
            blockedRanges,
            horizonStartUtc,
            horizonEndUtc,
            settings.SessionDurationMinutes,
            settings.BufferMinutes,
            consultantTimeZone);

        var desiredStarts = desiredSlots.Select(slot => slot.StartUtc).ToHashSet();

        var existingSlots = await dbContext.AvailabilitySlots
            .Where(slot => slot.StartTimeUtc >= horizonStartUtc && slot.StartTimeUtc < horizonEndUtc)
            .ToListAsync(cancellationToken);

        foreach (var existingSlot in existingSlots.Where(slot => !slot.IsBooked && !desiredStarts.Contains(slot.StartTimeUtc)))
        {
            dbContext.AvailabilitySlots.Remove(existingSlot);
        }

        var existingStarts = existingSlots.Select(slot => slot.StartTimeUtc).ToHashSet();

        foreach (var desiredSlot in desiredSlots.Where(slot => !existingStarts.Contains(slot.StartUtc)))
        {
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot
            {
                Id = Guid.NewGuid(),
                StartTimeUtc = desiredSlot.StartUtc,
                EndTimeUtc = desiredSlot.EndUtc,
                IsBooked = false
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Slot generation completed for horizon {HorizonStartUtc:o} to {HorizonEndUtc:o}. Desired={DesiredCount}, Existing={ExistingCount}.",
            horizonStartUtc,
            horizonEndUtc,
            desiredSlots.Count,
            existingSlots.Count);
    }
}
