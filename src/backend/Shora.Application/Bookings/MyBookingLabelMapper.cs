using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Bookings;

internal static class MyBookingLabelMapper
{
    public const string CancelledByYou = "Cancelled by you";
    public const string CancelledByConsultant = "Cancelled by the consultant";
    public const string ReceiptNotUploadedInTime = "Receipt not uploaded in time";
    public const string Refunded = "Refunded";
    public const string RefundBeingProcessed = "Refund being processed";

    public static string? MapCancellationReasonLabel(BookingStatusAudit? cancelAudit) =>
        cancelAudit?.Actor switch
        {
            AuditActor.Client => CancelledByYou,
            AuditActor.Admin => CancelledByConsultant,
            AuditActor.System => ReceiptNotUploadedInTime,
            _ => null
        };

    public static string? MapRefundLabel(BookingStatus bookingStatus, Payment? payment)
    {
        if (bookingStatus != BookingStatus.Cancelled || payment is null)
        {
            return null;
        }

        return payment.Status switch
        {
            PaymentStatus.Refunded => Refunded,
            PaymentStatus.Approved => RefundBeingProcessed,
            _ => null
        };
    }
}
