namespace Shora.Application.Email;

public interface IEmailTemplateService
{
    string Render(EmailTemplateRequest request);
}
