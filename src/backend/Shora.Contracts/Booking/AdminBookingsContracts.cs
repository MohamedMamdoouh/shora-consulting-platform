namespace Shora.Contracts.Booking;

public enum AdminBookingStatusFilter
{
    PendingPayment = 0,
    PendingApproval = 1,
    Confirmed = 2,
    CancellationRequested = 3,
    Completed = 4,
    Cancelled = 5
}

public static class AdminBookingsQueryLimits
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

public sealed record AdminBookingsQuery(
    AdminBookingStatusFilter? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = AdminBookingsQueryLimits.DefaultPage,
    int PageSize = AdminBookingsQueryLimits.DefaultPageSize);

public sealed record AdminBookingCancellationRequestSummary(
    CancellationRequestStatus Status,
    string? ClientReason,
    DateTime RequestedAtUtc,
    DateTime AutoDeclineAtUtc);

public sealed record AdminBookingListItem(
    Guid BookingId,
    string ClientDisplayName,
    DeliveryMethod DeliveryMethod,
    string? ContactPhone,
    DateTime SlotStartUtc,
    DateTime SlotEndUtc,
    string Status,
    string? CancellationReasonLabel,
    Guid? PaymentId,
    string? PaymentStatus,
    bool RefundDue,
    AdminBookingCancellationRequestSummary? CancellationRequest = null);

public sealed record AdminBookingsResponse(
    IReadOnlyList<AdminBookingListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
