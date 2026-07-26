using Microsoft.Extensions.Options;
using Shora.Application.Auth;
using Shora.Application.Email;
using Shora.Application.Options;

namespace Shora.Tests.Unit.Email;

public class EmailTemplateTests
{
    private static readonly EmailBrandOptions DefaultBrand = new();

    [Fact]
    public void Verify_email_template_includes_branding_action_and_recipient()
    {
        var service = new EmailTemplateService(Options.Create(DefaultBrand));
        var request = AuthEmailTemplates.BuildRequest(
            AuthEmailKind.VerifyEmail,
            recipientName: "سارة",
            actionUrl: "https://example.com/auth/verify-email?email=test%40example.com&token=abc");
        var subject = AuthEmailTemplates.GetSubject(AuthEmailKind.VerifyEmail, DefaultBrand.BrandName);
        var htmlBody = service.Render(request);

        Assert.Contains("تأكيد بريدك الإلكتروني", subject);
        Assert.Contains("منصة شورى", htmlBody);
        Assert.Contains("تأكيد البريد الإلكتروني", htmlBody);
        Assert.Contains("سارة", htmlBody);
        Assert.Contains("https://example.com/auth/verify-email?email=test%40example.com&token=abc", htmlBody);
        Assert.DoesNotContain("{{", htmlBody);
    }

    [Fact]
    public void Reset_password_template_includes_branding_action_and_recipient()
    {
        var service = new EmailTemplateService(Options.Create(DefaultBrand));
        var request = AuthEmailTemplates.BuildRequest(
            AuthEmailKind.ResetPassword,
            recipientName: "سارة",
            actionUrl: "https://example.com/auth/reset-password?email=test%40example.com&token=abc");
        var subject = AuthEmailTemplates.GetSubject(AuthEmailKind.ResetPassword, DefaultBrand.BrandName);
        var htmlBody = service.Render(request);

        Assert.Contains("إعادة تعيين كلمة المرور", subject);
        Assert.Contains("منصة شورى", htmlBody);
        Assert.Contains("تعيين كلمة مرور جديدة", htmlBody);
        Assert.Contains("سارة", htmlBody);
        Assert.DoesNotContain("{{", htmlBody);
    }
}
