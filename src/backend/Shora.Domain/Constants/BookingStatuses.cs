using Shora.Domain.Enums;

namespace Shora.Domain.Constants;

public static class BookingStatuses
{
    public static readonly BookingStatus[] Active =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingApproval,
        BookingStatus.Confirmed,
        BookingStatus.CancellationRequested,
        BookingStatus.Completed
    ];
}
