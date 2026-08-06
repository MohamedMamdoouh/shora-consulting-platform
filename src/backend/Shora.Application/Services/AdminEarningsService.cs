using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Common.Results;
using Shora.Application.Earnings;
using Shora.Contracts.Payments;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class AdminEarningsService(IApplicationDbContext dbContext)
{
    public async Task<Result<AdminEarningsResponse>> GetAsync(
        ValidatedAdminEarningsQuery query,
        CancellationToken cancellationToken = default)
    {
        var revenuePaymentsQuery = ApplyPaymentDateFilter(
            dbContext.Payments
                .AsNoTracking()
                .Where(payment =>
                    payment.Status == PaymentStatus.Approved || payment.Status == PaymentStatus.Refunded),
            query.FromUtc,
            query.ToUtc);

        var grossRevenue = await revenuePaymentsQuery.SumAsync(payment => payment.Amount, cancellationToken);
        var approvedCount = await revenuePaymentsQuery.CountAsync(cancellationToken);

        var refundedPaymentsQuery = ApplyPaymentDateFilter(
            dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.Status == PaymentStatus.Refunded),
            query.FromUtc,
            query.ToUtc);

        var refundedAmount = await refundedPaymentsQuery.SumAsync(payment => payment.Amount, cancellationToken);
        var refundedCount = await refundedPaymentsQuery.CountAsync(cancellationToken);

        var refundDueQuery = ApplyBookingDateFilter(
            dbContext.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.Status == BookingStatus.Cancelled
                    && booking.Payment != null
                    && booking.Payment.Status == PaymentStatus.Approved),
            query.FromUtc,
            query.ToUtc);

        var refundDueCount = await refundDueQuery.CountAsync(cancellationToken);

        return new AdminEarningsResponse(
            grossRevenue,
            refundedAmount,
            grossRevenue - refundedAmount,
            approvedCount,
            refundedCount,
            refundDueCount);
    }

    private static IQueryable<Domain.Entities.Payment> ApplyPaymentDateFilter(
        IQueryable<Domain.Entities.Payment> query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc is not null)
        {
            query = query.Where(payment => payment.Booking.SlotStartUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(payment => payment.Booking.SlotStartUtc < toUtc);
        }

        return query;
    }

    private static IQueryable<Domain.Entities.Booking> ApplyBookingDateFilter(
        IQueryable<Domain.Entities.Booking> query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc is not null)
        {
            query = query.Where(booking => booking.SlotStartUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(booking => booking.SlotStartUtc < toUtc);
        }

        return query;
    }
}
