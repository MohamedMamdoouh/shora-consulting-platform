using Microsoft.EntityFrameworkCore;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common.Results;
using Shora.Contracts.Booking;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using ContractCancellationRequestStatus = Shora.Contracts.Booking.CancellationRequestStatus;
using ContractDeliveryMethod = Shora.Contracts.Booking.DeliveryMethod;

namespace Shora.Application.Services;

public sealed class AdminBookingListService(IApplicationDbContext dbContext)
{
    public async Task<Result<AdminBookingsResponse>> ListAsync(
        ValidatedAdminBookingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var bookingsQuery = dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Client)
            .Include(booking => booking.Payment)
            .Include(booking => booking.CancellationRequest)
            .AsQueryable();

        bookingsQuery = ApplyStatusFilter(bookingsQuery, query.Status);
        bookingsQuery = ApplyDateRangeFilter(bookingsQuery, query.FromUtc, query.ToUtc);

        var totalCount = await bookingsQuery.CountAsync(cancellationToken);

        var bookings = await bookingsQuery
            .OrderByDescending(booking => booking.SlotStartUtc)
            .ThenByDescending(booking => booking.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var cancelledBookingIds = bookings
            .Where(booking => booking.Status == BookingStatus.Cancelled)
            .Select(booking => booking.Id)
            .ToList();

        var cancelAudits = await LoadLatestCancelAuditsAsync(cancelledBookingIds, cancellationToken);

        var items = bookings
            .Select(booking =>
            {
                cancelAudits.TryGetValue(booking.Id, out var cancelAudit);
                return MapListItem(booking, cancelAudit);
            })
            .ToList();

        return new AdminBookingsResponse(items, query.Page, query.PageSize, totalCount);
    }

    private static AdminBookingListItem MapListItem(Booking booking, BookingStatusAudit? cancelAudit)
    {
        var payment = booking.Payment;
        var refundDue = booking.Status == BookingStatus.Cancelled
            && payment?.Status == PaymentStatus.Approved;

        return new AdminBookingListItem(
            booking.Id,
            booking.Client.DisplayName,
            MapDeliveryMethod(booking.DeliveryMethod),
            booking.ContactPhone,
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            booking.Status.ToString(),
            MyBookingLabelMapper.MapCancellationReasonLabel(cancelAudit),
            payment?.Id,
            payment?.Status.ToString(),
            refundDue,
            MapCancellationRequest(booking.CancellationRequest));
    }

    private static AdminBookingCancellationRequestSummary? MapCancellationRequest(
        CancellationRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return new AdminBookingCancellationRequestSummary(
            MapCancellationRequestStatus(request.Status),
            request.ClientReason,
            request.RequestedAtUtc,
            request.AutoDeclineAtUtc);
    }

    private static ContractCancellationRequestStatus MapCancellationRequestStatus(
        Domain.Enums.CancellationRequestStatus status) =>
        (ContractCancellationRequestStatus)(int)status;

    private static ContractDeliveryMethod MapDeliveryMethod(Domain.Enums.DeliveryMethod deliveryMethod) =>
        deliveryMethod switch
        {
            Domain.Enums.DeliveryMethod.VoiceCall => ContractDeliveryMethod.VoiceCall,
            Domain.Enums.DeliveryMethod.Chat => ContractDeliveryMethod.Chat,
            _ => throw new ArgumentOutOfRangeException(nameof(deliveryMethod), deliveryMethod, "Unknown delivery method.")
        };

    private static IQueryable<Booking> ApplyStatusFilter(
        IQueryable<Booking> query,
        AdminBookingStatusFilter? statusFilter)
    {
        if (statusFilter is null)
        {
            return query;
        }

        var status = MapStatusFilter(statusFilter.Value);
        return query.Where(booking => booking.Status == status);
    }

    private static IQueryable<Booking> ApplyDateRangeFilter(
        IQueryable<Booking> query,
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

    private static BookingStatus MapStatusFilter(AdminBookingStatusFilter statusFilter) =>
        statusFilter switch
        {
            AdminBookingStatusFilter.PendingPayment => BookingStatus.PendingPayment,
            AdminBookingStatusFilter.PendingApproval => BookingStatus.PendingApproval,
            AdminBookingStatusFilter.Confirmed => BookingStatus.Confirmed,
            AdminBookingStatusFilter.CancellationRequested => BookingStatus.CancellationRequested,
            AdminBookingStatusFilter.Completed => BookingStatus.Completed,
            AdminBookingStatusFilter.Cancelled => BookingStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(statusFilter), statusFilter, "Unknown status filter.")
        };

    private async Task<IReadOnlyDictionary<Guid, BookingStatusAudit>> LoadLatestCancelAuditsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0)
        {
            return new Dictionary<Guid, BookingStatusAudit>();
        }

        var audits = await dbContext.BookingStatusAudits
            .AsNoTracking()
            .Where(audit => bookingIds.Contains(audit.BookingId) && audit.ToStatus == BookingStatus.Cancelled)
            .OrderByDescending(audit => audit.AtUtc)
            .ToListAsync(cancellationToken);

        return audits
            .GroupBy(audit => audit.BookingId)
            .ToDictionary(group => group.Key, group => group.First());
    }
}
