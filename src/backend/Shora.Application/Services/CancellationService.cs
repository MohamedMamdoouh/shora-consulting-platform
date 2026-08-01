using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;

namespace Shora.Application.Services;

public sealed class CancellationService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper)
{
    public async Task<Result<CancellationRequestResponse>> RequestCancellationAsync(
        Guid clientId,
        Guid bookingId,
        CancellationRequestBody body,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.CancellationRequest)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        if (booking.ClientId != clientId)
        {
            return Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking.");
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured.");
        }

        var now = dateTimeProvider.UtcNow;
        var autoDeclineAtUtc = booking.SlotStartUtc.AddHours(-settings.CancellationRequestAutoDeclineHours);

        if (now >= autoDeclineAtUtc)
        {
            return Error.Conflict(
                ErrorCodes.Cancellation.TooLate,
                "Too late to request online — contact the consultant on WhatsApp.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        CancellationRequest request;
        string auditReason;

        if (booking.CancellationRequest is null)
        {
            if (booking.Status != BookingStatus.Confirmed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Cancellation requests are only allowed for confirmed bookings.");
            }

            request = new CancellationRequest
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                RequestedByClientId = clientId,
                RequestedAtUtc = now,
                ClientReason = body.Reason,
                AutoDeclineAtUtc = autoDeclineAtUtc,
                Status = Domain.Enums.CancellationRequestStatus.Pending,
                ReopenCount = 0
            };

            dbContext.CancellationRequests.Add(request);
            auditReason = "Cancellation requested by client";

            var transitionResult = transitionHelper.ApplyTransition(
                booking,
                BookingStatus.CancellationRequested,
                AuditActor.Client,
                BookingStatus.Confirmed,
                clientId,
                auditReason);

            if (transitionResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return transitionResult.Error!;
            }
        }
        else
        {
            request = booking.CancellationRequest;

            if (booking.Status == BookingStatus.CancellationRequested
                && request.Status == Domain.Enums.CancellationRequestStatus.Pending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "A cancellation request is already pending.");
            }

            if (request.Status == Domain.Enums.CancellationRequestStatus.AutoDeclined)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Cancellation.ReopenExhausted,
                    "Further online cancellation requests are not allowed for this booking.");
            }

            if (request.Status != Domain.Enums.CancellationRequestStatus.Declined)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Cancellation requests are only allowed for confirmed bookings.");
            }

            if (request.ReopenCount >= 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Cancellation.ReopenExhausted,
                    "You have already used your one allowed reopen for this booking.");
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Cancellation requests are only allowed for confirmed bookings.");
            }

            request.Status = Domain.Enums.CancellationRequestStatus.Pending;
            request.ReopenCount += 1;
            request.RequestedAtUtc = now;
            request.ClientReason = body.Reason;
            request.AutoDeclineAtUtc = autoDeclineAtUtc;
            request.ClientDecisionSeenAtUtc = null;
            request.DecisionReasonCode = null;
            request.DecisionReason = null;
            request.ReviewedByAdminId = null;
            request.ReviewedAtUtc = null;
            auditReason = "Cancellation request reopened by client";

            var transitionResult = transitionHelper.ApplyTransition(
                booking,
                BookingStatus.CancellationRequested,
                AuditActor.Client,
                BookingStatus.Confirmed,
                clientId,
                auditReason);

            if (transitionResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return transitionResult.Error!;
            }
        }

        EnqueueAdminNewCancellationRequestEmail(booking, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancellationRequestResponse(
            request.Id,
            MapStatus(request.Status),
            request.AutoDeclineAtUtc,
            booking.Status.ToString());
    }

    public async Task<Result> MarkDecisionSeenAsync(
        Guid clientId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.CancellationRequest)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        if (booking.ClientId != clientId)
        {
            return Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking.");
        }

        if (booking.CancellationRequest is not { } request)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "No cancellation request exists for this booking.");
        }

        if (request.Status is not Domain.Enums.CancellationRequestStatus.Declined
            and not Domain.Enums.CancellationRequestStatus.AutoDeclined)
        {
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "Only declined cancellation decisions can be acknowledged.");
        }

        request.ClientDecisionSeenAtUtc = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private void EnqueueAdminNewCancellationRequestEmail(Booking booking, DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.AdminNewCancellationRequestEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.AdminNewCancellationRequestEmail}:{now.Ticks}",
            PayloadJson = JsonSerializer.Serialize(new { bookingId = booking.Id, clientId = booking.ClientId }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private static ContractCancellationRequestStatus MapStatus(Domain.Enums.CancellationRequestStatus status) =>
        (ContractCancellationRequestStatus)(int)status;
}
