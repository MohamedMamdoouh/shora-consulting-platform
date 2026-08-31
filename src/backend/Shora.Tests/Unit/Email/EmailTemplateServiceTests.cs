using Microsoft.Extensions.Options;
using Shora.Application.Email;
using Shora.Application.Options;

namespace Shora.Tests.Unit.Email;

public class EmailTemplateServiceTests
{
    [Fact]
    public void Render_replaces_tokens_in_content_and_layout()
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            BodyHtml: EmailHtml.Paragraph("نص الرسالة"),
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains("lang=\"ar\"", html);
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("#1a3a3a", html);
        Assert.Contains("دكتور محمود البنا", html);
        Assert.Contains("Alex", html);
        Assert.Contains("نص الرسالة", html);
        Assert.Contains("https://example.com/action", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Render_html_encodes_recipient_name()
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            BodyHtml: EmailHtml.Paragraph("نص الرسالة"),
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "<script>alert(1)</script>",
            FooterNote: "ملاحظة"));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Fact]
    public void Render_uses_configured_brand_name()
    {
        var service = CreateService(new EmailBrandOptions
        {
            BrandName = "علامة مخصصة"
        });

        var html = service.Render(new EmailTemplateRequest(
            BodyHtml: EmailHtml.Paragraph("نص الرسالة"),
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains("علامة مخصصة", html);
    }

    [Fact]
    public void Render_omits_preview_text_and_fallback_link()
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            BodyHtml: EmailHtml.Paragraph("نص الرسالة"),
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.DoesNotContain("إذا لم يعمل الزر", html);
        Assert.DoesNotContain("max-height: 0", html);
    }

    [Fact]
    public void Render_includes_rtl_direction_on_body_and_content()
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            BodyHtml: EmailHtml.Paragraph("نص الرسالة"),
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("direction:rtl", html);
        Assert.Contains("text-align:right", html);
    }

    private static EmailTemplateService CreateService(EmailBrandOptions? options = null) =>
        new(Options.Create(options ?? new EmailBrandOptions()));
}
