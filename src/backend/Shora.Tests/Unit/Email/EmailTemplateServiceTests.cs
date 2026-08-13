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
            ContentTemplate: "Auth/verify-email.content.html",
            PreviewText: "معاينة",
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains("lang=\"ar\"", html);
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("#4a5748", html);
        Assert.Contains("محمود البنا", html);
        Assert.Contains("Alex", html);
        Assert.Contains("https://example.com/action", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Render_html_encodes_recipient_name()
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            ContentTemplate: "Auth/verify-email.content.html",
            PreviewText: "معاينة",
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "<script>alert(1)</script>",
            FooterNote: "ملاحظة"));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Theory]
    [InlineData("Auth/verify-email.content.html", "تفعيل حسابك")]
    [InlineData("Auth/reset-password.content.html", "إعادة تعيين كلمة المرور")]
    public void Render_auth_content_templates_without_unreplaced_tokens(
        string contentTemplate,
        string expectedPhrase)
    {
        var service = CreateService();
        var html = service.Render(new EmailTemplateRequest(
            ContentTemplate: contentTemplate,
            PreviewText: "معاينة",
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains(expectedPhrase, html);
        Assert.Contains("Alex", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Render_uses_configured_brand_name()
    {
        var service = CreateService(new EmailBrandOptions
        {
            BrandName = "علامة مخصصة"
        });

        var html = service.Render(new EmailTemplateRequest(
            ContentTemplate: "Auth/verify-email.content.html",
            PreviewText: "معاينة",
            Heading: "عنوان",
            ActionUrl: "https://example.com/action",
            ActionLabel: "اضغط هنا",
            RecipientName: "Alex",
            FooterNote: "ملاحظة"));

        Assert.Contains("علامة مخصصة", html);
    }

    private static EmailTemplateService CreateService(EmailBrandOptions? options = null) =>
        new(Options.Create(options ?? new EmailBrandOptions()));
}
