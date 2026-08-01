namespace Shora.Contracts.Payments;

public sealed record PaymentInstructionsResponse(
    decimal Amount,
    string Currency,
    string VodafoneCashNumber,
    string InstaPayHandle,
    string? PaymentInstructions,
    DateTime ReceiptUploadDeadlineUtc);

public sealed record UploadReceiptResponse(
    Guid ReceiptId,
    Guid BookingId,
    string BookingStatus,
    IReadOnlyList<string> ReviewWarnings);
