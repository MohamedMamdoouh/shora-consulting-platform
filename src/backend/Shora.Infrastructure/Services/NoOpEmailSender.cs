using Shora.Application.Abstractions;

namespace Shora.Infrastructure.Services;

public sealed class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
