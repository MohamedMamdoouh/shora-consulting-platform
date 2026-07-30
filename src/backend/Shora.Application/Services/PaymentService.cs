using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Payments;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class PaymentService(IApplicationDbContext dbContext)
{
    public async Task<Result<PaymentInstructionsResponse>> GetPaymentInstructionsAsync(
        Guid clientId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Result<PaymentInstructionsResponse>.Failure(
                Error.NotFound(ErrorCodes.Booking.NotFound, "Booking was not found."));
        }

        if (booking.ClientId != clientId)
        {
            return Result<PaymentInstructionsResponse>.Failure(
                Error.Forbidden(ErrorCodes.Booking.Forbidden, "You do not have access to this booking."));
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return Result<PaymentInstructionsResponse>.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Payment instructions are only available for bookings awaiting receipt upload."));
        }

        if (booking.Payment is null || booking.ReceiptUploadDeadlineUtc is not { } deadline)
        {
            return Result<PaymentInstructionsResponse>.Failure(
                Error.Conflict(
                    ErrorCodes.Booking.InvalidStatus,
                    "Payment instructions are only available while the upload window is open."));
        }

        var settings = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

        if (settings is null)
        {
            return Result<PaymentInstructionsResponse>.Failure(
                Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured."));
        }

        return Result<PaymentInstructionsResponse>.Success(new PaymentInstructionsResponse(
            booking.Payment.Amount,
            booking.Payment.Currency,
            settings.VodafoneCashNumber,
            settings.InstaPayHandle,
            settings.PaymentInstructions,
            deadline));
    }
}
