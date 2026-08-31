using Shora.Application.Availability;
using Shora.Application.Email;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Email.Outbox;

internal sealed class TransactionEmailContext
{
    public required string MessageType { get; init; }

    public required ApplicationUser Recipient { get; init; }

    public required Booking Booking { get; init; }

    public required Settings Settings { get; init; }

    public Payment? Payment { get; init; }

    public string? ReasonCode { get; init; }

    public string? ReasonNote { get; init; }

    public DateTime? ReceiptUploadDeadlineUtc { get; init; }

    public string? RefundReference { get; init; }

    public string? RefundNote { get; init; }

    public decimal? RefundAmount { get; init; }

    public string? RefundCurrency { get; init; }

    public DateTime? AutoDeclineAtUtc { get; init; }

    public string? ClientReason { get; init; }

    public string? CancellationReasonLabel { get; init; }

    public string? CancellationDetail { get; init; }
}

internal static class TransactionEmailTemplates
{
    private static readonly TimeZoneInfo ConsultantTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(SlotGenerationConstants.ConsultantTimeZoneId);

    public static EmailTemplateRequest BuildRequest(
        TransactionEmailContext context,
        TransactionEmailLinks links,
        string brandName)
    {
        return context.MessageType switch
        {
            Common.OutboxMessageTypes.ClientBookingConfirmedEmail =>
                BuildClientBookingConfirmed(context, links),
            Common.OutboxMessageTypes.AdminNewBookingEmail =>
                BuildAdminNewBooking(context, links),
            Common.OutboxMessageTypes.AdminReceiptUploadedEmail =>
                BuildAdminReceiptUploaded(context, links),
            Common.OutboxMessageTypes.ClientReceiptDeclinedEmail =>
                BuildClientReceiptDeclined(context, links),
            Common.OutboxMessageTypes.ClientBookingCancelledEmail =>
                BuildClientBookingCancelled(context, links),
            Common.OutboxMessageTypes.AdminNewCancellationRequestEmail =>
                BuildAdminNewCancellationRequest(context, links),
            Common.OutboxMessageTypes.ClientCancellationRequestDeclinedEmail =>
                BuildClientCancellationRequestDeclined(context, links),
            Common.OutboxMessageTypes.ClientRefundConfirmationEmail =>
                BuildClientRefundConfirmation(context, links),
            _ => throw new InvalidOperationException(
                $"Unsupported outbox message type '{context.MessageType}'.")
        };
    }

    public static string GetSubject(string messageType, string brandName) =>
        messageType switch
        {
            Common.OutboxMessageTypes.ClientBookingConfirmedEmail =>
                $"تم تأكيد حجزك — {brandName}",
            Common.OutboxMessageTypes.AdminNewBookingEmail =>
                $"حجز جديد مؤكد — {brandName}",
            Common.OutboxMessageTypes.AdminReceiptUploadedEmail =>
                $"إيصال دفع جديد بانتظار المراجعة — {brandName}",
            Common.OutboxMessageTypes.ClientReceiptDeclinedEmail =>
                $"يرجى إعادة رفع إيصال الدفع — {brandName}",
            Common.OutboxMessageTypes.ClientBookingCancelledEmail =>
                $"تم إلغاء حجزك — {brandName}",
            Common.OutboxMessageTypes.AdminNewCancellationRequestEmail =>
                $"طلب إلغاء جديد — {brandName}",
            Common.OutboxMessageTypes.ClientCancellationRequestDeclinedEmail =>
                $"تم رفض طلب الإلغاء — {brandName}",
            Common.OutboxMessageTypes.ClientRefundConfirmationEmail =>
                $"تأكيد استرداد المبلغ — {brandName}",
            _ => throw new InvalidOperationException(
                $"Unsupported outbox message type '{messageType}'.")
        };

