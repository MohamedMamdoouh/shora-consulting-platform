using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class AdminRefundService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AdminRefundService> logger)
{
    public async Task<Result<PaymentRefundResponse>> RecordRefundAsync(
        Guid adminId,
        Guid paymentId,
        RecordRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var reference = NormalizeReference(request.Reference);
        if (reference is null)
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidRefundReference,
                "Refund reference is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payment = await dbContext.Payments
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "Payments" WHERE "Id" = {paymentId} FOR UPDATE
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Payment.NotFound, "Payment was not found.");
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == payment.BookingId, cancellationToken);

        if (booking is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        using var _ = PaymentLogScope.Begin(logger, booking.Id, payment.Id);

        if (payment.Status == PaymentStatus.Refunded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MapResponse(payment);
        }

        if (booking.Status != BookingStatus.Cancelled || payment.Status != PaymentStatus.Approved)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Payment.RefundNotDue,
                "A refund can only be recorded for a cancelled booking with an approved payment.");
        }

        var now = dateTimeProvider.UtcNow;

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAtUtc = now;
        payment.RefundReference = reference;
        payment.RefundedByAdminId = adminId;
        payment.UpdatedAt = now;

        EnqueueClientRefundConfirmationEmail(payment, booking.ClientId, request.Note, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Refund recorded for booking {BookingId} payment {PaymentId} by admin {AdminId}",
            booking.Id,
            payment.Id,
            adminId);

        return MapResponse(payment);
    }

    public async Task<Result<PaymentRefundResponse>> RevokeRefundAsync(
        Guid adminId,
        Guid paymentId,
        RevokeRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeRevocationReason(request.CorrectionReason);
        if (reason is null)
        {
            return Error.Validation(
                ErrorCodes.Payment.InvalidRefundRevocationReason,
                "Correction reason is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payment = await dbContext.Payments
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "Payments" WHERE "Id" = {paymentId} FOR UPDATE
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Payment.NotFound, "Payment was not found.");
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == payment.BookingId, cancellationToken);

        if (booking is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found.");
        }

        using var _ = PaymentLogScope.Begin(logger, booking.Id, payment.Id);

        if (payment.Status != PaymentStatus.Refunded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Conflict(
                ErrorCodes.Payment.NotRefunded,
                "Only a recorded refund can be revoked.");
        }

        var now = dateTimeProvider.UtcNow;
        var previousReference = payment.RefundReference;

        payment.Status = PaymentStatus.Approved;
        payment.RefundedAtUtc = null;
        payment.RefundReference = null;
        payment.RefundedByAdminId = null;
        payment.RefundRevokedAtUtc = now;
        payment.RefundRevokedByAdminId = adminId;
        payment.RefundRevocationReason = reason;
        payment.UpdatedAt = now;

        EnqueueAdminRefundRevocationEmail(payment, previousReference, reason, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Refund revoked for booking {BookingId} payment {PaymentId} by admin {AdminId}",
            booking.Id,
            payment.Id,
            adminId);

        return MapResponse(payment);
    }

    private void EnqueueClientRefundConfirmationEmail(
        Payment payment,
        Guid clientId,
        string? note,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.ClientRefundConfirmationEmail,
            AggregateType = nameof(Payment),
            AggregateId = payment.Id,
            IdempotencyKey = $"{payment.Id}:{OutboxMessageTypes.ClientRefundConfirmationEmail}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                paymentId = payment.Id,
                bookingId = payment.BookingId,
                clientId,
                reference = payment.RefundReference,
                note,
                amount = payment.Amount,
                currency = payment.Currency
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private void EnqueueAdminRefundRevocationEmail(
        Payment payment,
        string? previousReference,
        string reason,
        DateTime now)
    {
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.AdminRefundRevocationEmail,
            AggregateType = nameof(Payment),
            AggregateId = payment.Id,
            IdempotencyKey = $"{payment.Id}:{OutboxMessageTypes.AdminRefundRevocationEmail}:{now.Ticks}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                paymentId = payment.Id,
                bookingId = payment.BookingId,
                previousReference,
                correctionReason = reason,
                revokedAtUtc = now
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            Status = OutboxMessageStatus.Pending
        });
    }

    private static PaymentRefundResponse MapResponse(Payment payment) =>
        new(
            payment.Id,
            payment.BookingId,
            payment.Status.ToString(),
            payment.RefundReference,
            payment.RefundedAtUtc,
            payment.RefundRevokedAtUtc,
            payment.RefundRevocationReason);

    private static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private static string? NormalizeRevocationReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }
}
