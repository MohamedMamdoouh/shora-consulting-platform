namespace Shora.Domain.Enums;

public enum BookingStatus
{
    PendingPayment = 0,
    PendingApproval = 1,
    Confirmed = 2,
    CancellationRequested = 3,
    Completed = 4,
    Cancelled = 5
}
