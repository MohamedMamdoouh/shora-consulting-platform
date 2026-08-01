namespace Shora.Contracts.Payments;

public enum ReceiptDeclineReasonCode
{
    UnreadableImage = 0,
    AmountMismatch = 1,
    DuplicateReceipt = 2,
    UnverifiableTransfer = 3,
    Other = 4
}

public sealed record DeclineReceiptRequest(
    ReceiptDeclineReasonCode ReasonCode,
    string? ReasonNote);

public sealed record AdminReceiptDecisionResponse(
    Guid BookingId,
    string BookingStatus,
    string PaymentStatus,
    Guid ReceiptId,
    string ReceiptReviewStatus,
    DateTime? ReceiptUploadDeadlineUtc);

public sealed record AdminBookingReceiptsResponse(
    Guid BookingId,
    Guid PaymentId,
    string PaymentStatus,
    PaymentMethod? Method,
    decimal Amount,
    string Currency,
    IReadOnlyList<AdminPaymentReceiptItem> Receipts);

public sealed record AdminPaymentReceiptItem(
    Guid ReceiptId,
    int AttemptNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? SenderReference,
    DateTime UploadedAtUtc,
    string BlobState,
    string MalwareScanStatus,
    string ReviewStatus,
    string? DeclineReasonCode,
    string? DeclineReason,
    DateTime? ReviewedAtUtc,
    IReadOnlyList<string> ReviewWarnings,
    string? ImageReadUrl,
    DateTime? ImageReadUrlExpiresAtUtc);
