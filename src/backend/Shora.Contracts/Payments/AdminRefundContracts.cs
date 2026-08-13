namespace Shora.Contracts.Payments;

public sealed record RecordRefundRequest(
    string Reference,
    string? Note);

public sealed record PaymentRefundResponse(
    Guid PaymentId,
    Guid BookingId,
    string PaymentStatus,
    string? RefundReference,
    DateTime? RefundedAtUtc);
