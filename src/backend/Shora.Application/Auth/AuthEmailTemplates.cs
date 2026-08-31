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
        string actionUrl,
        string brandName)
    {
        return kind switch
        {
            AuthEmailKind.VerifyEmail => new EmailTemplateRequest(
                BodyHtml: EmailHtml.Join(
                    EmailHtml.Paragraph(
                        $"شكرًا لتسجيلك لدى {brandName}. لتفعيل حسابك وإتمام الحجز، يرجى تأكيد بريدك الإلكتروني."),
                    EmailHtml.ParagraphLast("اضغط على الزر أدناه لتأكيد بريدك الإلكتروني.")),
                Heading: "تأكيد البريد الإلكتروني",
                ActionUrl: actionUrl,
                ActionLabel: "تأكيد البريد الإلكتروني",
                RecipientName: recipientName,
                FooterNote: "إذا لم تنشئ هذا الحساب، يمكنك تجاهل هذه الرسالة."),

            AuthEmailKind.ResetPassword => new EmailTemplateRequest(
                BodyHtml: EmailHtml.Join(
                    EmailHtml.Paragraph(
                        $"تلقيت طلبًا لإعادة تعيين كلمة المرور لحسابك لدى {brandName}."),
                    EmailHtml.ParagraphLast(
                        "اضغط على الزر أدناه لاختيار كلمة مرور جديدة. صلاحية الرابط محدودة.")),
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
