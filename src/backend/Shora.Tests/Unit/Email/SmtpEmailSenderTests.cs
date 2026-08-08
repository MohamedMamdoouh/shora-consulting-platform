using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shora.Application.Options;
using Shora.Infrastructure.Services;

namespace Shora.Tests.Unit.Email;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_throws_when_email_is_not_configured()
    {
        var sender = new SmtpEmailSender(
            Options.Create(new EmailOptions()),
            NullLogger<SmtpEmailSender>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(
                "client@test.local",
                "Subject",
                "<p>Body</p>",
                TestContext.Current.CancellationToken));

        Assert.Contains("Email is not configured", exception.Message);
    }
}