    private static EmailTemplateRequest BuildClientBookingConfirmed(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("أؤكد حجزك بنجاح."),
                EmailHtml.DetailsList(
                    ("موعد الجلسة", slotText),
                    ("طريقة التسليم", TransactionEmailLabels.FormatDeliveryMethod(booking.DeliveryMethod))),
                TransactionEmailLabels.FormatDeliveryInstructionsHtml(booking, context.Settings),
                EmailHtml.ParagraphLast("يمكنك متابعة تفاصيل الحجز من لوحة العميل.")),
            Heading: "تم تأكيد حجزك",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "أتطلع إلى جلستك. إذا احتجت مساعدة، راسلني عبر لوحة العميل.");
    }

    private static EmailTemplateRequest BuildAdminNewBooking(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var client = booking.Client;
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);
        var contactPhone = string.IsNullOrWhiteSpace(booking.ContactPhone)
            ? "—"
            : booking.ContactPhone;

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("تم تأكيد حجز جديد بعد قبول الإيصال."),
                EmailHtml.DetailsList(
                    ("العميل", client.DisplayName, false),
                    ("موعد الجلسة", slotText, false),
                    ("طريقة التسليم", TransactionEmailLabels.FormatDeliveryMethod(booking.DeliveryMethod), false),
                    ("رقم التواصل", contactPhone, true)),
                EmailHtml.ParagraphLast("راجع تفاصيل الحجز في لوحة الإدارة.")),
            Heading: "حجز جديد مؤكد",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة الحجوزات",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "راجع تفاصيل الحجز في لوحة الإدارة.");
    }

    private static EmailTemplateRequest BuildAdminReceiptUploaded(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph($"رفع العميل {booking.Client.DisplayName} إيصال دفع جديد ويحتاج إلى مراجعتك."),
                EmailHtml.DetailsList(("موعد الجلسة", slotText)),
                EmailHtml.ParagraphLast("يرجى مراجعة الإيصال واتخاذ قرار القبول أو الرفض.")),
            Heading: "إيصال جديد بانتظار المراجعة",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة الإيصال",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يرجى مراجعة الإيصال في أقرب وقت ممكن.");
    }

    private static EmailTemplateRequest BuildClientReceiptDeclined(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var reason = TransactionEmailLabels.FormatReceiptDeclineReason(context.ReasonCode);
        var note = EmailHtml.OptionalNoteParagraph(context.ReasonNote);
        var deadline = context.ReceiptUploadDeadlineUtc is { } deadlineUtc
            ? TransactionEmailLabels.FormatDateTimeUtc(deadlineUtc, ConsultantTimeZone)
            : "—";

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("لم أتمكن من قبول إيصال الدفع الأخير."),
                EmailHtml.DetailsList(
                    ("سبب الرفض", reason),
                    ("آخر موعد لرفع الإيصال", deadline)),
                note,
                EmailHtml.ParagraphLast("يرجى رفع إيصال جديد قبل انتهاء المهلة.")),
            Heading: "يرجى إعادة رفع الإيصال",
            ActionUrl: links.ClientPayment(booking.Id),
            ActionLabel: "رفع إيصال جديد",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "إذا كان لديك أي استفسار، راسلني عبر لوحة العميل.");
    }

    private static EmailTemplateRequest BuildClientBookingCancelled(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var slotText = TransactionEmailLabels.FormatSlotRange(
            context.Booking.SlotStartUtc,
            context.Booking.SlotEndUtc,
            ConsultantTimeZone);
        var cancelledBy = TransactionEmailLabels.FormatCancelledBy(context.CancellationReasonLabel);
        var cancellationDetail = TransactionEmailLabels.FormatCancellationDetail(context.CancellationDetail);
        var details = new List<(string Label, string Value)>
        {
            ("موعد الجلسة", slotText),
            ("من قام بالإلغاء", cancelledBy)
        };

        if (!string.IsNullOrWhiteSpace(cancellationDetail))
        {
            details.Add(("السبب", cancellationDetail));
        }

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("تم إلغاء حجزك."),
                EmailHtml.DetailsList(details.ToArray()),
                EmailHtml.ParagraphLast("إذا كان لديك أي استفسار، يمكنك التواصل معي عبر لوحة العميل.")),
            Heading: "تم إلغاء الحجز",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يمكنك حجز موعد جديد في أي وقت.");
    }

    private static EmailTemplateRequest BuildAdminNewCancellationRequest(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);
        var autoDecline = context.AutoDeclineAtUtc is { } autoDeclineUtc
            ? TransactionEmailLabels.FormatDateTimeUtc(autoDeclineUtc, ConsultantTimeZone)
            : "—";
        var clientReason = string.IsNullOrWhiteSpace(context.ClientReason)
            ? "—"
            : context.ClientReason;

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph($"قدم العميل {booking.Client.DisplayName} طلب إلغاء جديد."),
                EmailHtml.DetailsList(
                    ("موعد الجلسة", slotText),
                    ("سبب العميل", clientReason),
                    ("الإغلاق التلقائي", autoDecline)),
                EmailHtml.ParagraphLast("يرجى مراجعة الطلب واتخاذ قرار قبل موعد الإغلاق التلقائي.")),
            Heading: "طلب إلغاء جديد",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة طلب الإلغاء",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يرجى اتخاذ قرار قبل موعد الإغلاق التلقائي.");
    }

    private static EmailTemplateRequest BuildClientCancellationRequestDeclined(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var reason = TransactionEmailLabels.FormatCancellationDecisionReason(context.ReasonCode);
        var note = EmailHtml.OptionalNoteParagraph(context.ReasonNote);
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("تم رفض طلب الإلغاء، وموعدك ما زال مؤكدًا."),
                EmailHtml.DetailsList(
                    ("سبب الرفض", reason),
                    ("موعد الجلسة", slotText)),
                note,
                EmailHtml.ParagraphLast("أتطلع إلى جلستك في الموعد المحدد.")),
            Heading: "تم رفض طلب الإلغاء",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "موعدك ما زال مؤكدًا كما هو.");
    }

    private static EmailTemplateRequest BuildClientRefundConfirmation(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var amount = context.RefundAmount ?? context.Payment?.Amount ?? 0m;
        var currency = context.RefundCurrency ?? context.Payment?.Currency ?? "EGP";
        var reference = string.IsNullOrWhiteSpace(context.RefundReference) ? "—" : context.RefundReference;
        var note = EmailHtml.OptionalNoteParagraph(context.RefundNote);

        return new EmailTemplateRequest(
            BodyHtml: EmailHtml.Join(
                EmailHtml.Paragraph("تم تسجيل استرداد المبلغ لحجزك الذي تم إلغاؤه."),
                EmailHtml.DetailsList(
                    ("المبلغ", TransactionEmailLabels.FormatMoney(amount, currency), false),
                    ("مرجع التحويل", reference, true)),
                note,
                EmailHtml.ParagraphLast("إذا لم تستلم المبلغ بعد، تواصل معي عبر لوحة العميل.")),
            Heading: "تأكيد الاسترداد",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "إذا لم تستلم المبلغ بعد، تواصل معي عبر لوحة العميل.");
    }
}
