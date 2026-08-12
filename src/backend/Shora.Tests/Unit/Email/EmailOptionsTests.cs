using Shora.Application.Options;

namespace Shora.Tests.Unit.Email;

public class EmailOptionsTests
{
    [Fact]
    public void IsConfigured_requires_api_key_and_from_address()
    {
        Assert.False(new EmailOptions().IsConfigured);
        Assert.False(new EmailOptions { ApiKey = "re_test" }.IsConfigured);
        Assert.False(new EmailOptions { FromAddress = "noreply@example.com" }.IsConfigured);
        Assert.True(new EmailOptions
        {
            ApiKey = "re_test",
            FromAddress = "noreply@example.com"
        }.IsConfigured);
    }
}
