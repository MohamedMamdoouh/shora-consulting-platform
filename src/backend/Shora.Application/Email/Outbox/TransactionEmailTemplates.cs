using Shora.Application.Availability;
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

    public string? PreviousRefundReference { get; init; }

    public string? CorrectionReason { get; init; }

    public DateTime? AutoDeclineAtUtc { get; init; }

    public string? ClientReason { get; init; }
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
            Common.OutboxMessageTypes.AdminRefundRevocationEmail =>
                BuildAdminRefundRevocation(context, links),
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
            Common.OutboxMessageTypes.AdminRefundRevocationEmail =>
                $"تصحيح تسجيل استرداد — {brandName}",
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
            ContentTemplate: "Transaction/client-booking-confirmed.content.html",
            PreviewText: "تم تأكيد حجزك بنجاح.",
            Heading: "تم تأكيد حجزك",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "أتطلع إلى جلستك. إذا احتجت مساعدة، راسلني عبر لوحة العميل.",
            AdditionalTokens: BuildDetailTokens(
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText)),
                ("DeliveryMethod", TransactionEmailLabels.HtmlEncode(
                    TransactionEmailLabels.FormatDeliveryMethod(booking.DeliveryMethod))),
                ("DeliveryInstructions", TransactionEmailLabels.FormatDeliveryInstructionsHtml(
                    booking,
                    context.Settings))));
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
            ContentTemplate: "Transaction/admin-new-booking.content.html",
            PreviewText: "تم تأكيد حجز جديد.",
            Heading: "حجز جديد مؤكد",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة الحجوزات",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "راجع تفاصيل الحجز في لوحة الإدارة.",
            AdditionalTokens: BuildDetailTokens(
                ("ClientName", TransactionEmailLabels.HtmlEncode(client.DisplayName)),
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText)),
                ("DeliveryMethod", TransactionEmailLabels.HtmlEncode(
                    TransactionEmailLabels.FormatDeliveryMethod(booking.DeliveryMethod))),
                ("ContactPhone", TransactionEmailLabels.HtmlEncode(contactPhone))));
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
            ContentTemplate: "Transaction/admin-receipt-uploaded.content.html",
            PreviewText: "إيصال دفع جديد بانتظار مراجعتك.",
            Heading: "إيصال جديد بانتظار المراجعة",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة الإيصال",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يرجى مراجعة الإيصال في أقرب وقت ممكن.",
            AdditionalTokens: BuildDetailTokens(
                ("ClientName", TransactionEmailLabels.HtmlEncode(booking.Client.DisplayName)),
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText))));
    }

    private static EmailTemplateRequest BuildClientReceiptDeclined(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var reason = TransactionEmailLabels.FormatReceiptDeclineReason(context.ReasonCode);
        var note = TransactionEmailLabels.OptionalNoteParagraph(context.ReasonNote);
        var deadline = context.ReceiptUploadDeadlineUtc is { } deadlineUtc
            ? TransactionEmailLabels.FormatDateTimeUtc(deadlineUtc, ConsultantTimeZone)
            : "—";

        return new EmailTemplateRequest(
            ContentTemplate: "Transaction/client-receipt-declined.content.html",
            PreviewText: "يرجى إعادة رفع إيصال الدفع.",
            Heading: "يرجى إعادة رفع الإيصال",
            ActionUrl: links.ClientPayment(booking.Id),
            ActionLabel: "رفع إيصال جديد",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "إذا كان لديك أي استفسار، راسلني عبر لوحة العميل.",
            AdditionalTokens: BuildDetailTokens(
                ("DeclineReason", TransactionEmailLabels.HtmlEncode(reason)),
                ("ReasonNoteHtml", note),
                ("UploadDeadline", TransactionEmailLabels.HtmlEncode(deadline))));
    }

    private static EmailTemplateRequest BuildClientBookingCancelled(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var slotText = TransactionEmailLabels.FormatSlotRange(
            context.Booking.SlotStartUtc,
            context.Booking.SlotEndUtc,
            ConsultantTimeZone);

        return new EmailTemplateRequest(
            ContentTemplate: "Transaction/client-booking-cancelled.content.html",
            PreviewText: "تم إلغاء حجزك.",
            Heading: "تم إلغاء الحجز",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يمكنك حجز موعد جديد في أي وقت.",
            AdditionalTokens: BuildDetailTokens(
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText))));
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
            ContentTemplate: "Transaction/admin-new-cancellation-request.content.html",
            PreviewText: "طلب إلغاء جديد بانتظار قرارك.",
            Heading: "طلب إلغاء جديد",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة طلب الإلغاء",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "يرجى اتخاذ قرار قبل موعد الإغلاق التلقائي.",
            AdditionalTokens: BuildDetailTokens(
                ("ClientName", TransactionEmailLabels.HtmlEncode(booking.Client.DisplayName)),
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText)),
                ("ClientReason", TransactionEmailLabels.HtmlEncode(clientReason)),
                ("AutoDeclineAt", TransactionEmailLabels.HtmlEncode(autoDecline))));
    }

    private static EmailTemplateRequest BuildClientCancellationRequestDeclined(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var booking = context.Booking;
        var reason = TransactionEmailLabels.FormatCancellationDecisionReason(context.ReasonCode);
        var note = TransactionEmailLabels.OptionalNoteParagraph(context.ReasonNote);
        var slotText = TransactionEmailLabels.FormatSlotRange(
            booking.SlotStartUtc,
            booking.SlotEndUtc,
            ConsultantTimeZone);

        return new EmailTemplateRequest(
            ContentTemplate: "Transaction/client-cancellation-request-declined.content.html",
            PreviewText: "تم رفض طلب الإلغاء ويبقى موعدك قائمًا.",
            Heading: "تم رفض طلب الإلغاء",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "موعدك ما زال مؤكدًا كما هو.",
            AdditionalTokens: BuildDetailTokens(
                ("DeclineReason", TransactionEmailLabels.HtmlEncode(reason)),
                ("ReasonNoteHtml", note),
                ("SlotTime", TransactionEmailLabels.HtmlEncode(slotText))));
    }

    private static EmailTemplateRequest BuildClientRefundConfirmation(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var amount = context.RefundAmount ?? context.Payment?.Amount ?? 0m;
        var currency = context.RefundCurrency ?? context.Payment?.Currency ?? "EGP";
        var reference = string.IsNullOrWhiteSpace(context.RefundReference) ? "—" : context.RefundReference;
        var note = TransactionEmailLabels.OptionalNoteParagraph(context.RefundNote);

        return new EmailTemplateRequest(
            ContentTemplate: "Transaction/client-refund-confirmation.content.html",
            PreviewText: "تم تسجيل استرداد المبلغ.",
            Heading: "تأكيد الاسترداد",
            ActionUrl: links.ClientDashboard(),
            ActionLabel: "عرض حجوزاتي",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "إذا لم تستلم المبلغ بعد، تواصل معي عبر لوحة العميل.",
            AdditionalTokens: BuildDetailTokens(
                ("RefundAmount", TransactionEmailLabels.HtmlEncode(
                    TransactionEmailLabels.FormatMoney(amount, currency))),
                ("RefundReference", TransactionEmailLabels.HtmlEncode(reference)),
                ("RefundNoteHtml", note)));
    }

    private static EmailTemplateRequest BuildAdminRefundRevocation(
        TransactionEmailContext context,
        TransactionEmailLinks links)
    {
        var previousReference = string.IsNullOrWhiteSpace(context.PreviousRefundReference)
            ? "—"
            : context.PreviousRefundReference;
        var correctionReason = string.IsNullOrWhiteSpace(context.CorrectionReason)
            ? "—"
            : context.CorrectionReason;

        return new EmailTemplateRequest(
            ContentTemplate: "Transaction/admin-refund-revocation.content.html",
            PreviewText: "تم إلغاء تسجيل استرداد سابق.",
            Heading: "تصحيح تسجيل استرداد",
            ActionUrl: links.AdminBookings(),
            ActionLabel: "مراجعة الحجوزات",
            RecipientName: context.Recipient.DisplayName,
            FooterNote: "راجع حالة الاسترداد في لوحة الإدارة.",
            AdditionalTokens: BuildDetailTokens(
                ("PreviousReference", TransactionEmailLabels.HtmlEncode(previousReference)),
                ("CorrectionReason", TransactionEmailLabels.HtmlEncode(correctionReason)),
                ("BookingId", TransactionEmailLabels.HtmlEncode(context.Booking.Id.ToString("D")))));
    }

    private static Dictionary<string, string> BuildDetailTokens(
        params (string Key, string Value)[] tokens)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in tokens)
        {
            dictionary[key] = value;
        }

        return dictionary;
    }
}
