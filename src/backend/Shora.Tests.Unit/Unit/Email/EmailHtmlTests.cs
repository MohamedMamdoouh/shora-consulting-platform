using Shora.Application.Email;
using Shora.Application.Email.Outbox;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Tests.Unit.Email;

public class EmailHtmlTests
{
    [Fact]
    public void DetailsList_renders_label_value_rows()
    {
        var html = EmailHtml.DetailsList(("موعد الجلسة", "الاثنين 10:00"));

        Assert.Contains("موعد الجلسة", html);
        Assert.Contains("الاثنين 10:00", html);
        Assert.Contains("role=\"presentation\"", html);
    }

    [Fact]
    public void DetailsList_encodes_labels_and_values()
    {
        var html = EmailHtml.DetailsList(("العميل", "Alex <test>"));

        Assert.Contains("العميل", html);
        Assert.Contains("Alex &lt;test&gt;", html);
        Assert.DoesNotContain("<test>", html);
    }

    [Fact]
    public void OptionalNoteParagraph_returns_empty_for_blank_note()
    {
        Assert.Equal(string.Empty, EmailHtml.OptionalNoteParagraph(null));
        Assert.Equal(string.Empty, EmailHtml.OptionalNoteParagraph("   "));
    }

    [Fact]
    public void OptionalNoteParagraph_encodes_note_text()
    {
        var html = EmailHtml.OptionalNoteParagraph("ملاحظة <خاصة>");

        Assert.Contains("ملاحظة:", html);
        Assert.Contains("ملاحظة &lt;خاصة&gt;", html);
    }

    [Fact]
    public void VoiceCallInstructions_encodes_phone_number()
    {
        var html = EmailHtml.VoiceCallInstructions("0100<script>");

        Assert.Contains("0100&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void WhatsAppInstructions_includes_encoded_link()
    {
        var html = EmailHtml.WhatsAppInstructions("https://wa.me/201234567890");

        Assert.Contains("href=\"https://wa.me/201234567890\"", html);
        Assert.Contains("اضغط هنا للتواصل", html);
    }

    [Fact]
    public void FormatSlotRange_uses_arabic_day_and_month_names()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var startUtc = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddHours(1);

        var text = TransactionEmailLabels.FormatSlotRange(startUtc, endUtc, timeZone);

        Assert.Contains("مارس", text);
        Assert.DoesNotContain("March", text);
        Assert.DoesNotContain("Sunday", text);
        Assert.DoesNotContain("Monday", text);
    }

    [Fact]
    public void FormatDateTimeUtc_uses_arabic_day_and_month_names()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var valueUtc = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);

        var text = TransactionEmailLabels.FormatDateTimeUtc(valueUtc, timeZone);

        Assert.Contains("مارس", text);
        Assert.DoesNotContain("March", text);
    }

    [Fact]
    public void FormatDeliveryInstructionsHtml_renders_voice_call_instructions()
    {
        var booking = new Booking
        {
            DeliveryMethod = DeliveryMethod.VoiceCall,
            ContactPhone = "01001234567"
        };
        var settings = new Settings();

        var html = TransactionEmailLabels.FormatDeliveryInstructionsHtml(booking, settings);

        Assert.Contains("01001234567", html);
        Assert.Contains("سأتصل بك هاتفيًا", html);
    }

    [Fact]
    public void DetailsList_marks_ltr_values_for_phone_numbers()
    {
        var html = EmailHtml.DetailsList(("رقم التواصل", "+201012345678", true));

        Assert.Contains("dir=\"ltr\"", html);
        Assert.Contains("+201012345678", html);
    }

    [Fact]
    public void FormatDeliveryInstructionsHtml_renders_whatsapp_instructions()
    {
        var booking = new Booking
        {
            DeliveryMethod = DeliveryMethod.Chat
        };
        var settings = new Settings
        {
            ConsultantWhatsAppNumber = "+201234567890"
        };

        var html = TransactionEmailLabels.FormatDeliveryInstructionsHtml(booking, settings);

        Assert.Contains("https://wa.me/201234567890", html);
        Assert.Contains("واتساب", html);
    }
}
