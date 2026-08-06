using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractCancellationDecisionReasonCode = Shora.Contracts.Booking.CancellationDecisionReasonCode;

namespace Shora.Application.Services;

public sealed class AdminBookingCancellationService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ICacheInvalidator cacheInvalidator)
{
    public async Task<Result<AdminBookingCancellationResponse>> CancelAsync(
        Guid adminId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loadResult = await LoadBookingAsync(bookingId, includeCancellationRequest: true, cancellationToken);
        if (loadResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return loadResult.Error!;
        }

        var booking = loadResult.Value!;
        var now = dateTimeProvider.UtcNow;

        if (!IsDirectCancelAllowed(booking.Status))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "This booking cannot be cancelled in its current status.");
        }

        if (RequiresSessionNotStartedGuard(booking.Status) && now >= booking.SlotStartUtc)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "The booking can no longer be cancelled because the session has started.");
        }

        if (booking.Status == BookingStatus.CancellationRequested
            && booking.CancellationRequest is { Status: Domain.Enums.CancellationRequestStatus.Pending } request)
        {
            request.Status = Domain.Enums.CancellationRequestStatus.Approved;
            request.ReviewedByAdminId = adminId;
            request.ReviewedAtUtc = now;
        }

        var cancelResult = await CancelBookingCoreAsync(
            booking,
            adminId,
            AuditActor.Admin,
            booking.Status,
            "Cancelled by admin",
            now,
            cancellationToken);

        if (cancelResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return cancelResult.Error!;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "The booking was updated by another action. Please refresh and try again.");
        }

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return MapResponse(booking);
    }

    public async Task<Result<AdminBookingCancellationResponse>> ApproveCancellationRequestAsync(
        Guid adminId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loadResult = await LoadBookingAsync(bookingId, includeCancellationRequest: true, cancellationToken);
        if (loadResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return loadResult.Error!;
        }

        var booking = loadResult.Value!;
        var now = dateTimeProvider.UtcNow;

        if (booking.Status != BookingStatus.CancellationRequested)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "Cancellation requests can only be approved while the booking is awaiting a decision.");
        }

        if (booking.CancellationRequest is not { Status: Domain.Enums.CancellationRequestStatus.Pending } request)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "No pending cancellation request was found for this booking.");
        }

        if (now >= booking.SlotStartUtc)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "The booking can no longer be cancelled because the session has started.");
        }

        request.Status = Domain.Enums.CancellationRequestStatus.Approved;
        request.ReviewedByAdminId = adminId;
        request.ReviewedAtUtc = now;

        var cancelResult = await CancelBookingCoreAsync(
            booking,
            adminId,
            AuditActor.Admin,
            BookingStatus.CancellationRequested,
            "Cancellation request approved by admin",
            now,
            cancellationToken);

        if (cancelResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return cancelResult.Error!;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "The booking was updated by another action. Please refresh and try again.");
        }

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return MapResponse(booking);
    }

    public async Task<Result<AdminBookingCancellationResponse>> DeclineCancellationRequestAsync(
        Guid adminId,
        Guid bookingId,
        DeclineCancellationRequestBody request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ReasonCode))
        {
            return Error.Validation(
                ErrorCodes.Cancellation.InvalidDecisionReason,
                "The decline reason code is invalid.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loadResult = await LoadBookingAsync(bookingId, includeCancellationRequest: true, cancellationToken);
        if (loadResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return loadResult.Error!;
        }

        var booking = loadResult.Value!;
        var now = dateTimeProvider.UtcNow;

        if (booking.Status != BookingStatus.CancellationRequested)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "Cancellation requests can only be declined while the booking is awaiting a decision.");
        }

        if (booking.CancellationRequest is not { Status: Domain.Enums.CancellationRequestStatus.Pending } cancellationRequest)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "No pending cancellation request was found for this booking.");
        }

        cancellationRequest.Status = Domain.Enums.CancellationRequestStatus.Declined;
        cancellationRequest.ReviewedByAdminId = adminId;
        cancellationRequest.ReviewedAtUtc = now;
        cancellationRequest.DecisionReasonCode = MapDecisionReasonCode(request.ReasonCode);
        cancellationRequest.DecisionReason = NormalizeDecisionReasonNote(request.ReasonNote);
        cancellationRequest.ClientDecisionSeenAtUtc = null;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Confirmed,
            AuditActor.Admin,
            BookingStatus.CancellationRequested,
            adminId,
            "Cancellation request declined by admin");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return transitionResult.Error!;
        }

        EnqueueClientCancellationRequestDeclinedEmail(
            booking,
            cancellationRequest,
            request.ReasonCode,
            request.ReasonNote,
            now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "The booking was updated by another action. Please refresh and try again.");
        }

        return MapResponse(booking);
    }

    private async Task<Result> CancelBookingCoreAsync(
        Booking booking,
        Guid adminId,
        AuditActor actor,
        BookingStatus expectedFromStatus,
        string auditReason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await ReleaseSlotAsync(booking, cancellationToken);

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            actor,
            expectedFromStatus,
            adminId,
            auditReason);

        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        ApplyPaymentStatusOnCancel(booking, now);
        EnqueueClientBookingCancelledEmail(booking, now);

        return Result.Success();
    }

    private async Task<Result<Booking>> LoadBookingAsync(
        Guid bookingId,
        bool includeCancellationRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Booking> query = dbContext.Bookings.Include(b => b.Payment);

        if (includeCancellationRequest)
        {
            query = query.Include(b => b.CancellationRequest);
        }

        var booking = await query.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        return booking;
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

    private static bool IsDirectCancelAllowed(BookingStatus status) =>
        status is BookingStatus.PendingPayment
            or BookingStatus.PendingApproval
            or BookingStatus.Confirmed
            or BookingStatus.CancellationRequested;

    private static bool RequiresSessionNotStartedGuard(BookingStatus status) =>
        status is BookingStatus.Confirmed or BookingStatus.CancellationRequested;

    private static void ApplyPaymentStatusOnCancel(Booking booking, DateTime now)
    {
        if (booking.Payment is null)
        {
            return;
        }

        if (booking.Payment.Status == PaymentStatus.Approved)
        {
            booking.Payment.UpdatedAt = now;
            return;
        }

        booking.Payment.Status = PaymentStatus.Void;
        booking.Payment.UpdatedAt = now;
    }

    private static AdminBookingCancellationResponse MapResponse(Booking booking)
    {
        var payment = booking.Payment;
        var refundDue = booking.Status == BookingStatus.Cancelled
            && payment?.Status == PaymentStatus.Approved;

        return new AdminBookingCancellationResponse(
            booking.Id,
            booking.Status.ToString(),
            payment?.Status.ToString(),
            refundDue);
    }

    private static DecisionReasonCode MapDecisionReasonCode(ContractCancellationDecisionReasonCode reasonCode) =>
        reasonCode switch
        {
            ContractCancellationDecisionReasonCode.TimingConflict => DecisionReasonCode.TimingConflict,
            ContractCancellationDecisionReasonCode.InsufficientReason => DecisionReasonCode.InsufficientReason,
            ContractCancellationDecisionReasonCode.Policy => DecisionReasonCode.Policy,
            ContractCancellationDecisionReasonCode.Other => DecisionReasonCode.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(reasonCode), reasonCode, "Unknown decision reason code.")
        };

    private static string? NormalizeDecisionReasonNote(string? reasonNote)
    {
        if (string.IsNullOrWhiteSpace(reasonNote))
        {
            return null;
        }

        var trimmed = reasonNote.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private void EnqueueClientBookingCancelledEmail(Booking booking, DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientBookingCancelledEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientBookingCancelledEmail}:{now.Ticks}",
            PayloadJson = JsonSerializer.Serialize(new { bookingId = booking.Id, clientId = booking.ClientId }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private void EnqueueClientCancellationRequestDeclinedEmail(
        Booking booking,
        CancellationRequest cancellationRequest,
        ContractCancellationDecisionReasonCode reasonCode,
        string? reasonNote,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientCancellationRequestDeclinedEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey =
                $"{booking.Id}:{OutboxMessageTypes.ClientCancellationRequestDeclinedEmail}:{cancellationRequest.Id}:{now.Ticks}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                clientId = booking.ClientId,
                requestId = cancellationRequest.Id,
                reasonCode = reasonCode.ToString(),
                reasonNote
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }
}
