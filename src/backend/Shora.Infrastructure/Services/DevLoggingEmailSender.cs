using Microsoft.Extensions.Logging;
using Shora.Application.Abstractions;

namespace Shora.Infrastructure.Services;

public sealed class DevLoggingEmailSender(ILogger<DevLoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Dev email to {Email} | Subject: {Subject} | Body: {Body}",
            toEmail,
            subject,
            htmlBody);
        return Task.CompletedTask;
    }
}
