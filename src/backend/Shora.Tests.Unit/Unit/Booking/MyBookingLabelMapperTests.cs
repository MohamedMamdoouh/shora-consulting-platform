using Shora.Application.Bookings;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Tests.Unit.Bookings;

public class MyBookingLabelMapperTests
{
    [Fact]
    public void Maps_client_hold_cancellation_without_detail()
    {
        var audit = CancelAudit(AuditActor.Client);

        Assert.Equal(
            MyBookingLabelMapper.CancelledByYou,
            MyBookingLabelMapper.MapCancellationReasonLabel(audit, cancellationRequest: null));
        Assert.Null(MyBookingLabelMapper.MapCancellationDetail(audit, cancellationRequest: null));
    }

    [Fact]
    public void Maps_instructor_direct_cancellation()
    {
        var audit = CancelAudit(AuditActor.Admin);

        Assert.Equal(
            MyBookingLabelMapper.CancelledByInstructor,
            MyBookingLabelMapper.MapCancellationReasonLabel(audit, cancellationRequest: null));
        Assert.Null(MyBookingLabelMapper.MapCancellationDetail(audit, cancellationRequest: null));
    }

    [Fact]
    public void Maps_approved_client_request_to_the_client_with_trimmed_reason()
    {
        var audit = CancelAudit(AuditActor.Admin);
        var request = new CancellationRequest
        {
            Status = CancellationRequestStatus.Approved,
            ClientReason = "  تعارض في الموعد  "
        };

        Assert.Equal(
            MyBookingLabelMapper.CancelledByYou,
            MyBookingLabelMapper.MapCancellationReasonLabel(audit, request));
        Assert.Equal(
            "تعارض في الموعد",
            MyBookingLabelMapper.MapCancellationDetail(audit, request));
    }

    [Fact]
    public void Maps_system_cancellation_with_receipt_deadline_detail()
    {
        var audit = CancelAudit(AuditActor.System);

        Assert.Equal(
            MyBookingLabelMapper.CancelledBySystem,
            MyBookingLabelMapper.MapCancellationReasonLabel(audit, cancellationRequest: null));
        Assert.Equal(
            MyBookingLabelMapper.ReceiptNotUploadedInTime,
            MyBookingLabelMapper.MapCancellationDetail(audit, cancellationRequest: null));
    }

    [Fact]
    public void Returns_null_when_there_is_no_cancel_audit()
    {
        Assert.Null(MyBookingLabelMapper.MapCancellationReasonLabel(null, null));
        Assert.Null(MyBookingLabelMapper.MapCancellationDetail(null, null));
    }

    private static BookingStatusAudit CancelAudit(AuditActor actor) =>
        new()
        {
            ToStatus = BookingStatus.Cancelled,
            Actor = actor,
            AtUtc = DateTime.UtcNow
        };
}
