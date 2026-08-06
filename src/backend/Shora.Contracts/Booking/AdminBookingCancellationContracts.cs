namespace Shora.Contracts.Booking;

public enum CancellationDecisionReasonCode
{
    TimingConflict = 0,
    InsufficientReason = 1,
    Policy = 2,
    Other = 3
}

public sealed record DeclineCancellationRequestBody(
    CancellationDecisionReasonCode ReasonCode,
    string? ReasonNote);

public sealed record AdminBookingCancellationResponse(
    Guid BookingId,
    string BookingStatus,
    string? PaymentStatus,
    bool RefundDue);
