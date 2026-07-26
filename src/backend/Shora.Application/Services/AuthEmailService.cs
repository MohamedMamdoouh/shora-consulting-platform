using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Auth;
using Shora.Application.Email;
using Shora.Application.Options;
using Shora.Domain.Entities;

namespace Shora.Application.Services;

public sealed class AuthEmailService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<EmailBrandOptions> emailBrandOptions)
{
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly EmailBrandOptions _emailBrandOptions = emailBrandOptions.Value;

    public async Task SendVerificationAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await SendAsync(user, AuthEmailKind.VerifyEmail, AuthFrontendRoutes.VerifyEmail, token, cancellationToken);
    }

    public async Task SendPasswordResetAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await SendAsync(user, AuthEmailKind.ResetPassword, AuthFrontendRoutes.ResetPassword, token, cancellationToken);
    }

    private async Task SendAsync(
        ApplicationUser user,
        AuthEmailKind kind,
        string route,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var link = BuildLink(_frontendOptions.BaseUrl, route, user.Email!, rawToken);
        var request = AuthEmailTemplates.BuildRequest(kind, user.DisplayName, link);
        var subject = AuthEmailTemplates.GetSubject(kind, _emailBrandOptions.BrandName);
        var htmlBody = emailTemplateService.Render(request);

        await emailSender.SendAsync(user.Email!, subject, htmlBody, cancellationToken);
    }

    private static string BuildLink(string baseUrl, string route, string email, string rawToken)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(rawToken);
        return $"{baseUrl.TrimEnd('/')}{route}?email={encodedEmail}&token={encodedToken}";
    }
}
