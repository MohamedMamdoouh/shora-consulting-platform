using Shora.Domain.Enums;

namespace Shora.Application.Availability;

public static class BlockedDateConflictPolicy
{
    public static readonly BookingStatus[] ActiveBlockingStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingApproval,
        BookingStatus.Confirmed,
        BookingStatus.CancellationRequested,
        BookingStatus.Completed
    ];

    public static bool OverlapsRange(DateTime rangeStartUtc, DateTime rangeEndUtc, DateTime slotStartUtc, DateTime slotEndUtc) =>
        slotStartUtc < rangeEndUtc && slotEndUtc > rangeStartUtc;
}
