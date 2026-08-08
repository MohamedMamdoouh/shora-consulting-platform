using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class BookingAutoCompleteService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ICacheInvalidator cacheInvalidator,
    ILogger<BookingAutoCompleteService> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var bookingIds = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Confirmed
                && booking.SlotEndUtc <= now)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (bookingIds.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var bookingId in bookingIds)
        {
            if (await TryCompleteAsync(bookingId, now, cancellationToken))
            {
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

            logger.LogInformation(
                "Booking auto-complete processed {ProcessedCount} of {CandidateCount} bookings.",
                processedCount,
                bookingIds.Count);
        }

        return processedCount;
    }

    private async Task<bool> TryCompleteAsync(
        Guid bookingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null
            || booking.Status != BookingStatus.Confirmed
            || booking.SlotEndUtc > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await ReleaseSlotAsync(booking, cancellationToken);

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Completed,
            AuditActor.System,
            BookingStatus.Confirmed,
            reason: "Session ended");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        return true;
    }

    private async Task ReleaseSlotAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.AvailabilitySlotId is not Guid slotId)
        {
            return;
        }

        var slot = await dbContext.AvailabilitySlots
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM AvailabilitySlots WITH (UPDLOCK, ROWLOCK)
                 WHERE Id = {slotId}
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (slot is not null)
        {
            slot.IsBooked = false;
            slot.BookingId = null;
        }

        booking.AvailabilitySlotId = null;
    }
}
