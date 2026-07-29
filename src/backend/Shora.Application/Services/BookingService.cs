using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Contracts.Booking;
using Shora.Domain.Constants;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Application.Services;

public sealed class BookingService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    ICacheInvalidator cacheInvalidator,
    IOptions<BookingOptions> bookingOptions)
{
    public async Task<Result<ReserveBookingResponse>> ReserveAsync(
        Guid clientId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var deliveryValidation = ValidateDeliveryAndPhone(request);
        if (deliveryValidation.IsFailure)
        {
            return Result<ReserveBookingResponse>.Failure(deliveryValidation.Error!);
        }

        var normalizedPhone = deliveryValidation.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var emailResult = await EnsureEmailVerifiedAsync(clientId, cancellationToken);
        if (emailResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReserveBookingResponse>.Failure(emailResult.Error!);
        }

        var holdCapResult = await EnsureHoldCapAsync(clientId, cancellationToken);
        if (holdCapResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReserveBookingResponse>.Failure(holdCapResult.Error!);
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReserveBookingResponse>.Failure(
                Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured."));
        }

        var slot = await dbContext.AvailabilitySlots
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM AvailabilitySlots WITH (UPDLOCK, ROWLOCK)
                 WHERE Id = {request.AvailabilitySlotId}
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (slot is null || slot.IsBooked)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReserveBookingResponse>.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.SlotUnavailable,
                    "The selected slot is no longer available."));
        }

        var now = dateTimeProvider.UtcNow;
        var bookingId = Guid.NewGuid();
        var receiptUploadDeadlineUtc = now.AddMinutes(settings.ReceiptUploadWindowMinutes);

        slot.IsBooked = true;
        slot.BookingId = bookingId;

        var booking = new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            AvailabilitySlotId = slot.Id,
            SlotStartUtc = slot.StartTimeUtc,
            SlotEndUtc = slot.EndTimeUtc,
            DeliveryMethod = MapDeliveryMethod(request.DeliveryMethod),
            ContactPhone = normalizedPhone,
            ReceiptUploadDeadlineUtc = receiptUploadDeadlineUtc,
            CreatedAt = now
        };

        dbContext.Bookings.Add(booking);

        dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Status = PaymentStatus.AwaitingReceipt,
            Amount = settings.SessionPrice,
            Currency = CurrencyCodes.Egp,
            CreatedAt = now,
            UpdatedAt = now
        });

        var auditResult = transitionHelper.RecordInitialStatus(
            booking,
            BookingStatus.PendingPayment,
            AuditActor.Client,
            clientId,
            "Booking reserved");

        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReserveBookingResponse>.Failure(auditResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return Result<ReserveBookingResponse>.Success(new ReserveBookingResponse(
            bookingId,
            new PaymentInstructionsSnapshot(
                settings.SessionPrice,
                CurrencyCodes.Egp,
                settings.VodafoneCashNumber,
                settings.InstaPayHandle,
                settings.PaymentInstructions,
                receiptUploadDeadlineUtc)));
    }

    public async Task<Result> CancelHoldAsync(
        Guid clientId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(
                Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found."));
        }

        if (booking.ClientId != clientId)
        {
            return Result.Failure(
                Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking."));
        }

        if (booking.Status is not BookingStatus.PendingPayment and not BookingStatus.PendingApproval)
        {
            return Result.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Only unpaid holds can be cancelled."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (booking.AvailabilitySlotId is Guid slotId)
        {
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

        var fromStatus = booking.Status;
        var now = dateTimeProvider.UtcNow;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Cancelled,
            AuditActor.Client,
            fromStatus,
            clientId,
            "Hold cancelled by client");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return transitionResult;
        }

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Void;
            booking.Payment.UpdatedAt = now;
        }

        EnqueueClientBookingCancelledEmail(booking, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cacheInvalidator.InvalidateAvailabilityAsync(cancellationToken);

        return Result.Success();
    }

    private void EnqueueClientBookingCancelledEmail(Booking booking, DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientBookingCancelledEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientBookingCancelledEmail}",
            PayloadJson = JsonSerializer.Serialize(new { bookingId = booking.Id, clientId = booking.ClientId }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private static Result<string?> ValidateDeliveryAndPhone(CreateBookingRequest request)
    {
        if (request.DeliveryMethod == ContractDeliveryMethod.VoiceCall)
        {
            if (string.IsNullOrWhiteSpace(request.ContactPhone))
            {
                return Result<string?>.Failure(
                    Error.Validation(
                        ErrorCodes.Booking.ContactPhoneRequired,
                        "Contact phone is required for voice call delivery."));
            }

            var phoneResult = PhoneNormalizer.NormalizeToE164(request.ContactPhone);
            if (phoneResult.IsFailure)
            {
                return Result<string?>.Failure(phoneResult.Error!);
            }

            return Result<string?>.Success(phoneResult.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ContactPhone))
        {
            var phoneResult = PhoneNormalizer.NormalizeToE164(request.ContactPhone);
            if (phoneResult.IsFailure)
            {
                return Result<string?>.Failure(phoneResult.Error!);
            }

            return Result<string?>.Success(phoneResult.Value);
        }

        return Result<string?>.Success(null);
    }

    private async Task<Result> EnsureEmailVerifiedAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var emailConfirmed = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == clientId)
            .Select(user => user.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        if (!emailConfirmed)
        {
            return Result.Failure(
                Error.Forbidden(
                    ErrorCodes.Booking.EmailNotVerified,
                    "Verify your email before reserving a session."));
        }

        return Result.Success();
    }

    private async Task<Result> EnsureHoldCapAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var pendingPayment = nameof(BookingStatus.PendingPayment);
        var pendingApproval = nameof(BookingStatus.PendingApproval);

        var holdCount = await dbContext.Bookings
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM Bookings WITH (UPDLOCK, HOLDLOCK)
                 WHERE ClientId = {clientId}
                 AND Status IN ({pendingPayment}, {pendingApproval})
                 """)
            .CountAsync(cancellationToken);

        if (holdCount >= bookingOptions.Value.UnconfirmedHoldCap)
        {
            return Result.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.HoldCapExceeded,
                    "You already have the maximum number of unpaid holds."));
        }

        return Result.Success();
    }

    private static Domain.Enums.DeliveryMethod MapDeliveryMethod(ContractDeliveryMethod deliveryMethod) =>
        deliveryMethod switch
        {
            ContractDeliveryMethod.VoiceCall => Domain.Enums.DeliveryMethod.VoiceCall,
            ContractDeliveryMethod.Chat => Domain.Enums.DeliveryMethod.Chat,
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryMethod), deliveryMethod, "Unknown delivery method.")
        };
}
