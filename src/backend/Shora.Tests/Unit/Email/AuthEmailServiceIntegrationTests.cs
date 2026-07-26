using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Auth;
using Shora.Application.Email;
using Shora.Application.Options;
using Shora.Infrastructure.Services;

namespace Shora.Tests.Unit.Email;

public class AuthEmailServiceIntegrationTests
{
    [Fact]
    public async Task DevLoggingEmailSender_logs_full_styled_auth_email_body()
    {
        var brand = new EmailBrandOptions();
        var service = new EmailTemplateService(Options.Create(brand));
        var logger = new CapturingLogger<DevLoggingEmailSender>();
        var emailSender = new DevLoggingEmailSender(logger);

        var request = AuthEmailTemplates.BuildRequest(
            AuthEmailKind.VerifyEmail,
            recipientName: "سارة",
            actionUrl: "http://localhost:4200/auth/verify-email?email=test%40example.com&token=abc");
        var subject = AuthEmailTemplates.GetSubject(AuthEmailKind.VerifyEmail, brand.BrandName);
        var htmlBody = service.Render(request);

        await emailSender.SendAsync("client@test.local", subject, htmlBody);

        Assert.NotNull(logger.LastMessage);
        Assert.Contains("client@test.local", logger.LastMessage);
        Assert.Contains("تأكيد بريدك الإلكتروني", logger.LastMessage);
        Assert.Contains("منصة شورى", logger.LastMessage);
        Assert.Contains("http://localhost:4200/auth/verify-email", logger.LastMessage);
        Assert.Contains("lang=\"ar\"", htmlBody);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public string? LastMessage { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastMessage = formatter(state, exception);
        }
    }
}
