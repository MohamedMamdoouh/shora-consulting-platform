using System.Net;
using Shora.Application.Bookings;
using Shora.Domain.Constants;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Email.Outbox;

internal static class TransactionEmailLabels
{
    public static string FormatDeliveryMethod(DeliveryMethod method) =>
        method switch
        {
            DeliveryMethod.VoiceCall => "مكالمة صوتية",
            DeliveryMethod.Chat => "محادثة واتساب",
            _ => method.ToString()
        };

    public static string FormatReceiptDeclineReason(string? reasonCode) =>
        reasonCode switch
        {
            "UnreadableImage" => "صورة غير واضحة",
            "AmountMismatch" => "المبلغ غير مطابق",
            "DuplicateReceipt" => "إيصال مكرر",
            "UnverifiableTransfer" => "تعذر التحقق من التحويل",
            "Other" => "سبب آخر",
            null or "" => "—",
            _ => reasonCode
        };

    public static string FormatCancellationDecisionReason(string? reasonCode) =>
        reasonCode switch
        {
            "TimingConflict" => "تعارض في المواعيد",
            "InsufficientReason" => "السبب غير كاف",
            "Policy" => "سياسة الإلغاء",
            "Other" => "سبب آخر",
            null or "" => "—",
            _ => reasonCode
        };

    public static string FormatCancelledBy(string? label) =>
        label switch
        {
            MyBookingLabelMapper.CancelledByYou => "أنت",
            MyBookingLabelMapper.CancelledByInstructor => "المستشار",
            MyBookingLabelMapper.CancelledBySystem => "النظام (تلقائيًا)",
            null or "" => "—",
            _ => label
        };

    public static string? FormatCancellationDetail(string? detail) =>
        detail switch
        {
            MyBookingLabelMapper.ReceiptNotUploadedInTime => "لم يُرفع الإيصال في الوقت المحدد",
            null or "" => null,
            _ => detail.Trim()
        };

    public static string OptionalListItem(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return $"<li><strong>{HtmlEncode(label)}:</strong> {HtmlEncode(value)}</li>";
    }

    public static string FormatSlotRange(DateTime slotStartUtc, DateTime slotEndUtc, TimeZoneInfo timeZone)
    {
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(slotStartUtc, timeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(slotEndUtc, timeZone);
        return $"{startLocal:dddd d MMMM yyyy، HH:mm} – {endLocal:HH:mm}";
    }

    public static string FormatDateTimeUtc(DateTime valueUtc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(valueUtc, timeZone);
        return $"{local:dddd d MMMM yyyy، HH:mm}";
    }

    public static string FormatMoney(decimal amount, string currency) =>
        $"{amount:N0} {CurrencyCodes.DisplayLabel(currency)}";

    public static string HtmlEncode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlEncode(value);

    public static string OptionalNoteParagraph(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return string.Empty;
        }

        return $"""<p style="margin:0 0 16px">ملاحظة: {HtmlEncode(note)}</p>""";
    }

    public static string FormatDeliveryInstructionsHtml(Booking booking, Settings settings)
    {
        if (booking.DeliveryMethod == DeliveryMethod.VoiceCall)
        {
            var phone = string.IsNullOrWhiteSpace(booking.ContactPhone)
                ? "رقمك المسجل"
                : HtmlEncode(booking.ContactPhone);

            return EmailTemplateService.RenderFragment(
                "Transaction/delivery-voice-call.fragment.html",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ContactPhone"] = phone
                });
        }

        var whatsAppNumber = settings.ConsultantWhatsAppNumber.TrimStart('+');
        var whatsAppUrl = $"https://wa.me/{whatsAppNumber}";

        return EmailTemplateService.RenderFragment(
            "Transaction/delivery-whatsapp.fragment.html",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WhatsAppUrl"] = whatsAppUrl
            });
    }
}
