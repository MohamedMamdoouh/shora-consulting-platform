namespace Shora.Application.Email.Outbox;

internal sealed class BookingClientPayload
{
    public Guid BookingId { get; set; }

    public Guid ClientId { get; set; }
}

internal sealed class BookingPaymentReceiptPayload
{
    public Guid BookingId { get; set; }

    public Guid PaymentId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid ClientId { get; set; }
}

internal sealed class ClientReceiptDeclinedPayload
{
    public Guid BookingId { get; set; }

    public Guid PaymentId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid ClientId { get; set; }

    public string? ReasonCode { get; set; }

    public string? ReasonNote { get; set; }

    public DateTime ReceiptUploadDeadlineUtc { get; set; }
}

internal sealed class ClientCancellationRequestDeclinedPayload
{
    public Guid BookingId { get; set; }

    public Guid ClientId { get; set; }

    public Guid RequestId { get; set; }

    public string? ReasonCode { get; set; }

    public string? ReasonNote { get; set; }
}

internal sealed class ClientRefundConfirmationPayload
{
    public Guid PaymentId { get; set; }

    public Guid BookingId { get; set; }

    public Guid ClientId { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;
}
