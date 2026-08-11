using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;
using Shora.Application.Payments;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractPaymentMethod = Shora.Contracts.Payments.PaymentMethod;
using ContractDeclineReasonCode = Shora.Contracts.Payments.ReceiptDeclineReasonCode;

namespace Shora.Application.Services;

public sealed class AdminReceiptReviewService(
    IApplicationDbContext dbContext,
    IFileStorage fileStorage,
    IDateTimeProvider dateTimeProvider,
    BookingTransitionHelper transitionHelper,
    IOptions<StorageOptions> storageOptions,
    ILogger<AdminReceiptReviewService> logger)
{
    public async Task<Result<AdminBookingReceiptsResponse>> GetReceiptsAsync(
        Guid adminId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Payment!)
            .ThenInclude(p => p.Receipts)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        if (booking.Payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.NotFound, "Payment was not found.");
        }

        var payment = booking.Payment;
        var orderedReceipts = payment.Receipts
            .OrderBy(r => r.UploadedAtUtc)
            .ThenBy(r => r.Id)
            .ToList();

        var readUrlValidity = TimeSpan.FromMinutes(storageOptions.Value.ReceiptReadUrlMinutes);
        var mintedAtUtc = dateTimeProvider.UtcNow;
        var items = new List<AdminPaymentReceiptItem>(orderedReceipts.Count);

        for (var index = 0; index < orderedReceipts.Count; index++)
        {
            var receipt = orderedReceipts[index];
            string? readUrl = null;
            DateTime? readUrlExpiresAtUtc = null;

            if (CanMintReadUrl(receipt))
            {
                try
                {
                    readUrl = await fileStorage.GetReadUrlAsync(receipt.BlobPath, readUrlValidity, cancellationToken);
                    readUrlExpiresAtUtc = mintedAtUtc.Add(readUrlValidity);

                    logger.LogInformation(
                        "Receipt read URL minted for booking {BookingId} receipt {ReceiptId} by admin {AdminId}",
                        bookingId,
                        receipt.Id,
                        adminId);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Receipt read URL could not be minted for booking {BookingId} receipt {ReceiptId}",
                        bookingId,
                        receipt.Id);
                }
            }

            items.Add(MapReceipt(receipt, index + 1, readUrl, readUrlExpiresAtUtc));
        }

        return new AdminBookingReceiptsResponse(
            booking.Id,
            payment.Id,
            payment.Status.ToString(),
            MapPaymentMethod(payment.Method),
            payment.Amount,
            payment.Currency,
            items);
    }

    public async Task<Result<AdminReceiptDecisionResponse>> ApproveAsync(
        Guid adminId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        using var _ = PaymentLogScope.Begin(logger, bookingId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loadResult = await LoadPendingReviewContextAsync(bookingId, cancellationToken);
        if (loadResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return loadResult.Error!;
        }

        var (booking, payment, receipt) = loadResult.Value!;
        var now = dateTimeProvider.UtcNow;

        if (!CanMintReadUrl(receipt))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Payment.ReceiptNotReviewable,
                "Receipt approval requires a finalized clean receipt.");
        }

        receipt.ReviewStatus = ReceiptReviewStatus.Approved;
        receipt.ReviewedByAdminId = adminId;
        receipt.ReviewedAtUtc = now;

        payment.Status = PaymentStatus.Approved;
        payment.UpdatedAt = now;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.Confirmed,
            AuditActor.Admin,
            BookingStatus.PendingApproval,
            adminId,
            "Receipt approved");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return transitionResult.Error!;
        }

        EnqueueApprovalEmails(booking, payment, receipt, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Payment.InvalidStatus,
                "The booking was updated by another action. Please refresh and try again.");
        }

        logger.LogInformation(
            "Receipt approved for booking {BookingId} payment {PaymentId} receipt {ReceiptId} by admin {AdminId}",
            booking.Id,
            payment.Id,
            receipt.Id,
            adminId);

        return new AdminReceiptDecisionResponse(
            booking.Id,
            nameof(BookingStatus.Confirmed),
            nameof(PaymentStatus.Approved),
            receipt.Id,
            nameof(ReceiptReviewStatus.Approved),
            null);
    }

    public async Task<Result<AdminReceiptDecisionResponse>> DeclineAsync(
        Guid adminId,
        Guid bookingId,
        DeclineReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ReasonCode))
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidDeclineReason,
                "The decline reason code is invalid.");
        }

        var domainReasonCode = MapDeclineReasonCode(request.ReasonCode);

        using var _ = PaymentLogScope.Begin(logger, bookingId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loadResult = await LoadPendingReviewContextAsync(bookingId, cancellationToken);
        if (loadResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return loadResult.Error!;
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured.");
        }

        var (booking, payment, receipt) = loadResult.Value!;
        var now = dateTimeProvider.UtcNow;
        var newDeadline = now.AddMinutes(settings.ReceiptUploadWindowMinutes);

        receipt.ReviewStatus = ReceiptReviewStatus.Declined;
        receipt.ReviewedByAdminId = adminId;
        receipt.ReviewedAtUtc = now;
        receipt.DeclineReasonCode = domainReasonCode;
        receipt.DeclineReason = NormalizeDeclineReasonNote(request.ReasonNote);

        payment.Status = PaymentStatus.AwaitingReceipt;
        payment.UpdatedAt = now;

        var transitionResult = transitionHelper.ApplyTransition(
            booking,
            BookingStatus.PendingPayment,
            AuditActor.Admin,
            BookingStatus.PendingApproval,
            adminId,
            "Receipt declined");

        if (transitionResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return transitionResult.Error!;
        }

        booking.ReceiptUploadDeadlineUtc = newDeadline;

        EnqueueDeclineEmail(booking, payment, receipt, request.ReasonCode, request.ReasonNote, newDeadline, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Payment.InvalidStatus,
                "The booking was updated by another action. Please refresh and try again.");
        }

        logger.LogInformation(
            "Receipt declined for booking {BookingId} payment {PaymentId} receipt {ReceiptId} by admin {AdminId}",
            booking.Id,
            payment.Id,
            receipt.Id,
            adminId);

        return new AdminReceiptDecisionResponse(
            booking.Id,
            nameof(BookingStatus.PendingPayment),
            nameof(PaymentStatus.AwaitingReceipt),
            receipt.Id,
            nameof(ReceiptReviewStatus.Declined),
            newDeadline);
    }

    private async Task<Result<(Booking Booking, Payment Payment, PaymentReceipt Receipt)>> LoadPendingReviewContextAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "Bookings" WHERE "Id" = {bookingId} FOR UPDATE
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        if (booking.Status != BookingStatus.PendingApproval)
        {
            return Error.Conflict(
                ErrorCodes.Booking.InvalidStatus,
                "Receipt review is only allowed while the booking is pending approval.");
        }

        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);

        if (payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.NotFound, "Payment was not found.");
        }

        if (payment.Status != PaymentStatus.UnderReview)
        {
            return Error.Conflict(
                ErrorCodes.Payment.InvalidStatus,
                "No receipt is currently under review for this booking.");
        }

        var receipt = await dbContext.PaymentReceipts
            .Where(r => r.PaymentId == payment.Id && r.ReviewStatus == ReceiptReviewStatus.Pending)
            .OrderByDescending(r => r.UploadedAtUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null)
        {
            return Error.Conflict(
                ErrorCodes.Payment.NoPendingReceipt,
                "No pending receipt was found for this booking.");
        }

        return (booking, payment, receipt);
    }

    private void EnqueueApprovalEmails(
        Booking booking,
        Payment payment,
        PaymentReceipt receipt,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientBookingConfirmedEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientBookingConfirmedEmail}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                paymentId = payment.Id,
                receiptId = receipt.Id,
                clientId = booking.ClientId
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.AdminNewBookingEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.AdminNewBookingEmail}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                paymentId = payment.Id,
                receiptId = receipt.Id,
                clientId = booking.ClientId
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private void EnqueueDeclineEmail(
        Booking booking,
        Payment payment,
        PaymentReceipt receipt,
        ContractDeclineReasonCode reasonCode,
        string? reasonNote,
        DateTime newDeadline,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientReceiptDeclinedEmail,
            AggregateType = nameof(Booking),
            AggregateId = booking.Id,
            IdempotencyKey = $"{booking.Id}:{OutboxMessageTypes.ClientReceiptDeclinedEmail}:{receipt.Id}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                paymentId = payment.Id,
                receiptId = receipt.Id,
                clientId = booking.ClientId,
                reasonCode = reasonCode.ToString(),
                reasonNote,
                receiptUploadDeadlineUtc = newDeadline
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private static bool CanMintReadUrl(PaymentReceipt receipt) =>
        receipt.BlobState == BlobState.Finalized
        && receipt.MalwareScanStatus == MalwareScanStatus.Clean;

    private static AdminPaymentReceiptItem MapReceipt(
        PaymentReceipt receipt,
        int attemptNumber,
        string? readUrl,
        DateTime? readUrlExpiresAtUtc) =>
        new(
            receipt.Id,
            attemptNumber,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.SizeBytes,
            receipt.SenderReference,
            receipt.UploadedAtUtc,
            receipt.BlobState.ToString(),
            receipt.MalwareScanStatus.ToString(),
            receipt.ReviewStatus.ToString(),
            receipt.DeclineReasonCode?.ToString(),
            receipt.DeclineReason,
            receipt.ReviewedAtUtc,
            ReceiptReviewWarningMapper.ToWarningCodes(receipt.ReviewWarnings),
            readUrl,
            readUrlExpiresAtUtc);

    private static ContractPaymentMethod? MapPaymentMethod(Domain.Enums.PaymentMethod? method) =>
        method switch
        {
            Domain.Enums.PaymentMethod.VodafoneCash => ContractPaymentMethod.VodafoneCash,
            Domain.Enums.PaymentMethod.InstaPay => ContractPaymentMethod.InstaPay,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown payment method.")
        };

    private static DeclineReasonCode MapDeclineReasonCode(ContractDeclineReasonCode reasonCode) =>
        reasonCode switch
        {
            ContractDeclineReasonCode.UnreadableImage => DeclineReasonCode.UnreadableImage,
            ContractDeclineReasonCode.AmountMismatch => DeclineReasonCode.AmountMismatch,
            ContractDeclineReasonCode.DuplicateReceipt => DeclineReasonCode.DuplicateReceipt,
            ContractDeclineReasonCode.UnverifiableTransfer => DeclineReasonCode.UnverifiableTransfer,
            ContractDeclineReasonCode.Other => DeclineReasonCode.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(reasonCode), reasonCode, "Unknown decline reason code.")
        };

    private static string? NormalizeDeclineReasonNote(string? reasonNote)
    {
        if (string.IsNullOrWhiteSpace(reasonNote))
        {
            return null;
        }

        var trimmed = reasonNote.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
