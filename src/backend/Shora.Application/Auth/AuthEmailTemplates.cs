using Shora.Application.Email;

namespace Shora.Application.Auth;

internal enum AuthEmailKind
{
    VerifyEmail,
    ResetPassword
}

internal static class AuthEmailTemplates
{
    public static EmailTemplateRequest BuildRequest(
        AuthEmailKind kind,
        string recipientName,
        string actionUrl)
    {
        return kind switch
        {
            AuthEmailKind.VerifyEmail => new EmailTemplateRequest(
                ContentTemplate: "Auth/verify-email.content.html",
                PreviewText: "أكد بريدك الإلكتروني لتفعيل حسابك.",
                Heading: "تأكيد البريد الإلكتروني",
                ActionUrl: actionUrl,
                ActionLabel: "تأكيد البريد الإلكتروني",
                RecipientName: recipientName,
                FooterNote: "إذا لم تنشئ هذا الحساب، يمكنك تجاهل هذه الرسالة."),

            AuthEmailKind.ResetPassword => new EmailTemplateRequest(
                ContentTemplate: "Auth/reset-password.content.html",
                PreviewText: "أعد تعيين كلمة المرور لحسابك.",
                Heading: "إعادة تعيين كلمة المرور",
                ActionUrl: actionUrl,
                ActionLabel: "تعيين كلمة مرور جديدة",
                RecipientName: recipientName,
                FooterNote: "إذا لم تطلب إعادة التعيين، يمكنك تجاهل هذه الرسالة."),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static string GetSubject(AuthEmailKind kind, string brandName)
    {
        return kind switch
        {
            AuthEmailKind.VerifyEmail => $"تأكيد بريدك الإلكتروني — {brandName}",
            AuthEmailKind.ResetPassword => $"إعادة تعيين كلمة المرور — {brandName}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
