using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Bookings;

internal static class MyBookingLabelMapper
{
    public const string CancelledByYou = "Cancelled by you";
    public const string CancelledByInstructor = "Cancelled by the instructor";
    public const string CancelledBySystem = "Cancelled by the system";
    public const string ReceiptNotUploadedInTime = "Receipt not uploaded in time";
    public const string Refunded = "Refunded";
    public const string RefundBeingProcessed = "Refund being processed";

    public static string? MapCancellationReasonLabel(
        BookingStatusAudit? cancelAudit,
        CancellationRequest? cancellationRequest) =>
        ResolveCancelledBy(cancelAudit, cancellationRequest) switch
        {
            CancelledBy.Client => CancelledByYou,
            CancelledBy.Instructor => CancelledByInstructor,
            CancelledBy.System => CancelledBySystem,
            _ => null
        };

    public static string? MapCancellationDetail(
        BookingStatusAudit? cancelAudit,
        CancellationRequest? cancellationRequest)
    {
        return ResolveCancelledBy(cancelAudit, cancellationRequest) switch
        {
            CancelledBy.Client => NormalizeDetail(cancellationRequest?.ClientReason),
            CancelledBy.System => ReceiptNotUploadedInTime,
            _ => null
        };
    }

    private static CancelledBy? ResolveCancelledBy(
        BookingStatusAudit? cancelAudit,
        CancellationRequest? cancellationRequest)
    {
        if (cancelAudit is null)
        {
            return null;
        }

        return cancelAudit.Actor switch
        {
            AuditActor.Client => CancelledBy.Client,
            AuditActor.System => CancelledBy.System,
            AuditActor.Admin when IsClientRequestedCancellation(cancellationRequest) => CancelledBy.Client,
            AuditActor.Admin => CancelledBy.Instructor,
            _ => null
        };
    }

    private static bool IsClientRequestedCancellation(CancellationRequest? request) =>
        request is { Status: CancellationRequestStatus.Approved };

    private static string? NormalizeDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

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

    private enum CancelledBy
    {
        Client,
        Instructor,
        System
    }
}
