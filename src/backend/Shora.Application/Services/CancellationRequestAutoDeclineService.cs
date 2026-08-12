using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using DomainCancellationRequestStatus = Shora.Domain.Enums.CancellationRequestStatus;

namespace Shora.Application.Services;

public sealed class CancellationRequestAutoDeclineService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ILogger<CancellationRequestAutoDeclineService> logger)
{
    private const string AutoDeclineReasonNote =
        "تم إغلاق الطلب تلقائيًا لعدم اتخاذ قرار قبل موعد الجلسة.";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var bookingIds = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.CancellationRequested
                && booking.CancellationRequest != null
                && booking.CancellationRequest.Status == DomainCancellationRequestStatus.Pending
                && booking.CancellationRequest.AutoDeclineAtUtc <= now)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (bookingIds.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;

        foreach (var bookingId in bookingIds)
        {
            if (await TryAutoDeclineAsync(bookingId, now, cancellationToken))
            {
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            logger.LogInformation(
                "Cancellation request auto-decline processed {ProcessedCount} of {CandidateCount} bookings.",
                processedCount,
                bookingIds.Count);
        }

        return processedCount;
    }

    private async Task<bool> TryAutoDeclineAsync(
        Guid bookingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await dbContext.Bookings
            .Include(b => b.CancellationRequest)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking?.CancellationRequest is not { Status: DomainCancellationRequestStatus.Pending } cancellationRequest
            || booking.Status != BookingStatus.CancellationRequested
            || cancellationRequest.AutoDeclineAtUtc > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        cancellationRequest.Status = DomainCancellationRequestStatus.AutoDeclined;
        cancellationRequest.ReviewedAtUtc = now;
        cancellationRequest.ReviewedByAdminId = null;
        cancellationRequest.DecisionReasonCode = DecisionReasonCode.Policy;
        cancellationRequest.DecisionReason = AutoDeclineReasonNote;
        cancellationRequest.ClientDecisionSeenAtUtc = null;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Confirmed,
            AuditActor.System,
            BookingStatus.CancellationRequested,
            reason: "Cancellation request auto-declined at deadline");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        EnqueueClientCancellationRequestDeclinedEmail(booking, cancellationRequest, now);

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

    private void EnqueueClientCancellationRequestDeclinedEmail(
        Booking booking,
        CancellationRequest cancellationRequest,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientCancellationRequestDeclinedEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey =
                $"{booking.Id}:{OutboxMessageTypes.ClientCancellationRequestDeclinedEmail}:{cancellationRequest.Id}:auto-decline",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                clientId = booking.ClientId,
                requestId = cancellationRequest.Id,
                reasonCode = CancellationDecisionReasonCode.Policy.ToString(),
                reasonNote = AutoDeclineReasonNote
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }
}
