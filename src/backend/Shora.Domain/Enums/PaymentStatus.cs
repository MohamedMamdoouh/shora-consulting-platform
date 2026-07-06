namespace Shora.Domain.Enums;

public enum PaymentStatus
{
    AwaitingReceipt = 0,
    UnderReview = 1,
    Approved = 2,
    Refunded = 3,
    Void = 4
}
