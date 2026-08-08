using Shora.Application.Options;

namespace Shora.Tests.Unit.Email;

public class EmailOptionsTests
{
    [Fact]
    public void IsConfigured_requires_host_and_from_address()
    {
        Assert.False(new EmailOptions().IsConfigured);
        Assert.False(new EmailOptions { Host = "smtp.example.com" }.IsConfigured);
        Assert.False(new EmailOptions { FromAddress = "noreply@example.com" }.IsConfigured);
        Assert.True(new EmailOptions
        {
            Host = "smtp.example.com",
            FromAddress = "noreply@example.com"
        }.IsConfigured);
    }
}
