namespace Shora.Contracts.Booking;

public enum MyBookingsStatusFilter
{
    Upcoming = 0,
    Pending = 1,
    Past = 2
}

public static class MyBookingsQueryLimits
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

public sealed record MyBookingsQuery(
    MyBookingsStatusFilter? Status = null,
    int Page = MyBookingsQueryLimits.DefaultPage,
    int PageSize = MyBookingsQueryLimits.DefaultPageSize);

public sealed record MyBookingCancellationRequestMetadata(
    CancellationRequestStatus Status,
    int ReopenCount,
    DateTime? ClientDecisionSeenAtUtc,
    string? DeclineReason,
    DateTime AutoDeclineAtUtc);

public sealed record MyBookingPaymentSummary(
    decimal Amount,
    string Currency,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions,
    DateTime? ReceiptUploadDeadlineUtc,
    string? LatestReceiptDeclineReason);

public sealed record MyBookingListItem(
    Guid BookingId,
    DateTime SlotStartUtc,
    DateTime SlotEndUtc,
    DeliveryMethod DeliveryMethod,
    string? ContactPhone,
    string Status,
    string? CancellationReasonLabel,
    string? RefundLabel,
    MyBookingCancellationRequestMetadata? CancellationRequest,
    MyBookingPaymentSummary? PaymentSummary,
    string? ReceiptThumbnailUrl,
    string? ConsultantWhatsAppNumber);

public sealed record MyBookingsResponse(
    IReadOnlyList<MyBookingListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
