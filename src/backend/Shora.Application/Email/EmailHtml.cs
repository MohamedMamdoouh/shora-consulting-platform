using System.Net;
using System.Text;

namespace Shora.Application.Email;

public static class EmailHtml
{
    private const string Ink = "#1a2e2a";
    private const string Primary = "#1a3a3a";
    private const string Accent = "#7a9e8e";
    private const string Linen = "#f5f1ea";
    private const string Border = "#e5ebe8";
    private const string TextMuted = "#5c7268";
    private const string TextSubtle = "#8a9e94";
    private const string FontDisplay = "'Cairo', 'Segoe UI', Tahoma, Arial, sans-serif";
    private const string Rtl = "direction:rtl;text-align:right";

    public static string Join(params string?[] parts) =>
        string.Concat(parts.Where(static part => !string.IsNullOrEmpty(part)));

    public static string BrandHeader(string brandName, string heading) =>
        $"""
        <p dir="rtl" style="margin:0 0 4px;font-size:20px;line-height:1.3;font-weight:700;color:{Ink};font-family:{FontDisplay};direction:rtl;text-align:center">
          {HtmlEncode(brandName)}
        </p>
        <p dir="rtl" style="margin:0;font-size:22px;line-height:1.35;font-weight:700;color:{Ink};font-family:{FontDisplay};direction:rtl;text-align:center">
          {HtmlEncode(heading)}
        </p>
        """;

    public static string Paragraph(string text) =>
        $"""<p dir="rtl" style="margin:0 0 16px;font-size:15px;line-height:1.75;color:{Ink};{Rtl}">{HtmlEncode(text)}</p>""";

    public static string ParagraphLast(string text) =>
        $"""<p dir="rtl" style="margin:0;font-size:15px;line-height:1.75;color:{Ink};{Rtl}">{HtmlEncode(text)}</p>""";

    public static string DetailsList(params (string Label, string Value)[] items) =>
        DetailsList(items.Select(static item => (item.Label, item.Value, false)).ToArray());

    public static string DetailsList(params (string Label, string Value, bool Ltr)[] items) =>
        DetailsList((IReadOnlyList<(string Label, string Value, bool Ltr)>)items);

    public static string DetailsList(IReadOnlyList<(string Label, string Value, bool Ltr)> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(
            $"""<table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" dir="rtl" style="margin:0 0 20px;background-color:{Linen};border:1px solid {Border};border-radius:10px;overflow:hidden;{Rtl}">""");

        for (var index = 0; index < items.Count; index++)
        {
            var (label, value, ltr) = items[index];
            var divider = index < items.Count - 1
                ? $"border-bottom:1px solid {Border};"
                : string.Empty;

            builder.Append(
                $"""
                <tr>
                  <td dir="rtl" style="padding:12px 16px;{divider}{Rtl}">
                    <span style="display:block;font-size:12px;line-height:1.4;color:{TextSubtle};margin-bottom:4px">{HtmlEncode(label)}</span>
                    <span style="display:block;font-size:15px;line-height:1.5;color:{Ink}">{FormatDetailValue(value, ltr)}</span>
                  </td>
                </tr>
                """);
        }

        builder.Append("</table>");
        return builder.ToString();
    }

    public static string OptionalNoteParagraph(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return string.Empty;
        }

        return $"""
            <p dir="rtl" style="margin:0 0 16px;padding:12px 16px;background-color:{Linen};border-right:3px solid {Accent};border-radius:8px;font-size:14px;line-height:1.65;color:{Ink};{Rtl}">
              <span style="color:{TextMuted};font-weight:600">ملاحظة:</span> {HtmlEncode(note)}
            </p>
            """;
    }

    public static string VoiceCallInstructions(string phone) =>
        $"""<p dir="rtl" style="margin:0 0 16px;font-size:14px;line-height:1.65;color:{TextMuted};{Rtl}">سأتصل بك هاتفيًا على <strong style="color:{Ink}">{FormatDetailValue(phone, ltr: true)}</strong> في موعد الجلسة.</p>""";

    public static string WhatsAppInstructions(string url) =>
        $"""<p dir="rtl" style="margin:0 0 16px;font-size:14px;line-height:1.65;color:{TextMuted};{Rtl}">أجري الجلسة عبر واتساب. <a href="{HtmlAttributeEncode(url)}" dir="ltr" style="color:{Primary};font-weight:600;text-decoration:none;unicode-bidi:isolate">اضغط هنا للتواصل</a> عند موعد الجلسة.</p>""";

    public static string HtmlEncode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlEncode(value);

    private static string FormatDetailValue(string value, bool ltr) =>
        ltr
            ? $"""<span dir="ltr" style="unicode-bidi:isolate">{HtmlEncode(value)}</span>"""
            : HtmlEncode(value);

    private static string HtmlAttributeEncode(string value) => WebUtility.HtmlEncode(value);
}
