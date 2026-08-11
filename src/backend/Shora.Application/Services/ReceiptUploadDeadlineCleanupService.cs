using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class ReceiptUploadDeadlineCleanupService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ICacheInvalidator cacheInvalidator,
    ILogger<ReceiptUploadDeadlineCleanupService> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var expiredBookingIds = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.PendingPayment
                && booking.ReceiptUploadDeadlineUtc != null
                && booking.ReceiptUploadDeadlineUtc <= now)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (expiredBookingIds.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var bookingId in expiredBookingIds)
        {
            if (await TryCancelExpiredHoldAsync(bookingId, now, cancellationToken))
            {
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);
        }

        logger.LogInformation(
            "Receipt upload deadline cleanup processed {ProcessedCount} of {CandidateCount} expired holds.",
            processedCount,
            expiredBookingIds.Count);

        return processedCount;
    }

    private async Task<bool> TryCancelExpiredHoldAsync(
        Guid bookingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await dbContext.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null
            || booking.Status != BookingStatus.PendingPayment
            || booking.ReceiptUploadDeadlineUtc is null
            || booking.ReceiptUploadDeadlineUtc > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (booking.AvailabilitySlotId is Guid slotId)
        {
            var slot = await dbContext.AvailabilitySlots
                .FromSqlInterpolated(
                    $"""
                     SELECT * FROM "AvailabilitySlots" WHERE "Id" = {slotId} FOR UPDATE
                     """)
                .FirstOrDefaultAsync(cancellationToken);

            if (slot is not null)
            {
                slot.IsBooked = false;
                slot.BookingId = null;
            }

            booking.AvailabilitySlotId = null;
        }

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            AuditActor.System,
            BookingStatus.PendingPayment,
            reason: "Receipt upload deadline expired");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Void;
            booking.Payment.UpdatedAt = now;
        }

        EnqueueClientBookingCancelledEmail(booking, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private void EnqueueClientBookingCancelledEmail(Booking booking, DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientBookingCancelledEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientBookingCancelledEmail}:deadline",
            PayloadJson = JsonSerializer.Serialize(new { bookingId = booking.Id, clientId = booking.ClientId }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }
}
