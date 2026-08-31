using System.Globalization;
using Shora.Application.Bookings;
using Shora.Domain.Constants;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Email.Outbox;

internal static class TransactionEmailLabels
{
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-EG");

    public static string FormatDeliveryMethod(DeliveryMethod method) =>
        method switch
        {
            DeliveryMethod.VoiceCall => "مكالمة صوتية",
            DeliveryMethod.Chat => "محادثة واتساب",
            _ => "غير محدد"
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
            _ => "غير محدد"
        };

    public static string FormatCancellationDecisionReason(string? reasonCode) =>
        reasonCode switch
        {
            "TimingConflict" => "تعارض في المواعيد",
            "InsufficientReason" => "السبب غير كاف",
            "Policy" => "سياسة الإلغاء",
            "Other" => "سبب آخر",
            null or "" => "—",
            _ => "غير محدد"
        };

    public static string FormatCancelledBy(string? label) =>
        label switch
        {
            MyBookingLabelMapper.CancelledByYou => "أنت",
            MyBookingLabelMapper.CancelledByInstructor => "المستشار",
            MyBookingLabelMapper.CancelledBySystem => "النظام (تلقائيًا)",
            null or "" => "—",
            _ => "غير محدد"
        };

    public static string? FormatCancellationDetail(string? detail) =>
        detail switch
        {
            MyBookingLabelMapper.ReceiptNotUploadedInTime => "لم يتم رفع الإيصال في الوقت المحدد",
            null or "" => null,
            _ => detail.Trim()
        };

    public static string FormatSlotRange(DateTime slotStartUtc, DateTime slotEndUtc, TimeZoneInfo timeZone)
    {
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(slotStartUtc, timeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(slotEndUtc, timeZone);
        var datePart = startLocal.ToString("dddd d MMMM yyyy", ArabicCulture);
        var startTime = startLocal.ToString("HH:mm", ArabicCulture);
        var endTime = endLocal.ToString("HH:mm", ArabicCulture);
        return $"{datePart}، {startTime} – {endTime}";
    }

    public static string FormatDateTimeUtc(DateTime valueUtc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(valueUtc, timeZone);
        return local.ToString("dddd d MMMM yyyy، HH:mm", ArabicCulture);
    }

    public static string FormatMoney(decimal amount, string currency) =>
        $"{amount.ToString("N0", ArabicCulture)} {CurrencyCodes.DisplayLabel(currency)}";

    public static string FormatDeliveryInstructionsHtml(Booking booking, Settings settings)
    {
        if (booking.DeliveryMethod == DeliveryMethod.VoiceCall)
        {
            var phone = string.IsNullOrWhiteSpace(booking.ContactPhone)
                ? "رقمك المسجل"
                : booking.ContactPhone;

            return EmailHtml.VoiceCallInstructions(phone);
        }

        var whatsAppNumber = settings.ConsultantWhatsAppNumber.TrimStart('+');
        var whatsAppUrl = $"https://wa.me/{whatsAppNumber}";

        return EmailHtml.WhatsAppInstructions(whatsAppUrl);
    }
}
